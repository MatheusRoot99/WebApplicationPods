using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Controllers
{
    public class ProdutoController : Controller
    {
        private readonly IProdutoRepository _produtoRepository;
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly BancoContext _context;
        private readonly ICurrentLojaService _currentLoja;

        public ProdutoController(
            IProdutoRepository produtoRepository,
            ICategoriaRepository categoriaRepository,
            IWebHostEnvironment hostEnvironment,
            BancoContext context,
            ICurrentLojaService currentLoja)
        {
            _produtoRepository = produtoRepository;
            _categoriaRepository = categoriaRepository;
            _hostEnvironment = hostEnvironment;
            _context = context;
            _currentLoja = currentLoja;
        }

        // ========= Flash padronizado =========
        private void FlashOk(string msg)
        {
            TempData["MensagemSucesso"] = msg;
            TempData["FlashSource"] = "Produto";
        }

        private void FlashErr(string msg)
        {
            TempData["MensagemErro"] = msg;
            TempData["FlashSource"] = "Produto";
        }

        private int GetLojaIdOrFail()
        {
            if (!_currentLoja.HasLoja || !_currentLoja.LojaId.HasValue || _currentLoja.LojaId.Value <= 0)
                throw new InvalidOperationException("Loja atual não definida. Verifique o middleware multi-loja.");
            return _currentLoja.LojaId.Value;
        }

        // ========= LISTA (painel) =========
        [Authorize(Roles = "Lojista,Admin")]
        public async Task<IActionResult> Index(
            string? q,
            int? categoriaId,
            bool? emPromocao,
            string? sort = "nome",
            int page = 1,
            int pageSize = 12)
        {
            // Gate de flash
            var src = TempData.Peek("FlashSource") as string;
            if (!string.Equals(src, "Produto", StringComparison.OrdinalIgnoreCase))
            {
                TempData.Remove("MensagemSucesso");
                TempData.Remove("MensagemErro");
            }

            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 60 ? 12 : pageSize;

            // BancoContext já filtra por loja via QueryFilter, se _hasLoja = true.
            // Mesmo assim, se Admin quiser ver "sem loja", o filtro pode estar desligado (depende do seu design).
            var query = _context.Produtos.AsNoTracking().Where(p => p.Ativo);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var termo = q.Trim();
                query = query.Where(p =>
                    p.Nome.Contains(termo) ||
                    (p.Descricao != null && p.Descricao.Contains(termo)));
            }

            if (categoriaId.HasValue)
                query = query.Where(p => p.CategoriaId == categoriaId.Value);

            if (emPromocao.HasValue)
            {
                query = emPromocao.Value
                    ? query.Where(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco)
                    : query.Where(p => !p.PrecoPromocional.HasValue || p.PrecoPromocional >= p.Preco);
            }

            query = sort switch
            {
                "preco" => query.OrderBy(p => p.Preco),
                "preco_desc" => query.OrderByDescending(p => p.Preco),
                "promo" => query.OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco)
                               .ThenBy(p => p.Nome),
                "novidades" => query.OrderByDescending(p => p.Id),
                _ => query.OrderBy(p => p.Nome),
            };

            var total = await query.CountAsync();

            var itens = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            foreach (var p in itens)
                p.DeserializarSaboresQuantidades();

            ViewBag.Busca = q;
            ViewBag.CategoriaId = categoriaId;
            ViewBag.EmPromocao = emPromocao;
            ViewBag.Sort = sort;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

            return View(itens);
        }

        // ========= PÚBLICO =========
        [AllowAnonymous]
        public IActionResult DetalhesProdutos(int id)
        {
            var produto = _context.Produtos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == id && p.Ativo);

            if (produto == null)
                return NotFound();

            produto.DeserializarSaboresQuantidades();
            produto.SaboresQuantidadesList ??= new List<ProdutoModel.SaborQuantidade>();

            var saboresDisponiveis = produto.SaboresQuantidadesList
                .Select(sq => new ProdutoModel.SaborQuantidade
                {
                    Sabor = sq.Sabor,
                    Quantidade = sq.Quantidade
                })
                .OrderByDescending(sq => sq.Quantidade > 0)
                .ThenBy(sq => sq.Sabor)
                .ToList();

            var relacionados = _context.Produtos
                .AsNoTracking()
                .Where(p => p.CategoriaId == produto.CategoriaId && p.Id != produto.Id && p.Ativo)
                .OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco)
                .ThenByDescending(p => p.Estoque > 0)
                .ThenByDescending(p => p.Id)
                .Take(4)
                .ToList();

            var viewModel = new ProdutoDetalhesViewModel
            {
                Produto = produto,
                SaboresDisponiveis = saboresDisponiveis,
                ProdutosRelacionados = relacionados
            };

            return View("Detalhes", viewModel);
        }

        // ========= CRUD (painel) =========

        [Authorize(Roles = "Lojista,Admin")]
        public IActionResult Criar()
        {
            CarregarCategorias();

            var vm = new ProdutoFormViewModel
            {
                Variacoes = new List<ProdutoFormViewModel.ProdutoVariacaoFormRow>
                    {
                        new() {
                            Nome = "Unidade",
                            Multiplicador = 1,
                            PrecoTexto = "0,01",
                            PrecoPromocionalTexto = "",
                            Estoque = 0,
                            Ativo = true
                        }
                    }

            };

            return View(vm);
        }


        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(ProdutoFormViewModel vm)
        {
            CarregarCategorias();

            // ===== normaliza + limpa variações =====
            vm.Variacoes = (vm.Variacoes ?? new())
                .Where(v => !string.IsNullOrWhiteSpace(v.Nome))
                .ToList();

            if (vm.Variacoes.Count == 0)
                ModelState.AddModelError("", "Adicione pelo menos 1 variação (Unidade, Fardo, Caixa...).");

            // ===== valida variações (server) =====
            foreach (var v in vm.Variacoes)
            {
                var preco = ParseDecimalBR(v.PrecoTexto);
                var promo = string.IsNullOrWhiteSpace(v.PrecoPromocionalTexto) ? (decimal?)null : ParseDecimalBR(v.PrecoPromocionalTexto);

                if (preco <= 0)
                    ModelState.AddModelError("", $"Preço inválido na variação: {v.Nome}");

                if (promo.HasValue && promo.Value > 0 && promo.Value >= preco)
                    ModelState.AddModelError("", $"Promo deve ser menor que o preço em: {v.Nome}");
            }

            if (!ModelState.IsValid)
                return View(vm);

            var lojaId = GetLojaIdOrFail();

            var produto = new ProdutoModel
            {
                LojaId = lojaId,
                Nome = vm.Nome,
                Descricao = vm.Descricao,
                Marca = vm.Marca,
                SKU = vm.SKU,
                CodigoBarras = vm.CodigoBarras,
                CategoriaId = vm.CategoriaId,
                RequerMaioridade = vm.RequerMaioridade,
                Ativo = vm.Ativo,
                MaisVendido = vm.MaisVendido,
                EmPromocao = vm.EmPromocao,
                DataCadastro = DateTime.Now
            };

            // imagem opcional
            if (vm.ImagemUpload is { Length: > 0 })
            {
                var erroImg = ValidateImage(vm.ImagemUpload, out var extLower);
                if (erroImg != null)
                {
                    ModelState.AddModelError(nameof(vm.ImagemUpload), erroImg);
                    return View(vm);
                }

                var pastaUploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                Directory.CreateDirectory(pastaUploads);

                var fileName = MakeShortFileName(produto.Nome, extLower);
                var caminho = Path.Combine(pastaUploads, fileName);

                using var fs = new FileStream(caminho, FileMode.Create);
                await vm.ImagemUpload.CopyToAsync(fs);

                produto.ImagemUrl = $"/imagens/produtos/{fileName}";
            }

            // variações
            foreach (var v in vm.Variacoes)
            {
                var preco = ParseDecimalBR(v.PrecoTexto);
                var promo = string.IsNullOrWhiteSpace(v.PrecoPromocionalTexto) ? (decimal?)null : ParseDecimalBR(v.PrecoPromocionalTexto);

                produto.Variacoes.Add(new ProdutoVariacaoModel
                {
                    Nome = v.Nome.Trim(),
                    Multiplicador = v.Multiplicador <= 0 ? 1 : v.Multiplicador,
                    Preco = preco,
                    PrecoPromocional = (promo.HasValue && promo.Value > 0) ? promo : null,
                    Estoque = v.Estoque,
                    SKU = v.SKU,
                    CodigoBarras = v.CodigoBarras,
                    Ativo = v.Ativo
                });
            }

            // ✅ estoque total correto (ATIVOS × multiplicador)
            produto.Estoque = produto.Variacoes
                .Where(x => x.Ativo)
                .Sum(x => x.Estoque * x.Multiplicador);

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            FlashOk("Produto cadastrado com variações!");
            return RedirectToAction(nameof(Index));
        }



        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult Editar(int id)
        {
            var produto = _context.Produtos
                .Include(p => p.Variacoes)
                .FirstOrDefault(p => p.Id == id && p.Ativo);

            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }

            CarregarCategorias();

            var ptbr = CultureInfo.GetCultureInfo("pt-BR");

            var vm = new ProdutoFormViewModel
            {
                Id = produto.Id,
                Nome = produto.Nome,
                Descricao = produto.Descricao,
                Marca = produto.Marca,
                SKU = produto.SKU,
                CodigoBarras = produto.CodigoBarras,
                CategoriaId = produto.CategoriaId,
                RequerMaioridade = produto.RequerMaioridade,
                Ativo = produto.Ativo,
                MaisVendido = produto.MaisVendido,
                EmPromocao = produto.EmPromocao,
                ImagemUrl = produto.ImagemUrl,
                Variacoes = produto.Variacoes
                    .OrderBy(v => v.Multiplicador)
                    .Select(v => new ProdutoFormViewModel.ProdutoVariacaoFormRow
                    {
                        Id = v.Id,
                        Nome = v.Nome,
                        Multiplicador = v.Multiplicador,
                        PrecoTexto = v.Preco.ToString("N2", ptbr),
                        PrecoPromocionalTexto = v.PrecoPromocional.HasValue
                            ? v.PrecoPromocional.Value.ToString("N2", ptbr)
                            : "",
                        Estoque = v.Estoque,
                        SKU = v.SKU,
                        CodigoBarras = v.CodigoBarras,
                        Ativo = v.Ativo
                    })
                    .ToList()
            };

            if (vm.Variacoes.Count == 0)
                vm.Variacoes.Add(new()
                {
                    Nome = "Unidade",
                    Multiplicador = 1,
                    PrecoTexto = "0,01",
                    PrecoPromocionalTexto = "",
                    Estoque = 0
                });

            return View(vm);
        }



        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(ProdutoFormViewModel vm)
        {
            if (vm.Id == null) return BadRequest();

            var produto = _context.Produtos
                .Include(p => p.Variacoes)
                .FirstOrDefault(p => p.Id == vm.Id.Value);

            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }

            CarregarCategorias();

            // ===== normaliza + limpa variações =====
            vm.Variacoes = (vm.Variacoes ?? new())
                .Where(v => !string.IsNullOrWhiteSpace(v.Nome))
                .ToList();

            if (vm.Variacoes.Count == 0)
                ModelState.AddModelError("", "Adicione pelo menos 1 variação.");

            // ===== valida variações (server) =====
            foreach (var v in vm.Variacoes)
            {
                var preco = ParseDecimalBR(v.PrecoTexto);
                var promo = string.IsNullOrWhiteSpace(v.PrecoPromocionalTexto) ? (decimal?)null : ParseDecimalBR(v.PrecoPromocionalTexto);

                if (preco <= 0)
                    ModelState.AddModelError("", $"Preço inválido na variação: {v.Nome}");

                if (promo.HasValue && promo.Value > 0 && promo.Value >= preco)
                    ModelState.AddModelError("", $"Promo deve ser menor que o preço em: {v.Nome}");
            }

            if (!ModelState.IsValid)
                return View(vm);

            // atualiza campos do produto
            produto.Nome = vm.Nome;
            produto.Descricao = vm.Descricao;
            produto.Marca = vm.Marca;
            produto.SKU = vm.SKU;
            produto.CodigoBarras = vm.CodigoBarras;
            produto.CategoriaId = vm.CategoriaId;
            produto.RequerMaioridade = vm.RequerMaioridade;
            produto.Ativo = vm.Ativo;
            produto.MaisVendido = vm.MaisVendido;
            produto.EmPromocao = vm.EmPromocao;

            // imagem opcional
            if (vm.ImagemUpload is { Length: > 0 })
            {
                var erroImg = ValidateImage(vm.ImagemUpload, out var extLower);
                if (erroImg != null)
                {
                    ModelState.AddModelError(nameof(vm.ImagemUpload), erroImg);
                    return View(vm);
                }

                var uploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                Directory.CreateDirectory(uploads);

                if (!string.IsNullOrEmpty(produto.ImagemUrl))
                {
                    var oldPath = Path.Combine(_hostEnvironment.WebRootPath,
                        produto.ImagemUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var fileName = MakeShortFileName(produto.Nome, extLower);
                using var fs = System.IO.File.Create(Path.Combine(uploads, fileName));
                await vm.ImagemUpload.CopyToAsync(fs);
                produto.ImagemUrl = $"/imagens/produtos/{fileName}";
            }

            // ===== sincroniza variações =====
            var idsNoForm = vm.Variacoes
                .Where(x => x.Id.HasValue && x.Id.Value > 0)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var paraRemover = produto.Variacoes.Where(v => !idsNoForm.Contains(v.Id)).ToList();
            foreach (var r in paraRemover)
                _context.ProdutoVariacoes.Remove(r);

            foreach (var row in vm.Variacoes)
            {
                var preco = ParseDecimalBR(row.PrecoTexto);
                var promo = string.IsNullOrWhiteSpace(row.PrecoPromocionalTexto) ? (decimal?)null : ParseDecimalBR(row.PrecoPromocionalTexto);

                if (row.Id.HasValue && row.Id.Value > 0)
                {
                    var ent = produto.Variacoes.First(v => v.Id == row.Id.Value);
                    ent.Nome = row.Nome.Trim();
                    ent.Multiplicador = row.Multiplicador <= 0 ? 1 : row.Multiplicador;
                    ent.Preco = preco;
                    ent.PrecoPromocional = (promo.HasValue && promo.Value > 0) ? promo : null;
                    ent.Estoque = row.Estoque;
                    ent.SKU = row.SKU;
                    ent.CodigoBarras = row.CodigoBarras;
                    ent.Ativo = row.Ativo;
                }
                else
                {
                    produto.Variacoes.Add(new ProdutoVariacaoModel
                    {
                        Nome = row.Nome.Trim(),
                        Multiplicador = row.Multiplicador <= 0 ? 1 : row.Multiplicador,
                        Preco = preco,
                        PrecoPromocional = (promo.HasValue && promo.Value > 0) ? promo : null,
                        Estoque = row.Estoque,
                        SKU = row.SKU,
                        CodigoBarras = row.CodigoBarras,
                        Ativo = row.Ativo
                    });
                }
            }

            // ✅ estoque total correto (ATIVOS × multiplicador)
            produto.Estoque = produto.Variacoes
                .Where(v => v.Ativo)
                .Sum(v => v.Estoque * v.Multiplicador);

            await _context.SaveChangesAsync();

            FlashOk("Produto e variações atualizados!");
            return RedirectToAction(nameof(Index));
        }




        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarExcluir(int id)
        {
            try
            {
                var produto = _produtoRepository.ObterPorId(id);
                if (produto == null)
                {
                    FlashErr("Produto não encontrado");
                    return RedirectToAction(nameof(Index));
                }

                _produtoRepository.Remover(id);
                FlashOk("Produto excluído com sucesso!");
            }
            catch (Exception ex)
            {
                FlashErr($"Erro ao excluir produto: {ex.Message}");
            }

            return RedirectToAction(nameof(Index));
        }

        // ========= Auxiliares =========

        private void CarregarCategorias()
        {
            var categorias = _categoriaRepository.ObterTodos()
                .OrderBy(c => c.Nome)
                .ToList();

            ViewBag.Categorias = new SelectList(categorias, "Id", "Nome");
        }

        [HttpGet]
        private List<SelectListItem> ObterTodosSabores()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Aloe Grape - Aloe Vera e Uva", Text = "Aloe Grape - Aloe Vera e Uva" },
                new SelectListItem { Value = "Banana Coconut - Banana e Água de Coco", Text = "Banana Coconut - Banana e Água de Coco" },
                new SelectListItem { Value = "Banana Ice", Text = "Banana Ice" },
                new SelectListItem { Value = "Blueberry Ice - Mirtilo Ice", Text = "Blueberry Ice - Mirtilo Ice" },
                new SelectListItem { Value = "Blueberry Straw Coco - Mirtilo, Morango, Coco", Text = "Blueberry Straw Coco - Mirtilo, Morango, Coco" },
                new SelectListItem { Value = "Grape Ice - Uva Ice", Text = "Grape Ice - Uva Ice" },
                new SelectListItem { Value = "Green Apple - Maçã Verde", Text = "Green Apple - Maçã Verde" },
                new SelectListItem { Value = "Icy Mint - Menta Ice", Text = "Icy Mint - Menta Ice" },
                new SelectListItem { Value = "Menthal - Menta e Hortelã Ice", Text = "Menthal - Menta e Hortelã Ice" },
                new SelectListItem { Value = "Pineapple Ice - Abacaxi Ice", Text = "Pineapple Ice - Abacaxi Ice" },
                new SelectListItem { Value = "Strawberry Banana - Morango e Banana", Text = "Strawberry Banana - Morango e Banana" },
                new SelectListItem { Value = "Strawberry Ice - Morango Ice", Text = "Strawberry Ice - Morango Ice" },
                new SelectListItem { Value = "Watermelon Ice - Melancia Ice", Text = "Watermelon Ice - Melancia Ice" }
            };
        }

        private List<SelectListItem> MesclarSabores(List<SelectListItem> baseSabores, IEnumerable<string> saboresDoProduto)
        {
            var set = new HashSet<string>(baseSabores.Select(s => s.Value), StringComparer.OrdinalIgnoreCase);
            var result = new List<SelectListItem>(baseSabores);

            foreach (var s in saboresDoProduto.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!set.Contains(s))
                {
                    result.Add(new SelectListItem { Value = s, Text = s + " (do produto)" });
                    set.Add(s);
                }
            }

            return result
                .OrderBy(s => s.Text.Replace(" (do produto)", ""), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? ValidateImage(IFormFile file, out string extLower)
        {
            extLower = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (!allowed.Contains(extLower))
                return "Apenas arquivos JPG, JPEG, PNG e WEBP são permitidos.";

            if (file.Length > 2 * 1024 * 1024)
                return "O tamanho da imagem não pode exceder 2MB.";

            return null;
        }

        private static string MakeShortFileName(string? productName, string extLower)
        {
            var slug = Slugify(productName ?? "produto");
            if (slug.Length > 32) slug = slug[..32];

            var guid8 = Guid.NewGuid().ToString("N")[..8];
            return $"{slug}-{guid8}{extLower}";
        }

        private static string Slugify(string s)
        {
            var slug = Regex.Replace(s ?? "", "[^a-zA-Z0-9]+", "-").Trim('-');
            return slug.ToLowerInvariant();
        }

        private static decimal ParseDecimalBR(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;

            s = s.Trim();
            s = Regex.Replace(s, @"[^\d\.,\-]", "");

            if (s.Contains(",") && s.Contains("."))
                s = s.Replace(".", "").Replace(",", ".");
            else
                s = s.Replace(",", ".");

            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        }

    }
}