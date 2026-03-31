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
using static WebApplicationPods.Models.ProdutoModel;

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

        private const int LOJA_FIXA_ID = 9;

        private int GetLojaIdOrFail()
        {
            return LOJA_FIXA_ID;
        }

        private int? GetLojaIdOrNull()
        {
            return LOJA_FIXA_ID;
        }

        private static bool IsPod(ProdutoTipo tipo) => tipo == ProdutoTipo.PodVape;
        private static bool IsBebida(ProdutoTipo tipo) => tipo == ProdutoTipo.BebidaAlcoolica;

        [Authorize(Roles = "Lojista,Admin")]
        public async Task<IActionResult> Index(string? q, int? categoriaId, bool? emPromocao, string? sort = "nome", int page = 1, int pageSize = 12)
        {
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
                .Include(p => p.Categoria)
                .Include(p => p.Variacoes);

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
                "promo" => query.OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco).ThenBy(p => p.Nome),
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

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            IQueryable<ProdutoModel> query = _context.Produtos
                .AsNoTracking()
                .Where(p => p.Id == id && p.Ativo);

            var lojaIdAtual = GetLojaIdOrNull();
            if (lojaIdAtual.HasValue)
            {
                query = query.Where(p => p.LojaId == lojaIdAtual.Value);
            }

            var produto = await query.FirstOrDefaultAsync();

            if (produto == null)
                return NotFound();

            var relacionados = await _context.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && p.LojaId == produto.LojaId && p.Id != produto.Id)
                .OrderByDescending(p => p.MaisVendido)
                .ThenByDescending(p => p.Id)
                .Take(8)
                .ToListAsync();

            var sabores = new List<SaborQuantidade>();

            if (IsPod(produto.TipoProduto) && !string.IsNullOrWhiteSpace(produto.Sabor))
            {
                sabores.Add(new SaborQuantidade
                {
                    Sabor = produto.Sabor,
                    Quantidade = produto.Estoque
                });
            }

            var vm = new ProdutoDetalhesViewModel
            {
                Produto = produto,
                SaboresDisponiveis = sabores,
                ProdutosRelacionados = relacionados
            };

            return View("Detalhes", vm);
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult CriarPadrao()
        {
            CarregarCategoriasEnum();
            ViewBag.Sabores = new List<SelectListItem>();

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
        public async Task<IActionResult> CriarPadrao(ProdutoFormSimplesViewModel vm, string submit, string? SaborSelect, string? SaborOutro)
        {
            vm.TipoProduto = ProdutoTipo.Padrao;
            vm.RequerMaioridade = false;

            vm.Sabor = (vm.Sabor ?? "").Trim();
            vm.Cor = string.IsNullOrWhiteSpace(vm.Cor) ? vm.Cor : vm.Cor.Trim();

            return await SalvarSimplesCreate(vm, submit, "CriarPadrao", nameof(CriarPadrao));
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult CriarBebida()
        {
            CarregarCategoriasEnum();
            ViewBag.Sabores = new List<SelectListItem>();

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
        public async Task<IActionResult> CriarBebida(ProdutoFormSimplesViewModel vm, string submit, string? SaborSelect, string? SaborOutro)
        {
            vm.TipoProduto = ProdutoTipo.BebidaAlcoolica;
            vm.RequerMaioridade = true;

            vm.Sabor = "";
            vm.Cor = "";

            return await SalvarSimplesCreate(vm, submit, "CriarBebida", nameof(CriarBebida));
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult CriarPod()
        {
            CarregarCategoriasEnum();
            CarregarSaboresPod(null);

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
        public async Task<IActionResult> CriarPod(ProdutoFormSimplesViewModel vm, string submit, string? SaborSelect, string? SaborOutro)
        {
            vm.TipoProduto = ProdutoTipo.PodVape;
            vm.RequerMaioridade = true;

            var saborFinal = !string.IsNullOrWhiteSpace(SaborOutro) ? SaborOutro.Trim()
                            : !string.IsNullOrWhiteSpace(SaborSelect) ? SaborSelect.Trim()
                            : (vm.Sabor ?? "").Trim();

            vm.Sabor = saborFinal;
            vm.Cor = string.IsNullOrWhiteSpace(vm.Cor) ? vm.Cor : vm.Cor.Trim();

            if (string.IsNullOrWhiteSpace(vm.Sabor))
                ModelState.AddModelError(nameof(vm.Sabor), "Informe o sabor do POD.");

            if (!ModelState.IsValid)
            {
                CarregarCategoriasEnum();
                CarregarSaboresPod(vm.Sabor);
                return View("CriarPod", vm);
            }

            return await SalvarSimplesCreate(vm, submit, "CriarPod", nameof(CriarPod));
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult EditarSimples(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var produto = _context.Produtos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == id && p.LojaId == lojaId);

            if (produto == null)
            {
                FlashErr("Produto não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            CarregarCategoriasEnum();

            if (IsPod(produto.TipoProduto))
                CarregarSaboresPod(produto.Sabor);
            else
                ViewBag.Sabores = new List<SelectListItem>();

            var vm = new ProdutoFormSimplesViewModel
            {
                Id = produto.Id,
                TipoProduto = produto.TipoProduto,
                CategoriaId = produto.CategoriaId,
                Nome = produto.Nome,
                Descricao = produto.Descricao,
                Marca = produto.Marca,
                SKU = produto.SKU,
                CodigoBarras = produto.CodigoBarras,
                Preco = produto.Preco,
                PrecoPromocional = produto.PrecoPromocional,
                Estoque = produto.Estoque,
                ImagemUrl = produto.ImagemUrl,
                Ativo = produto.Ativo,
                MaisVendido = produto.MaisVendido,
                RequerMaioridade = produto.RequerMaioridade,
                Sabor = produto.Sabor,
                Cor = produto.Cor,
                BebidaVolumeMl = produto.BebidaVolumeMl,
                BebidaTipo = produto.BebidaTipo,
                BebidaEmbalagem = produto.BebidaEmbalagem ?? BebidaEmbalagemTipo.NaoInformado,
                BebidaQtdPorEmbalagem = produto.BebidaQtdPorEmbalagem,
                BebidaTeorAlcoolico = produto.BebidaTeorAlcoolico,
                PodPuffs = produto.PodPuffs,
                PodCapacidadeBateria = produto.PodCapacidadeBateria,
                PodTipo = produto.PodTipo
            };

            return View(vm);
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarSimples(int id, ProdutoFormSimplesViewModel vm, string? SaborSelect, string? SaborOutro)
        {
            if (vm.Id != id) vm.Id = id;

            if (IsPod(vm.TipoProduto))
            {
                var saborFinal = !string.IsNullOrWhiteSpace(SaborOutro) ? SaborOutro.Trim()
                                : !string.IsNullOrWhiteSpace(SaborSelect) ? SaborSelect.Trim()
                                : (vm.Sabor ?? "").Trim();

                vm.Sabor = saborFinal;
                vm.Cor = string.IsNullOrWhiteSpace(vm.Cor) ? vm.Cor : vm.Cor.Trim();

                if (string.IsNullOrWhiteSpace(vm.Sabor))
                    ModelState.AddModelError(nameof(vm.Sabor), "Informe o sabor do POD.");
            }
            else if (IsBebida(vm.TipoProduto))
            {
                vm.Sabor = "";
                vm.Cor = "";
            }
            else
            {
                vm.Sabor = (vm.Sabor ?? "").Trim();
                vm.Cor = string.IsNullOrWhiteSpace(vm.Cor) ? vm.Cor : vm.Cor.Trim();
            }

            return await SalvarSimplesUpdate(vm, "EditarSimples");
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult Editar(int id)
        {
            return RedirectToAction(nameof(EditarSimples), new { id });
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Editar(int id, ProdutoFormSimplesViewModel vm, string? SaborSelect, string? SaborOutro)
        {
            return EditarSimples(id, vm, SaborSelect, SaborOutro);
        }

        private async Task<IActionResult> SalvarSimplesCreate(ProdutoFormSimplesViewModel vm, string submit, string viewName, string redirectNewAction)
        {
            CarregarCategoriasEnum();
            if (IsPod(vm.TipoProduto))
                CarregarSaboresPod(vm.Sabor);
            else
                ViewBag.Sabores = new List<SelectListItem>();

            if (Request.Form["Preco"].ToString().Contains(",") || Request.Form["PrecoPromocional"].ToString().Contains(","))
            {
                ModelState.Remove(nameof(vm.Preco));
                ModelState.Remove(nameof(vm.PrecoPromocional));
            }

            NormalizeMoneyServerSide(vm);

            if (IsPod(vm.TipoProduto) || IsBebida(vm.TipoProduto))
                vm.RequerMaioridade = true;

            if (IsBebida(vm.TipoProduto))
            {
                vm.Sabor = "";
                vm.Cor = "";

                var emb = vm.BebidaEmbalagem;
                var exigeQtd = emb == BebidaEmbalagemTipo.Caixa
                            || emb == BebidaEmbalagemTipo.Fardo
                            || emb == BebidaEmbalagemTipo.Pack;

                if (exigeQtd && (!vm.BebidaQtdPorEmbalagem.HasValue || vm.BebidaQtdPorEmbalagem <= 0))
                {
                    ModelState.AddModelError(nameof(vm.BebidaQtdPorEmbalagem), "Informe a quantidade por embalagem (ex.: 6, 12, 24).");
                }
            }

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
                BebidaVolumeMl = IsBebida(vm.TipoProduto) ? vm.BebidaVolumeMl : null,
                BebidaTipo = IsBebida(vm.TipoProduto) ? vm.BebidaTipo?.Trim() : null,
                BebidaEmbalagem = IsBebida(vm.TipoProduto) ? vm.BebidaEmbalagem : null,
                BebidaQtdPorEmbalagem = IsBebida(vm.TipoProduto) ? vm.BebidaQtdPorEmbalagem : null,
                BebidaTeorAlcoolico = IsBebida(vm.TipoProduto) ? vm.BebidaTeorAlcoolico : null,
                Preco = vm.Preco,
                PrecoPromocional = (vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0) ? vm.PrecoPromocional : null,
                EmPromocao = vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0 && vm.PrecoPromocional.Value < vm.Preco,
                Estoque = vm.Estoque < 0 ? 0 : vm.Estoque,
                Ativo = vm.Ativo,
                MaisVendido = vm.MaisVendido,
                RequerMaioridade = vm.RequerMaioridade,
                DataCadastro = DateTime.Now,
                PodPuffs = IsPod(vm.TipoProduto) ? vm.PodPuffs : null,
                PodCapacidadeBateria = IsPod(vm.TipoProduto) ? vm.PodCapacidadeBateria?.Trim() : null,
                PodTipo = IsPod(vm.TipoProduto) ? vm.PodTipo?.Trim() : null,
                Sabor = IsPod(vm.TipoProduto) ? (vm.Sabor ?? "").Trim()
                      : IsBebida(vm.TipoProduto) ? ""
                      : (vm.Sabor ?? "").Trim(),
                Cor = IsBebida(vm.TipoProduto) ? "" : (string.IsNullOrWhiteSpace(vm.Cor) ? vm.Cor : vm.Cor!.Trim())
            };

            if (vm.ImagemUpload is { Length: > 0 })
            {
                var erroImg = ValidateImage(vm.ImagemUpload, out _);
                if (erroImg != null)
                {
                    ModelState.AddModelError(nameof(vm.ImagemUpload), erroImg);
                    return View(viewName, vm);
                }

                produto.ImagemUrl = await SaveImageAndReturnUrl(vm.ImagemUpload, produto.Nome);
            }

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            FlashOk("Produto cadastrado!");

            if (string.Equals(submit, "save_new", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(redirectNewAction);

            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> SalvarSimplesUpdate(ProdutoFormSimplesViewModel vm, string viewName)
        {
            CarregarCategoriasEnum();
            if (IsPod(vm.TipoProduto))
                CarregarSaboresPod(vm.Sabor);
            else
                ViewBag.Sabores = new List<SelectListItem>();

            if (Request.Form["Preco"].ToString().Contains(",") || Request.Form["PrecoPromocional"].ToString().Contains(","))
            {
                ModelState.Remove(nameof(vm.Preco));
                ModelState.Remove(nameof(vm.PrecoPromocional));
            }

            NormalizeMoneyServerSide(vm);

            if (IsPod(vm.TipoProduto) || IsBebida(vm.TipoProduto))
                vm.RequerMaioridade = true;

            if (IsBebida(vm.TipoProduto))
            {
                vm.Sabor = "";
                vm.Cor = "";

                var emb = vm.BebidaEmbalagem;
                var exigeQtd = emb == BebidaEmbalagemTipo.Caixa
                            || emb == BebidaEmbalagemTipo.Fardo
                            || emb == BebidaEmbalagemTipo.Pack;

                if (exigeQtd && (!vm.BebidaQtdPorEmbalagem.HasValue || vm.BebidaQtdPorEmbalagem <= 0))
                {
                    ModelState.AddModelError(nameof(vm.BebidaQtdPorEmbalagem), "Informe a quantidade por embalagem (ex.: 6, 12, 24).");
                }
            }

            if (vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0 && vm.PrecoPromocional.Value >= vm.Preco)
                ModelState.AddModelError(nameof(vm.PrecoPromocional), "Promo deve ser menor que o preço.");

            if (!ModelState.IsValid)
                return View(viewName, vm);

            var lojaId = GetLojaIdOrFail();

            var produto = await _context.Produtos
                .FirstOrDefaultAsync(p => p.Id == vm.Id && p.LojaId == lojaId);

            if (produto == null)
            {
                FlashErr("Produto não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            produto.TipoProduto = vm.TipoProduto;
            produto.CategoriaId = vm.CategoriaId;
            produto.Nome = vm.Nome;
            produto.Descricao = vm.Descricao;
            produto.Marca = vm.Marca;
            produto.SKU = vm.SKU;
            produto.CodigoBarras = vm.CodigoBarras;
            produto.Preco = vm.Preco;
            produto.PrecoPromocional = (vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0) ? vm.PrecoPromocional : null;
            produto.EmPromocao = produto.PrecoPromocional.HasValue && produto.PrecoPromocional.Value > 0 && produto.PrecoPromocional.Value < produto.Preco;
            produto.Estoque = vm.Estoque < 0 ? 0 : vm.Estoque;
            produto.BebidaVolumeMl = IsBebida(vm.TipoProduto) ? vm.BebidaVolumeMl : null;
            produto.BebidaTipo = IsBebida(vm.TipoProduto) ? vm.BebidaTipo?.Trim() : null;
            produto.BebidaEmbalagem = IsBebida(vm.TipoProduto) ? vm.BebidaEmbalagem : null;
            produto.BebidaQtdPorEmbalagem = IsBebida(vm.TipoProduto) ? vm.BebidaQtdPorEmbalagem : null;
            produto.BebidaTeorAlcoolico = IsBebida(vm.TipoProduto) ? vm.BebidaTeorAlcoolico : null;
            produto.Ativo = vm.Ativo;
            produto.MaisVendido = vm.MaisVendido;
            produto.RequerMaioridade = vm.RequerMaioridade;
            produto.PodPuffs = IsPod(vm.TipoProduto) ? vm.PodPuffs : null;
            produto.PodCapacidadeBateria = IsPod(vm.TipoProduto) ? vm.PodCapacidadeBateria?.Trim() : null;
            produto.PodTipo = IsPod(vm.TipoProduto) ? vm.PodTipo?.Trim() : null;
            produto.Sabor = IsPod(vm.TipoProduto) ? (vm.Sabor ?? "").Trim()
                         : IsBebida(vm.TipoProduto) ? ""
                         : (vm.Sabor ?? "").Trim();
            produto.Cor = IsBebida(vm.TipoProduto) ? "" : (string.IsNullOrWhiteSpace(vm.Cor) ? vm.Cor : vm.Cor!.Trim());

            if (vm.ImagemUpload is { Length: > 0 })
            {
                var erroImg = ValidateImage(vm.ImagemUpload, out _);
                if (erroImg != null)
                {
                    ModelState.AddModelError(nameof(vm.ImagemUpload), erroImg);
                    return View(viewName, vm);
                }

                produto.ImagemUrl = await SaveImageAndReturnUrl(vm.ImagemUpload, produto.Nome);
            }

            await _context.SaveChangesAsync();

            FlashOk("Produto atualizado!");
            return RedirectToAction(nameof(Index));
        }

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

        private void CarregarCategoriasEnum()
        {
            var categorias = _categoriaRepository.ObterTodos()
                .OrderBy(c => c.Nome)
                .ToList();

            ViewBag.Categorias = new SelectList(categorias, "Id", "Nome");
        }

        private void CarregarSaboresPod(string? saborAtual)
        {
            var baseSabores = ObterTodosSabores();
            var merged = MesclarSabores(baseSabores, new[] { saborAtual ?? "" });
            ViewBag.Sabores = merged;
        }

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

        private async Task<string> SaveImageAndReturnUrl(IFormFile file, string? productName)
        {
            var extLower = Path.GetExtension(file.FileName).ToLowerInvariant();
            var pastaUploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
            Directory.CreateDirectory(pastaUploads);

            var fileName = MakeShortFileName(productName, extLower);
            var caminho = Path.Combine(pastaUploads, fileName);

            using var fs = new FileStream(caminho, FileMode.Create);
            await file.CopyToAsync(fs);

            return $"/imagens/produtos/{fileName}";
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

        private void NormalizeMoneyServerSide(ProdutoFormSimplesViewModel vm)
        {
            var precoRaw = GetRawFromModelState(nameof(vm.Preco));
            if (!string.IsNullOrWhiteSpace(precoRaw))
                vm.Preco = ParseDecimalBR(precoRaw);

            var promoRaw = GetRawFromModelState(nameof(vm.PrecoPromocional));
            if (!string.IsNullOrWhiteSpace(promoRaw))
                vm.PrecoPromocional = ParseNullableDecimalBR(promoRaw);
        }

        private string? GetRawFromModelState(string key)
        {
            if (ModelState.TryGetValue(key, out var entry))
                return entry.AttemptedValue;

            return null;
        }

        private static decimal? ParseNullableDecimalBR(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            var d = ParseDecimalBR(s);
            return d <= 0 ? null : d;
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

    public enum ProdutoCategoriaEnum
    {
        Bebidas = 1,
        Pods = 2,
        Doces = 3,
        Snacks = 4
    }
}