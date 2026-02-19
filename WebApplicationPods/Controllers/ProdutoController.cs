using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
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

        private static bool IsPod(ProdutoTipo tipo) => tipo == ProdutoTipo.PodVape;
        private static bool IsBebida(ProdutoTipo tipo) => tipo == ProdutoTipo.BebidaAlcoolica;

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

            var lojaId = GetLojaIdOrFail();

            IQueryable<ProdutoModel> query = _context.Produtos
                .AsNoTracking()
                .Where(p => p.LojaId == lojaId && p.Ativo)
                .Include(p => p.Categoria);

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

            IQueryable<ProdutoModel> queryOrdenada = sort switch
            {
                "preco" => query.OrderBy(p => p.Preco),
                "preco_desc" => query.OrderByDescending(p => p.Preco),
                "promo" => query.OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco)
                               .ThenBy(p => p.Nome),
                "novidades" => query.OrderByDescending(p => p.Id),
                _ => query.OrderBy(p => p.Nome),
            };

            var total = await queryOrdenada.CountAsync();

            var itens = await queryOrdenada
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

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
                .Include(p => p.Categoria)
                .FirstOrDefault(p => p.Id == id && p.Ativo);

            if (produto == null)
                return NotFound();

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
                SaboresDisponiveis = new List<ProdutoModel.SaborQuantidade>(), // não usado no novo fluxo
                ProdutosRelacionados = relacionados
            };

            return View("Detalhes", viewModel);
        }

        // ========= CRIAR PADRÃO =========
        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult CriarPadrao()
        {
            CarregarCategorias();
            return View(new ProdutoFormSimplesViewModel
            {
                TipoProduto = ProdutoTipo.Padrao,
                Ativo = true,
                RequerMaioridade = false
            });
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarPadrao(ProdutoFormSimplesViewModel vm, string submit)
        {
            vm.TipoProduto = ProdutoTipo.Padrao;
            vm.RequerMaioridade = false;
            return await SalvarSimples(vm, submit, viewName: "CriarPadrao", redirectNewAction: nameof(CriarPadrao));
        }

        // ========= CRIAR BEBIDA =========
        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult CriarBebida()
        {
            CarregarCategorias();
            return View(new ProdutoFormSimplesViewModel
            {
                TipoProduto = ProdutoTipo.BebidaAlcoolica,
                Ativo = true,
                RequerMaioridade = true
            });
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarBebida(ProdutoFormSimplesViewModel vm, string submit)
        {
            vm.TipoProduto = ProdutoTipo.BebidaAlcoolica;
            vm.RequerMaioridade = true; // força no server
            return await SalvarSimples(vm, submit, viewName: "CriarBebida", redirectNewAction: nameof(CriarBebida));
        }

        // ========= CRIAR POD =========
        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult CriarPod()
        {
            CarregarCategorias();
            return View(new ProdutoFormSimplesViewModel
            {
                TipoProduto = ProdutoTipo.PodVape,
                Ativo = true,
                RequerMaioridade = true
            });
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarPod(ProdutoFormSimplesViewModel vm, string submit)
        {
            vm.TipoProduto = ProdutoTipo.PodVape;
            vm.RequerMaioridade = true; // força no server

            // Se quiser obrigar sabor no POD:
            if (string.IsNullOrWhiteSpace(vm.Sabor))
                ModelState.AddModelError(nameof(vm.Sabor), "Informe o sabor.");

            if (!ModelState.IsValid)
            {
                CarregarCategorias();
                return View("CriarPod", vm);
            }

            return await SalvarSimples(vm, submit, viewName: "CriarPod", redirectNewAction: nameof(CriarPod));
        }

        // ========= SALVAR (COMPARTILHADO) =========
        private async Task<IActionResult> SalvarSimples(
            ProdutoFormSimplesViewModel vm,
            string submit,
            string viewName,
            string redirectNewAction)
        {
            CarregarCategorias();

            // força maioridade para Pod/Bebida
            if (IsPod(vm.TipoProduto) || IsBebida(vm.TipoProduto))
                vm.RequerMaioridade = true;

            // valida promo
            if (vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0 && vm.PrecoPromocional.Value >= vm.Preco)
                ModelState.AddModelError(nameof(vm.PrecoPromocional), "Promo deve ser menor que o preço.");

            if (!ModelState.IsValid)
                return View(viewName, vm);

            var lojaId = GetLojaIdOrFail();

            var produto = new ProdutoModel
            {
                LojaId = lojaId,
                TipoProduto = vm.TipoProduto,

                Nome = vm.Nome,
                Descricao = vm.Descricao,
                Marca = vm.Marca,
                SKU = vm.SKU,
                CodigoBarras = vm.CodigoBarras,
                CategoriaId = vm.CategoriaId,

                Preco = vm.Preco,
                PrecoPromocional = (vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0)
                    ? vm.PrecoPromocional
                    : null,
                EmPromocao = vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0 && vm.PrecoPromocional.Value < vm.Preco,

                Estoque = vm.Estoque < 0 ? 0 : vm.Estoque,

                Ativo = vm.Ativo,
                MaisVendido = vm.MaisVendido,
                RequerMaioridade = vm.RequerMaioridade,
                DataCadastro = DateTime.Now,

                // POD extras
                PodPuffs = IsPod(vm.TipoProduto) ? vm.PodPuffs : null,
                PodCapacidadeBateria = IsPod(vm.TipoProduto) ? vm.PodCapacidadeBateria?.Trim() : null,
                PodTipo = IsPod(vm.TipoProduto) ? vm.PodTipo?.Trim() : null,

                // atributos simples
                Sabor = (vm.Sabor ?? "").Trim(),
                Cor = vm.Cor
            };

            // imagem
            if (vm.ImagemUpload is { Length: > 0 })
            {
                var erroImg = ValidateImage(vm.ImagemUpload, out var extLower);
                if (erroImg != null)
                {
                    ModelState.AddModelError(nameof(vm.ImagemUpload), erroImg);
                    return View(viewName, vm);
                }

                var pastaUploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                Directory.CreateDirectory(pastaUploads);

                var fileName = MakeShortFileName(produto.Nome, extLower);
                var caminho = Path.Combine(pastaUploads, fileName);

                using var fs = new FileStream(caminho, FileMode.Create);
                await vm.ImagemUpload.CopyToAsync(fs);

                produto.ImagemUrl = $"/imagens/produtos/{fileName}";
            }

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            FlashOk("Produto cadastrado!");

            // submit: save_new => volta pra tela de criar
            if (string.Equals(submit, "save_new", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(redirectNewAction);

            return RedirectToAction(nameof(Index));
        }

        // ========= EXCLUIR (GET - tela de confirmação) =========
        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult Excluir(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var produto = _context.Produtos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .FirstOrDefault(p => p.Id == id && p.LojaId == lojaId);

            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }

            return View(produto);
        }

        // ========= EXCLUIR (POST - confirma exclusão) =========
        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarExcluir(int id)
        {
            try
            {
                var lojaId = GetLojaIdOrFail();

                var produto = await _context.Produtos
                    .FirstOrDefaultAsync(p => p.Id == id && p.LojaId == lojaId);

                if (produto == null)
                {
                    FlashErr("Produto não encontrado");
                    return RedirectToAction(nameof(Index));
                }

                // ✅ Soft delete
                produto.Ativo = false;
                await _context.SaveChangesAsync();

                FlashOk("Produto excluído (desativado) com sucesso!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                FlashErr($"Erro ao excluir produto: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        // ========= Auxiliares =========
        private void CarregarCategorias()
        {
            var categorias = _categoriaRepository.ObterTodos()
                .OrderBy(c => c.Nome)
                .ToList();

            ViewBag.Categorias = new SelectList(categorias, "Id", "Nome");
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

        // Mantive porque você usa noutros lugares às vezes; se não usa mais, pode apagar.
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
