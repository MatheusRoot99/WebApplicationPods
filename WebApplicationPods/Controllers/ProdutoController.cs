using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
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
        private readonly IConfiguration _configuration;

        public ProdutoController(
            IProdutoRepository produtoRepository,
            ICategoriaRepository categoriaRepository,
            IWebHostEnvironment hostEnvironment,
            BancoContext context,
            ICurrentLojaService currentLoja,
            IConfiguration configuration)
        {
            _produtoRepository = produtoRepository;
            _categoriaRepository = categoriaRepository;
            _hostEnvironment = hostEnvironment;
            _context = context;
            _currentLoja = currentLoja;
            _configuration = configuration;
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

        private bool IgnorarLojaNoAmbienteAtual()
        {
            return _configuration.GetValue<bool?>("DevelopmentSettings:LocalhostOnly")
                ?? _configuration.GetValue<bool?>("AppSettings:LocalhostOnly")
                ?? _hostEnvironment.IsDevelopment();
        }

        private int? GetLojaIdDoUsuario()
        {
            var claimLojaId = User.FindFirst("LojaId")?.Value
                           ?? User.FindFirst("lojaId")?.Value;

            if (int.TryParse(claimLojaId, out var lojaId) && lojaId > 0)
                return lojaId;

            return null;
        }

        private int? GetLojaIdOrNull()
        {
            if (_currentLoja?.LojaId is int lojaAtual && lojaAtual > 0)
                return lojaAtual;

            var lojaUsuario = GetLojaIdDoUsuario();
            if (lojaUsuario.HasValue)
                return lojaUsuario.Value;

            if (IgnorarLojaNoAmbienteAtual())
                return null;

            return null;
        }

        private int GetLojaIdOrFail()
        {
            if (_currentLoja?.LojaId is int lojaAtual && lojaAtual > 0)
                return lojaAtual;

            var lojaUsuario = GetLojaIdDoUsuario();
            if (lojaUsuario.HasValue)
                return lojaUsuario.Value;

            if (IgnorarLojaNoAmbienteAtual())
            {
                var defaultLojaId =
                    _configuration.GetValue<int?>("DevelopmentSettings:DefaultLojaId")
                    ?? _configuration.GetValue<int?>("AppSettings:DefaultLojaId");

                if (defaultLojaId.HasValue && defaultLojaId.Value > 0)
                    return defaultLojaId.Value;
            }

            throw new InvalidOperationException("Loja atual não definida. Acesse pelo subdomínio da loja ou vincule o usuário a uma loja.");
        }

        private static bool IsPod(ProdutoTipo tipo) => tipo == ProdutoTipo.PodVape;

        private static bool IsBebida(ProdutoTipo tipo) => tipo == ProdutoTipo.BebidaAlcoolica;

        private static bool IsEmbalagemComposta(BebidaEmbalagemTipo? embalagem)
        {
            return embalagem is BebidaEmbalagemTipo.Pack
                or BebidaEmbalagemTipo.Fardo
                or BebidaEmbalagemTipo.Caixa;
        }

        // ============================================================
        // LISTAGEM
        // ============================================================

        [Authorize(Roles = "Lojista,Admin")]
        public async Task<IActionResult> Index(
            string? q,
            int? categoriaId,
            bool? emPromocao,
            string? sort = "nome",
            int page = 1,
            int pageSize = 12)
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
                    (p.Descricao != null && p.Descricao.Contains(termo)) ||
                    (p.Marca != null && p.Marca.Contains(termo)));
            }

            if (categoriaId.HasValue)
                query = query.Where(p => p.CategoriaId == categoriaId.Value);

            if (emPromocao.HasValue)
            {
                query = emPromocao.Value
                    ? query.Where(p => p.PrecoPromocional.HasValue && p.PrecoPromocional.Value > 0 && p.PrecoPromocional.Value < p.Preco)
                    : query.Where(p => !p.PrecoPromocional.HasValue || p.PrecoPromocional.Value <= 0 || p.PrecoPromocional.Value >= p.Preco);
            }

            IQueryable<ProdutoModel> queryOrdenada = sort switch
            {
                "preco" => query.OrderBy(p => p.Preco),
                "preco_desc" => query.OrderByDescending(p => p.Preco),
                "promo" => query
                    .OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional.Value > 0 && p.PrecoPromocional.Value < p.Preco)
                    .ThenBy(p => p.Nome),
                "estoque" => query.OrderBy(p => p.Estoque).ThenBy(p => p.Nome),
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

        // ============================================================
        // DETALHES PÚBLICOS
        // ============================================================

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            IQueryable<ProdutoModel> query = _context.Produtos
                .AsNoTracking()
                .Where(p => p.Id == id && p.Ativo);

            var lojaIdAtual = GetLojaIdOrNull();
            if (lojaIdAtual.HasValue)
                query = query.Where(p => p.LojaId == lojaIdAtual.Value);

            var produto = await query.FirstOrDefaultAsync();

            if (produto == null)
                return NotFound();

            var relacionados = await _context.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && p.LojaId == produto.LojaId && p.Id != produto.Id)
                .OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional.Value > 0 && p.PrecoPromocional.Value < p.Preco)
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

        // ============================================================
        // CRIAR PRODUTO PADRÃO
        // ============================================================

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
                RequerMaioridade = false,
                Estoque = 0
            });
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarPadrao(
            ProdutoFormSimplesViewModel vm,
            string submit,
            string? SaborSelect,
            string? SaborOutro)
        {
            vm.TipoProduto = ProdutoTipo.Padrao;
            PrepararVmAntesValidacao(vm, SaborSelect, SaborOutro);

            return await SalvarSimplesCreate(vm, submit, "CriarPadrao", nameof(CriarPadrao));
        }

        // ============================================================
        // CRIAR BEBIDA
        // ============================================================

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
                RequerMaioridade = true,
                Estoque = 0,
                BebidaEmbalagem = BebidaEmbalagemTipo.NaoInformado,
                BebidaQtdPorEmbalagem = 1
            });
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarBebida(
            ProdutoFormSimplesViewModel vm,
            string submit,
            string? SaborSelect,
            string? SaborOutro)
        {
            vm.TipoProduto = ProdutoTipo.BebidaAlcoolica;
            PrepararVmAntesValidacao(vm, SaborSelect, SaborOutro);

            return await SalvarSimplesCreate(vm, submit, "CriarBebida", nameof(CriarBebida));
        }

        // ============================================================
        // CRIAR POD
        // ============================================================

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
                RequerMaioridade = true,
                Estoque = 0
            });
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarPod(
            ProdutoFormSimplesViewModel vm,
            string submit,
            string? SaborSelect,
            string? SaborOutro)
        {
            vm.TipoProduto = ProdutoTipo.PodVape;
            PrepararVmAntesValidacao(vm, SaborSelect, SaborOutro);

            return await SalvarSimplesCreate(vm, submit, "CriarPod", nameof(CriarPod));
        }

        // ============================================================
        // EDITAR
        // ============================================================

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public async Task<IActionResult> EditarSimples(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var produto = await _context.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.LojaId == lojaId);

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

                SKU = null,
                CodigoBarras = null,

                Preco = produto.Preco,
                PrecoPromocional = produto.PrecoPromocional,
                Estoque = produto.Estoque,
                ImagemUrl = produto.ImagemUrl,
                Ativo = produto.Ativo,

                MaisVendido = false,
                RequerMaioridade = IsPod(produto.TipoProduto) || IsBebida(produto.TipoProduto),

                Sabor = IsPod(produto.TipoProduto) ? produto.Sabor : "",
                Cor = "",

                BebidaVolumeMl = produto.BebidaVolumeMl,
                BebidaTipo = produto.BebidaTipo,
                BebidaEmbalagem = produto.BebidaEmbalagem ?? BebidaEmbalagemTipo.NaoInformado,
                BebidaQtdPorEmbalagem = produto.BebidaQtdPorEmbalagem ?? 1,
                BebidaTeorAlcoolico = null,

                PodPuffs = produto.PodPuffs,
                PodCapacidadeBateria = produto.PodCapacidadeBateria,
                PodTipo = produto.PodTipo
            };

            return View(vm);
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarSimples(
            int id,
            ProdutoFormSimplesViewModel vm,
            string? SaborSelect,
            string? SaborOutro)
        {
            if (vm.Id != id)
                vm.Id = id;

            PrepararVmAntesValidacao(vm, SaborSelect, SaborOutro);

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
        public Task<IActionResult> Editar(
            int id,
            ProdutoFormSimplesViewModel vm,
            string? SaborSelect,
            string? SaborOutro)
        {
            return EditarSimples(id, vm, SaborSelect, SaborOutro);
        }

        // ============================================================
        // SALVAR CREATE / UPDATE
        // ============================================================

        private async Task<IActionResult> SalvarSimplesCreate(
            ProdutoFormSimplesViewModel vm,
            string submit,
            string viewName,
            string redirectNewAction)
        {
            CarregarCombosFormulario(vm);

            NormalizarModelStateFormulario(vm);
            NormalizeMoneyServerSide(vm);
            ValidarFormularioProduto(vm);

            if (!ModelState.IsValid)
                return View(viewName, vm);

            var lojaId = GetLojaIdOrFail();

            var produto = new ProdutoModel
            {
                LojaId = lojaId,

                TipoProduto = vm.TipoProduto,
                CategoriaId = vm.CategoriaId,

                Nome = LimparTexto(vm.Nome),
                Descricao = LimparTextoOuNull(vm.Descricao),
                Marca = LimparTextoOuNull(vm.Marca),

                SKU = null,
                CodigoBarras = null,

                Preco = vm.Preco,
                PrecoPromocional = ObterPrecoPromocionalValido(vm),
                EmPromocao = TemPromocaoValida(vm),

                Estoque = Math.Max(0, vm.Estoque),

                Ativo = vm.Ativo,
                MaisVendido = false,
                RequerMaioridade = IsPod(vm.TipoProduto) || IsBebida(vm.TipoProduto),

                DataCadastro = DateTime.Now,

                Sabor = IsPod(vm.TipoProduto) ? LimparTexto(vm.Sabor) : "",
                Cor = "",

                BebidaVolumeMl = IsBebida(vm.TipoProduto) ? vm.BebidaVolumeMl : null,
                BebidaTipo = IsBebida(vm.TipoProduto) ? LimparTextoOuNull(vm.BebidaTipo) : null,
                BebidaEmbalagem = IsBebida(vm.TipoProduto) ? NormalizarEmbalagem(vm.BebidaEmbalagem) : null,
                BebidaQtdPorEmbalagem = IsBebida(vm.TipoProduto) ? NormalizarQtdPorEmbalagem(vm) : null,
                BebidaTeorAlcoolico = null,

                PodPuffs = IsPod(vm.TipoProduto) ? vm.PodPuffs : null,
                PodCapacidadeBateria = IsPod(vm.TipoProduto) ? LimparTextoOuNull(vm.PodCapacidadeBateria) : null,
                PodTipo = IsPod(vm.TipoProduto) ? LimparTextoOuNull(vm.PodTipo) : null
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

        private async Task<IActionResult> SalvarSimplesUpdate(
            ProdutoFormSimplesViewModel vm,
            string viewName)
        {
            CarregarCombosFormulario(vm);

            NormalizarModelStateFormulario(vm);
            NormalizeMoneyServerSide(vm);
            ValidarFormularioProduto(vm);

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

            produto.Nome = LimparTexto(vm.Nome);
            produto.Descricao = LimparTextoOuNull(vm.Descricao);
            produto.Marca = LimparTextoOuNull(vm.Marca);

            produto.SKU = null;
            produto.CodigoBarras = null;

            produto.Preco = vm.Preco;
            produto.PrecoPromocional = ObterPrecoPromocionalValido(vm);
            produto.EmPromocao = TemPromocaoValida(vm);

            produto.Estoque = Math.Max(0, vm.Estoque);

            produto.Ativo = vm.Ativo;
            produto.MaisVendido = false;
            produto.RequerMaioridade = IsPod(vm.TipoProduto) || IsBebida(vm.TipoProduto);

            produto.Sabor = IsPod(vm.TipoProduto) ? LimparTexto(vm.Sabor) : "";
            produto.Cor = "";

            produto.BebidaVolumeMl = IsBebida(vm.TipoProduto) ? vm.BebidaVolumeMl : null;
            produto.BebidaTipo = IsBebida(vm.TipoProduto) ? LimparTextoOuNull(vm.BebidaTipo) : null;
            produto.BebidaEmbalagem = IsBebida(vm.TipoProduto) ? NormalizarEmbalagem(vm.BebidaEmbalagem) : null;
            produto.BebidaQtdPorEmbalagem = IsBebida(vm.TipoProduto) ? NormalizarQtdPorEmbalagem(vm) : null;
            produto.BebidaTeorAlcoolico = null;

            produto.PodPuffs = IsPod(vm.TipoProduto) ? vm.PodPuffs : null;
            produto.PodCapacidadeBateria = IsPod(vm.TipoProduto) ? LimparTextoOuNull(vm.PodCapacidadeBateria) : null;
            produto.PodTipo = IsPod(vm.TipoProduto) ? LimparTextoOuNull(vm.PodTipo) : null;

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

        // ============================================================
        // EXCLUIR / DESATIVAR
        // ============================================================

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public async Task<IActionResult> Excluir(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var produto = await _context.Produtos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id && p.LojaId == lojaId);

            if (produto == null)
            {
                FlashErr("Produto não encontrado.");
                return RedirectToAction(nameof(Index));
            }

            return View(produto);
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarExcluir(int id)
        {
            return await DesativarProdutoAsync(id);
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Excluir")]
        public async Task<IActionResult> ExcluirPost(int id)
        {
            return await DesativarProdutoAsync(id);
        }

        private async Task<IActionResult> DesativarProdutoAsync(int id)
        {
            try
            {
                var lojaId = GetLojaIdOrFail();

                var produto = await _context.Produtos
                    .FirstOrDefaultAsync(p => p.Id == id && p.LojaId == lojaId);

                if (produto == null)
                {
                    FlashErr("Produto não encontrado.");
                    return RedirectToAction(nameof(Index));
                }

                if (!produto.Ativo)
                {
                    FlashOk("Produto já estava desativado.");
                    return RedirectToAction(nameof(Index));
                }

                produto.Ativo = false;
                await _context.SaveChangesAsync();

                FlashOk("Produto excluído com sucesso!");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                FlashErr($"Erro ao excluir produto: {ex.Message}");
                return RedirectToAction(nameof(Index));
            }
        }

        // ============================================================
        // PREPARAÇÃO / VALIDAÇÃO FORMULÁRIO
        // ============================================================

        private void PrepararVmAntesValidacao(
            ProdutoFormSimplesViewModel vm,
            string? saborSelect,
            string? saborOutro)
        {
            vm.Nome = LimparTexto(vm.Nome);
            vm.Descricao = LimparTextoOuNull(vm.Descricao);
            vm.Marca = LimparTextoOuNull(vm.Marca);

            vm.SKU = null;
            vm.CodigoBarras = null;
            vm.BebidaTeorAlcoolico = null;
            vm.MaisVendido = false;
            vm.Cor = "";

            if (IsPod(vm.TipoProduto))
            {
                vm.RequerMaioridade = true;

                var saborFinal = !string.IsNullOrWhiteSpace(saborOutro)
                    ? saborOutro.Trim()
                    : !string.IsNullOrWhiteSpace(saborSelect)
                        ? saborSelect.Trim()
                        : (vm.Sabor ?? "").Trim();

                vm.Sabor = saborFinal;

                vm.BebidaVolumeMl = null;
                vm.BebidaTipo = null;
                vm.BebidaEmbalagem = BebidaEmbalagemTipo.NaoInformado;
                vm.BebidaQtdPorEmbalagem = null;
            }
            else if (IsBebida(vm.TipoProduto))
            {
                vm.RequerMaioridade = true;
                vm.Sabor = "";
                vm.Cor = "";

                vm.PodPuffs = null;
                vm.PodCapacidadeBateria = null;
                vm.PodTipo = null;

                vm.BebidaEmbalagem = NormalizarEmbalagem(vm.BebidaEmbalagem);
                vm.BebidaQtdPorEmbalagem = NormalizarQtdPorEmbalagem(vm);
            }
            else
            {
                vm.TipoProduto = ProdutoTipo.Padrao;
                vm.RequerMaioridade = false;
                vm.Sabor = "";
                vm.Cor = "";

                vm.BebidaVolumeMl = null;
                vm.BebidaTipo = null;
                vm.BebidaEmbalagem = BebidaEmbalagemTipo.NaoInformado;
                vm.BebidaQtdPorEmbalagem = null;
                vm.BebidaTeorAlcoolico = null;

                vm.PodPuffs = null;
                vm.PodCapacidadeBateria = null;
                vm.PodTipo = null;
            }

            vm.Estoque = Math.Max(0, vm.Estoque);
        }

        private void NormalizarModelStateFormulario(ProdutoFormSimplesViewModel vm)
        {
            ModelState.Remove(nameof(vm.SKU));
            ModelState.Remove(nameof(vm.CodigoBarras));
            ModelState.Remove(nameof(vm.BebidaTeorAlcoolico));
            ModelState.Remove(nameof(vm.MaisVendido));
            ModelState.Remove(nameof(vm.RequerMaioridade));
            ModelState.Remove(nameof(vm.Cor));

            if (!IsPod(vm.TipoProduto))
                ModelState.Remove(nameof(vm.Sabor));

            if (!IsBebida(vm.TipoProduto))
            {
                ModelState.Remove(nameof(vm.BebidaVolumeMl));
                ModelState.Remove(nameof(vm.BebidaTipo));
                ModelState.Remove(nameof(vm.BebidaEmbalagem));
                ModelState.Remove(nameof(vm.BebidaQtdPorEmbalagem));
                ModelState.Remove(nameof(vm.BebidaTeorAlcoolico));
            }

            if (!IsPod(vm.TipoProduto))
            {
                ModelState.Remove(nameof(vm.PodPuffs));
                ModelState.Remove(nameof(vm.PodCapacidadeBateria));
                ModelState.Remove(nameof(vm.PodTipo));
            }

            if (Request.Form["Preco"].ToString().Contains(",") ||
                Request.Form["PrecoPromocional"].ToString().Contains(","))
            {
                ModelState.Remove(nameof(vm.Preco));
                ModelState.Remove(nameof(vm.PrecoPromocional));
            }
        }

        private void ValidarFormularioProduto(ProdutoFormSimplesViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Nome))
                ModelState.AddModelError(nameof(vm.Nome), "Informe o nome do produto.");

            if (vm.CategoriaId <= 0)
                ModelState.AddModelError(nameof(vm.CategoriaId), "Selecione uma categoria.");

            if (vm.Preco <= 0)
                ModelState.AddModelError(nameof(vm.Preco), "Informe um preço maior que zero.");

            if (vm.PrecoPromocional.HasValue &&
                vm.PrecoPromocional.Value > 0 &&
                vm.PrecoPromocional.Value >= vm.Preco)
            {
                ModelState.AddModelError(nameof(vm.PrecoPromocional), "O preço promocional deve ser menor que o preço normal.");
            }

            if (IsPod(vm.TipoProduto))
            {
                if (string.IsNullOrWhiteSpace(vm.Sabor))
                    ModelState.AddModelError(nameof(vm.Sabor), "Informe o sabor do POD.");
            }

            if (IsBebida(vm.TipoProduto))
            {
                var embalagem = NormalizarEmbalagem(vm.BebidaEmbalagem);

                if (IsEmbalagemComposta(embalagem))
                {
                    if (!vm.BebidaQtdPorEmbalagem.HasValue || vm.BebidaQtdPorEmbalagem.Value <= 1)
                    {
                        ModelState.AddModelError(nameof(vm.BebidaQtdPorEmbalagem), "Informe quantas unidades vêm na embalagem. Ex.: 6, 12 ou 24.");
                    }
                }

                if (vm.BebidaVolumeMl.HasValue && vm.BebidaVolumeMl.Value <= 0)
                    ModelState.AddModelError(nameof(vm.BebidaVolumeMl), "O volume deve ser maior que zero.");
            }
        }

        private void CarregarCombosFormulario(ProdutoFormSimplesViewModel vm)
        {
            CarregarCategoriasEnum();

            if (IsPod(vm.TipoProduto))
                CarregarSaboresPod(vm.Sabor);
            else
                ViewBag.Sabores = new List<SelectListItem>();
        }

        private static BebidaEmbalagemTipo NormalizarEmbalagem(BebidaEmbalagemTipo embalagem)
        {
            return embalagem;
        }

        private static int? NormalizarQtdPorEmbalagem(ProdutoFormSimplesViewModel vm)
        {
            if (!IsBebida(vm.TipoProduto))
                return null;

            var embalagem = NormalizarEmbalagem(vm.BebidaEmbalagem);

            if (IsEmbalagemComposta(embalagem))
                return vm.BebidaQtdPorEmbalagem.HasValue && vm.BebidaQtdPorEmbalagem.Value > 1
                    ? vm.BebidaQtdPorEmbalagem.Value
                    : null;

            return 1;
        }

        private static decimal? ObterPrecoPromocionalValido(ProdutoFormSimplesViewModel vm)
        {
            return vm.PrecoPromocional.HasValue && vm.PrecoPromocional.Value > 0
                ? vm.PrecoPromocional.Value
                : null;
        }

        private static bool TemPromocaoValida(ProdutoFormSimplesViewModel vm)
        {
            return vm.PrecoPromocional.HasValue &&
                   vm.PrecoPromocional.Value > 0 &&
                   vm.PrecoPromocional.Value < vm.Preco;
        }

        private static string LimparTexto(string? texto)
        {
            return (texto ?? string.Empty).Trim();
        }

        private static string? LimparTextoOuNull(string? texto)
        {
            var limpo = (texto ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(limpo) ? null : limpo;
        }

        // ============================================================
        // CATEGORIAS / SABORES
        // ============================================================

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

        private List<SelectListItem> MesclarSabores(
            List<SelectListItem> baseSabores,
            IEnumerable<string> saboresDoProduto)
        {
            var set = new HashSet<string>(
                baseSabores.Select(s => s.Value),
                StringComparer.OrdinalIgnoreCase);

            var result = new List<SelectListItem>(baseSabores);

            foreach (var sabor in saboresDoProduto.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!set.Contains(sabor))
                {
                    result.Add(new SelectListItem
                    {
                        Value = sabor,
                        Text = sabor + " (do produto)"
                    });

                    set.Add(sabor);
                }
            }

            return result
                .OrderBy(s => s.Text.Replace(" (do produto)", ""), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ============================================================
        // IMAGEM
        // ============================================================

        private static string? ValidateImage(IFormFile file, out string extLower)
        {
            extLower = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            if (!allowed.Contains(extLower))
                return "Apenas arquivos JPG, JPEG, PNG e WEBP são permitidos.";

            if (file.Length > 2 * 1024 * 1024)
                return "O tamanho da imagem não pode exceder 2MB.";

            if (file.Length == 0)
                return "A imagem enviada está vazia.";

            if (!HasValidImageSignature(file, extLower))
                return "O arquivo enviado não parece ser uma imagem válida.";

            return null;
        }

        private async Task<string> SaveImageAndReturnUrl(IFormFile file, string? productName)
        {
            var extLower = Path.GetExtension(file.FileName).ToLowerInvariant();
            var pastaUploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");

            Directory.CreateDirectory(pastaUploads);

            var fileName = MakeShortFileName(productName, extLower);
            var caminho = Path.Combine(pastaUploads, fileName);
            var tempPath = Path.Combine(pastaUploads, $"{Guid.NewGuid():N}.tmp");

            await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fs);
            }

            System.IO.File.Move(tempPath, caminho, overwrite: false);

            return $"/imagens/produtos/{fileName}";
        }

        private static bool HasValidImageSignature(IFormFile file, string extLower)
        {
            Span<byte> header = stackalloc byte[12];

            using var stream = file.OpenReadStream();
            var read = stream.Read(header);

            return extLower switch
            {
                ".jpg" or ".jpeg" => read >= 3 &&
                                      header[0] == 0xFF &&
                                      header[1] == 0xD8 &&
                                      header[2] == 0xFF,

                ".png" => read >= 8 &&
                          header[0] == 0x89 &&
                          header[1] == 0x50 &&
                          header[2] == 0x4E &&
                          header[3] == 0x47 &&
                          header[4] == 0x0D &&
                          header[5] == 0x0A &&
                          header[6] == 0x1A &&
                          header[7] == 0x0A,

                ".webp" => read >= 12 &&
                           header[0] == 0x52 &&
                           header[1] == 0x49 &&
                           header[2] == 0x46 &&
                           header[3] == 0x46 &&
                           header[8] == 0x57 &&
                           header[9] == 0x45 &&
                           header[10] == 0x42 &&
                           header[11] == 0x50,

                _ => false
            };
        }

        private static string MakeShortFileName(string? productName, string extLower)
        {
            var slug = Slugify(productName ?? "produto");

            if (slug.Length > 32)
                slug = slug[..32];

            var guid8 = Guid.NewGuid().ToString("N")[..8];

            return $"{slug}-{guid8}{extLower}";
        }

        private static string Slugify(string s)
        {
            var slug = Regex.Replace(s ?? "", "[^a-zA-Z0-9]+", "-").Trim('-');
            return slug.ToLowerInvariant();
        }

        // ============================================================
        // DINHEIRO BR
        // ============================================================

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
            if (string.IsNullOrWhiteSpace(s))
                return null;

            var d = ParseDecimalBR(s);
            return d <= 0 ? null : d;
        }

        private static decimal ParseDecimalBR(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return 0m;

            s = s.Trim();
            s = Regex.Replace(s, @"[^\d\.,\-]", "");

            if (s.Contains(",") && s.Contains("."))
                s = s.Replace(".", "").Replace(",", ".");
            else
                s = s.Replace(",", ".");

            return decimal.TryParse(
                s,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var d)
                ? d
                : 0m;
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