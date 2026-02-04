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
            try
            {
                CarregarCategorias();

                var model = new ProdutoModel
                {
                    TodosSabores = ObterTodosSabores()
                };

                return View(model);
            }
            catch
            {
                FlashErr("Erro ao carregar o formulário de cadastro");
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(ProdutoModel produto)
        {
            try
            {
                // Sabores do form
                var saboresList = new List<ProdutoModel.SaborQuantidade>();
                produto.SaboresQuantidadesList = saboresList;

                if (Request.Form.TryGetValue("SaboresQuantidadesList", out var valoresForm))
                {
                    foreach (var item in valoresForm)
                    {
                        if (string.IsNullOrWhiteSpace(item)) continue;
                        try
                        {
                            var sq = JsonConvert.DeserializeObject<ProdutoModel.SaborQuantidade>(item);
                            if (sq != null && !string.IsNullOrWhiteSpace(sq.Sabor) && sq.Quantidade > 0)
                                saboresList.Add(sq);
                        }
                        catch
                        {
                            ModelState.AddModelError("", "Houve um erro ao processar os sabores.");
                        }
                    }
                }

                if (saboresList.Count == 0)
                    ModelState.AddModelError("", "Adicione pelo menos um sabor com quantidade válida.");

                produto.Estoque = saboresList.Sum(s => s.Quantidade);
                produto.SerializarSaboresQuantidades();

                // Remover validações não usadas diretamente
                ModelState.Remove(nameof(ProdutoModel.Categoria));
                ModelState.Remove(nameof(ProdutoModel.ImagemUrl));
                ModelState.Remove(nameof(ProdutoModel.SaboresQuantidades));
                ModelState.Remove(nameof(ProdutoModel.SaboresQuantidadesList));

                if (!ModelState.IsValid)
                {
                    produto.TodosSabores = ObterTodosSabores();
                    CarregarCategorias();
                    return View(produto);
                }

                // Loja atual
                var lojaId = GetLojaIdOrFail();
                produto.LojaId = lojaId;

                // Imagem (opcional)
                if (produto.ImagemUpload is { Length: > 0 })
                {
                    var file = produto.ImagemUpload;
                    var erroImg = ValidateImage(file, out var extLower);

                    if (erroImg != null)
                    {
                        ModelState.AddModelError(nameof(ProdutoModel.ImagemUpload), erroImg);
                        produto.TodosSabores = ObterTodosSabores();
                        CarregarCategorias();
                        return View(produto);
                    }

                    var pastaUploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                    Directory.CreateDirectory(pastaUploads);

                    var fileName = MakeShortFileName(produto.Nome, extLower);
                    var caminhoArquivo = Path.Combine(pastaUploads, fileName);

                    using (var fs = new FileStream(caminhoArquivo, FileMode.Create))
                        await file.CopyToAsync(fs);

                    produto.ImagemUrl = $"/imagens/produtos/{fileName}";
                }

                _produtoRepository.Adicionar(produto);
                FlashOk("Produto cadastrado com sucesso!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                FlashErr($"Erro ao cadastrar produto: {ex.Message}");
                produto.TodosSabores = ObterTodosSabores();
                CarregarCategorias();
                return View(produto);
            }
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult Editar(int id)
        {
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }

            produto.DeserializarSaboresQuantidades();
            produto.SaboresQuantidadesList ??= new List<ProdutoModel.SaborQuantidade>();

            var baseSabores = ObterTodosSabores();
            produto.TodosSabores = MesclarSabores(baseSabores, produto.SaboresQuantidadesList.Select(s => s.Sabor));

            CarregarCategorias();
            return View(produto);
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Editar")]
        public async Task<IActionResult> EditarPost(int id)
        {
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }

            // Conversão pt-BR
            static decimal? ParsePtBr(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return null;
                if (decimal.TryParse(raw, NumberStyles.Currency, new CultureInfo("pt-BR"), out var d))
                    return d;
                raw = raw.Replace(".", "").Replace(",", ".");
                return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : (decimal?)null;
            }

            var precoRaw = Request.Form[nameof(ProdutoModel.Preco)];
            var promoRaw = Request.Form[nameof(ProdutoModel.PrecoPromocional)];

            var preco = ParsePtBr(precoRaw);
            if (preco is null || preco <= 0) ModelState.AddModelError(nameof(ProdutoModel.Preco), "Preço inválido. Ex.: 179,90");
            else { produto.Preco = preco.Value; ModelState.Remove(nameof(ProdutoModel.Preco)); }

            if (string.IsNullOrWhiteSpace(promoRaw))
            {
                produto.PrecoPromocional = null;
                ModelState.Remove(nameof(ProdutoModel.PrecoPromocional));
            }
            else
            {
                var promo = ParsePtBr(promoRaw);
                if (promo is null || promo <= 0) ModelState.AddModelError(nameof(ProdutoModel.PrecoPromocional), "Preço promocional inválido.");
                else { produto.PrecoPromocional = promo.Value; ModelState.Remove(nameof(ProdutoModel.PrecoPromocional)); }
            }

            var ok = await TryUpdateModelAsync(produto, prefix: "",
                p => p.Nome, p => p.Descricao, p => p.CategoriaId, p => p.Ativo,
                p => p.EmPromocao, p => p.MaisVendido, p => p.Sabor, p => p.Cor,
                p => p.Puffs, p => p.CapacidadeBateria, p => p.RequerMaioridade);

            if (!ok)
                ModelState.AddModelError(string.Empty, "Não foi possível vincular os dados do formulário.");

            // Sabores do form
            var saboresList = new List<ProdutoModel.SaborQuantidade>();
            if (Request.Form.TryGetValue("SaboresQuantidadesList", out var itens))
            {
                foreach (var item in itens)
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    try
                    {
                        var sq = JsonConvert.DeserializeObject<ProdutoModel.SaborQuantidade>(item);
                        if (sq != null && !string.IsNullOrWhiteSpace(sq.Sabor) && sq.Quantidade > 0)
                            saboresList.Add(sq);
                    }
                    catch
                    {
                        ModelState.AddModelError("", "Erro ao processar os sabores.");
                    }
                }
            }

            if (saboresList.Count == 0)
                ModelState.AddModelError("", "Adicione pelo menos um sabor com quantidade válida.");

            produto.SaboresQuantidadesList = saboresList;
            produto.Estoque = saboresList.Sum(s => s.Quantidade);
            produto.SerializarSaboresQuantidades();
            ModelState.Remove(nameof(ProdutoModel.SaboresQuantidades));

            // Regras promo
            if (produto.EmPromocao)
            {
                if (!produto.PrecoPromocional.HasValue)
                    ModelState.AddModelError(nameof(ProdutoModel.PrecoPromocional), "Informe o preço promocional.");
                else if (produto.PrecoPromocional.Value >= produto.Preco)
                    ModelState.AddModelError(nameof(ProdutoModel.PrecoPromocional), "Preço promocional deve ser menor que o preço.");
            }
            else
            {
                produto.PrecoPromocional = null;
            }

            // Imagem opcional
            ModelState.Remove(nameof(ProdutoModel.ImagemUpload));
            var file = Request.Form.Files[nameof(ProdutoModel.ImagemUpload)];
            if (file is { Length: > 0 })
            {
                var erroImg = ValidateImage(file, out _);
                if (erroImg != null)
                    ModelState.AddModelError(nameof(ProdutoModel.ImagemUpload), erroImg);
            }

            if (!ModelState.IsValid)
            {
                produto.DeserializarSaboresQuantidades();
                var baseSabores = ObterTodosSabores();
                produto.TodosSabores = MesclarSabores(baseSabores, produto.SaboresQuantidadesList.Select(s => s.Sabor));
                CarregarCategorias();
                return View("Editar", produto);
            }

            if (file is { Length: > 0 })
            {
                var uploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                Directory.CreateDirectory(uploads);

                if (!string.IsNullOrEmpty(produto.ImagemUrl))
                {
                    var oldPath = Path.Combine(_hostEnvironment.WebRootPath, produto.ImagemUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var extLower = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = MakeShortFileName(produto.Nome, extLower);
                using var fs = System.IO.File.Create(Path.Combine(uploads, fileName));
                await file.CopyToAsync(fs);
                produto.ImagemUrl = $"/imagens/produtos/{fileName}";
            }

            _produtoRepository.Atualizar(produto);
            FlashOk("Produto atualizado com sucesso!");
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult Excluir(int id)
        {
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }
            return View(produto);
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
    }
}
