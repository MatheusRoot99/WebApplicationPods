using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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

            // ✅ sabores JSON -> lista (para cards/Detalhes)
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
                .Include(p => p.Variacoes)
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
                TipoProduto = ProdutoTipo.Padrao,
                Variacoes = new List<ProdutoFormViewModel.ProdutoVariacaoFormRow>
                {
                    new()
                    {
                        Nome = "Unidade",
                        Multiplicador = 1,
                        PrecoTexto = "0,00",
                        PrecoPromocionalTexto = "",
                        Estoque = 0,
                        Ativo = true
                    }
                },
                Sabores = new List<ProdutoFormViewModel.SaborRow>
                {
                    new()
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

            // Tipo define se maioridade é obrigatória
            if (IsPod(vm.TipoProduto) || IsBebida(vm.TipoProduto))
            {
                vm.RequerMaioridade = true;
            }

            // ✅ valida / normaliza variações conforme tipo
            NormalizeVmByTipo(vm);

            ValidateVm(vm);

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

                TipoProduto = vm.TipoProduto,

                RequerMaioridade = vm.RequerMaioridade,
                Ativo = vm.Ativo,
                MaisVendido = vm.MaisVendido,
                EmPromocao = vm.EmPromocao,
                DataCadastro = DateTime.Now
            };

            // ✅ POD extras (NOVO)
            if (IsPod(vm.TipoProduto))
            {
                produto.PodPuffs = vm.PodPuffs;
                produto.PodCapacidadeBateria = vm.PodCapacidadeBateria?.Trim();
                produto.PodTipo = vm.PodTipo?.Trim();

                // (opcional) mantém legado sincronizado
                produto.Puffs = vm.PodPuffs ?? 0;
                if (!string.IsNullOrWhiteSpace(vm.PodCapacidadeBateria))
                {
                    var digits = new string(vm.PodCapacidadeBateria.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out var mah))
                        produto.CapacidadeBateria = mah;
                }
            }

            // ✅ SABORES
            // - POD: não usa lista Sabores, usa Variacoes (Nome=sabor) => salva JSON com Quantidade = Estoque
            // - PADRÃO/BEBIDA: usa lista Sabores (somente nomes) => salva JSON com Quantidade = 0
            ApplySaboresFromVm(vm, produto);

            // ✅ Imagem (upload)
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

            // ✅ Variações
            foreach (var v in vm.Variacoes)
            {
                var preco = ParseDecimalBR(v.PrecoTexto);
                var promo = string.IsNullOrWhiteSpace(v.PrecoPromocionalTexto)
                    ? (decimal?)null
                    : ParseDecimalBR(v.PrecoPromocionalTexto);

                produto.Variacoes.Add(new ProdutoVariacaoModel
                {
                    Nome = v.Nome.Trim(),
                    Multiplicador = v.Multiplicador <= 0 ? 1 : v.Multiplicador,
                    Preco = preco,
                    PrecoPromocional = (promo.HasValue && promo.Value > 0) ? promo : null,
                    Estoque = v.Estoque < 0 ? 0 : v.Estoque,
                    SKU = v.SKU,
                    CodigoBarras = v.CodigoBarras,
                    Ativo = v.Ativo
                });
            }

            // ✅ Preço/Promo no Produto = menor das variações ativas
            ApplyPrecoPromoFromVariacoes(produto);

            // ✅ Estoque total
            produto.Estoque = CalcEstoqueTotal(produto);

            _context.Produtos.Add(produto);
            await _context.SaveChangesAsync();

            FlashOk("Produto cadastrado!");
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpGet]
        public IActionResult Editar(int id)
        {
            var lojaId = GetLojaIdOrFail();

            var produto = _context.Produtos
                .Include(p => p.Variacoes)
                .FirstOrDefault(p => p.Id == id && p.LojaId == lojaId && p.Ativo);

            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }

            // ✅ Sabores do banco -> VM + mescla no dropdown (para POD select)
            produto.DeserializarSaboresQuantidades();
            var saboresDoProduto = (produto.SaboresQuantidadesList ?? new List<ProdutoModel.SaborQuantidade>())
                .Select(s => s.Sabor);

            CarregarCategorias(saboresDoProduto);

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

                TipoProduto = produto.TipoProduto,

                // POD extras
                PodPuffs = produto.PodPuffs,
                PodCapacidadeBateria = produto.PodCapacidadeBateria,
                PodTipo = produto.PodTipo,

                Variacoes = produto.Variacoes
                    .OrderBy(v => v.Id)
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
                    .ToList(),

                // PADRÃO/BEBIDA: sabores somente nome
                Sabores = (produto.SaboresQuantidadesList ?? new List<ProdutoModel.SaborQuantidade>())
                    .Select(s => new ProdutoFormViewModel.SaborRow { Sabor = s.Sabor })
                    .ToList()
            };

            if (vm.Variacoes.Count == 0)
            {
                vm.Variacoes.Add(new()
                {
                    Nome = IsPod(vm.TipoProduto) ? "" : "Unidade",
                    Multiplicador = 1,
                    PrecoTexto = "0,00",
                    PrecoPromocionalTexto = "",
                    Estoque = 0,
                    Ativo = true
                });
            }

            if (vm.Sabores == null) vm.Sabores = new();
            if (vm.Sabores.Count == 0) vm.Sabores.Add(new ProdutoFormViewModel.SaborRow());

            return View(vm);
        }

        [Authorize(Roles = "Lojista,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(ProdutoFormViewModel vm)
        {
            if (vm.Id == null) return BadRequest();

            var lojaId = GetLojaIdOrFail();

            var produto = _context.Produtos
                .Include(p => p.Variacoes)
                .FirstOrDefault(p => p.Id == vm.Id.Value && p.LojaId == lojaId);

            if (produto == null)
            {
                FlashErr("Produto não encontrado");
                return RedirectToAction(nameof(Index));
            }

            // ✅ TRAVA TIPO NO EDITAR (mantém o do banco)
            vm.TipoProduto = produto.TipoProduto;

            // ✅ carrega categorias + mescla sabores do produto pro dropdown
            produto.DeserializarSaboresQuantidades();
            var saboresDoProduto = (produto.SaboresQuantidadesList ?? new List<ProdutoModel.SaborQuantidade>())
                .Select(s => s.Sabor);
            CarregarCategorias(saboresDoProduto);

            // Tipo define se maioridade é obrigatória
            if (IsPod(vm.TipoProduto) || IsBebida(vm.TipoProduto))
            {
                vm.RequerMaioridade = true;
            }

            NormalizeVmByTipo(vm);
            ValidateVm(vm);

            if (!ModelState.IsValid)
                return View(vm);

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

            // ✅ POD extras (NOVO)
            if (IsPod(vm.TipoProduto))
            {
                produto.PodPuffs = vm.PodPuffs;
                produto.PodCapacidadeBateria = vm.PodCapacidadeBateria?.Trim();
                produto.PodTipo = vm.PodTipo?.Trim();

                // (opcional) mantém legado sincronizado
                produto.Puffs = vm.PodPuffs ?? 0;
                if (!string.IsNullOrWhiteSpace(vm.PodCapacidadeBateria))
                {
                    var digits = new string(vm.PodCapacidadeBateria.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out var mah))
                        produto.CapacidadeBateria = mah;
                }
            }
            else
            {
                // se não é POD, pode limpar os extras (opcional)
                produto.PodPuffs = null;
                produto.PodCapacidadeBateria = null;
                produto.PodTipo = null;
            }

            // ✅ SABORES (JSON)
            ApplySaboresFromVm(vm, produto);

            // ✅ Imagem (upload)
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

            // ✅ remove variações que saíram do form
            var idsNoForm = vm.Variacoes
                .Where(x => x.Id.HasValue && x.Id.Value > 0)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var paraRemover = produto.Variacoes.Where(v => !idsNoForm.Contains(v.Id)).ToList();
            foreach (var r in paraRemover)
                _context.Set<ProdutoVariacaoModel>().Remove(r);

            // ✅ upsert variações
            foreach (var row in vm.Variacoes)
            {
                var preco = ParseDecimalBR(row.PrecoTexto);
                var promo = string.IsNullOrWhiteSpace(row.PrecoPromocionalTexto)
                    ? (decimal?)null
                    : ParseDecimalBR(row.PrecoPromocionalTexto);

                if (row.Id.HasValue && row.Id.Value > 0)
                {
                    var ent = produto.Variacoes.First(v => v.Id == row.Id.Value);
                    ent.Nome = row.Nome.Trim();
                    ent.Multiplicador = row.Multiplicador <= 0 ? 1 : row.Multiplicador;
                    ent.Preco = preco;
                    ent.PrecoPromocional = (promo.HasValue && promo.Value > 0) ? promo : null;
                    ent.Estoque = row.Estoque < 0 ? 0 : row.Estoque;
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
                        Estoque = row.Estoque < 0 ? 0 : row.Estoque,
                        SKU = row.SKU,
                        CodigoBarras = row.CodigoBarras,
                        Ativo = row.Ativo
                    });
                }
            }

            // ✅ Preço/Promo no Produto = menor das variações ativas
            ApplyPrecoPromoFromVariacoes(produto);

            // ✅ estoque total
            produto.Estoque = CalcEstoqueTotal(produto);

            await _context.SaveChangesAsync();

            FlashOk("Produto atualizado!");
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
                .Include(p => p.Variacoes)
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
                    .Include(p => p.Variacoes)
                    .FirstOrDefaultAsync(p => p.Id == id && p.LojaId == lojaId);

                if (produto == null)
                {
                    FlashErr("Produto não encontrado");
                    return RedirectToAction(nameof(Index));
                }

                // ✅ Soft delete
                produto.Ativo = false;

                if (produto.Variacoes != null)
                    foreach (var v in produto.Variacoes)
                        v.Ativo = false;

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

        private void CarregarCategorias(IEnumerable<string>? saboresDoProduto = null)
        {
            var categorias = _categoriaRepository.ObterTodos()
                .OrderBy(c => c.Nome)
                .ToList();

            ViewBag.Categorias = new SelectList(categorias, "Id", "Nome");

            // ✅ sabores fixos base para POD/VAPE
            var baseSabores = ObterTodosSabores();

            // ✅ no Editar: mescla sabores do produto pra não "sumir" no dropdown
            ViewBag.SaboresPod = saboresDoProduto != null
                ? MesclarSabores(baseSabores, saboresDoProduto)
                : baseSabores;
        }

        // ✅ Centraliza salvar sabores do VM no ProdutoModel (JSON)
        private static void ApplySaboresFromVm(ProdutoFormViewModel vm, ProdutoModel produto)
        {
            if (IsPod(vm.TipoProduto))
            {
                // POD: sabores vêm das VARIAÇÕES (Nome = Sabor), Quantidade = Estoque da linha
                var sabores = (vm.Variacoes ?? new List<ProdutoFormViewModel.ProdutoVariacaoFormRow>())
                    .Where(v => !string.IsNullOrWhiteSpace(v.Nome))
                    .Select(v => new ProdutoModel.SaborQuantidade
                    {
                        Sabor = v.Nome.Trim(),
                        Quantidade = v.Estoque < 0 ? 0 : v.Estoque
                    })
                    .GroupBy(s => s.Sabor, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ProdutoModel.SaborQuantidade
                    {
                        Sabor = g.First().Sabor,
                        Quantidade = g.Sum(x => x.Quantidade)
                    })
                    .ToList();

                produto.SaboresQuantidadesList = sabores;
                produto.SerializarSaboresQuantidades();
                return;
            }

            // PADRÃO/BEBIDA: sabores só nome, Quantidade = 0
            var sabores2 = (vm.Sabores ?? new List<ProdutoFormViewModel.SaborRow>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Sabor))
                .Select(s => new ProdutoModel.SaborQuantidade
                {
                    Sabor = s.Sabor.Trim(),
                    Quantidade = 0
                })
                .GroupBy(s => s.Sabor, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ProdutoModel.SaborQuantidade
                {
                    Sabor = g.First().Sabor,
                    Quantidade = 0
                })
                .ToList();

            produto.SaboresQuantidadesList = sabores2;
            produto.SerializarSaboresQuantidades();
        }

        private static void NormalizeVmByTipo(ProdutoFormViewModel vm)
        {
            vm.Variacoes ??= new();
            vm.Sabores ??= new();

            if (IsPod(vm.TipoProduto))
            {
                // POD: Variações = linhas de sabor. Multiplicador fixo 1.
                vm.Variacoes = vm.Variacoes
                    .Select(v => new ProdutoFormViewModel.ProdutoVariacaoFormRow
                    {
                        Id = v.Id,
                        Nome = (v.Nome ?? "").Trim(),
                        Multiplicador = 1,
                        PrecoTexto = v.PrecoTexto ?? "",
                        PrecoPromocionalTexto = v.PrecoPromocionalTexto,
                        Estoque = v.Estoque < 0 ? 0 : v.Estoque,
                        SKU = v.SKU,
                        CodigoBarras = v.CodigoBarras,
                        Ativo = v.Ativo
                    })
                    .Where(v => !string.IsNullOrWhiteSpace(v.Nome) || !string.IsNullOrWhiteSpace(v.PrecoTexto))
                    .ToList();

                // não usa mais sabores “antigos”
                vm.Sabores = new List<ProdutoFormViewModel.SaborRow>();
            }
            else
            {
                // PADRÃO/BEBIDA: variações normais
                vm.Variacoes = vm.Variacoes
                    .Where(v => !string.IsNullOrWhiteSpace(v.Nome))
                    .ToList();

                // sabores (somente texto)
                vm.Sabores = vm.Sabores
                    .Where(s => !string.IsNullOrWhiteSpace(s.Sabor))
                    .Select(s => new ProdutoFormViewModel.SaborRow { Sabor = s.Sabor.Trim() })
                    .ToList();

                // garante pelo menos 1 linha de sabor para UI
                if (vm.Sabores.Count == 0) vm.Sabores.Add(new ProdutoFormViewModel.SaborRow());
            }

            // garante pelo menos 1 variação para UI (se veio tudo vazio)
            if (vm.Variacoes.Count == 0)
            {
                vm.Variacoes.Add(new ProdutoFormViewModel.ProdutoVariacaoFormRow
                {
                    Nome = IsPod(vm.TipoProduto) ? "" : "Unidade",
                    Multiplicador = IsPod(vm.TipoProduto) ? 1 : 1,
                    PrecoTexto = "0,00",
                    PrecoPromocionalTexto = "",
                    Estoque = 0,
                    Ativo = true
                });
            }
        }

        private void ValidateVm(ProdutoFormViewModel vm)
        {
            if (vm.Variacoes == null || vm.Variacoes.Count == 0)
                ModelState.AddModelError("", "Adicione pelo menos 1 variação.");

            foreach (var v in vm.Variacoes ?? new())
            {
                // nome é obrigatório (no POD = sabor)
                if (string.IsNullOrWhiteSpace(v.Nome))
                    ModelState.AddModelError("", "Informe o nome/sabor da linha.");

                var preco = ParseDecimalBR(v.PrecoTexto);
                var promo = string.IsNullOrWhiteSpace(v.PrecoPromocionalTexto)
                    ? (decimal?)null
                    : ParseDecimalBR(v.PrecoPromocionalTexto);

                if (preco <= 0)
                    ModelState.AddModelError("", $"Preço inválido na variação: {v.Nome}");

                if (promo.HasValue && promo.Value > 0 && promo.Value >= preco)
                    ModelState.AddModelError("", $"Promo deve ser menor que o preço em: {v.Nome}");

                if (IsPod(vm.TipoProduto) && v.Multiplicador != 1)
                    v.Multiplicador = 1;
            }

            // POD extras (opcional, mas evita lixo)
            if (IsPod(vm.TipoProduto))
            {
                if (vm.PodPuffs.HasValue && vm.PodPuffs.Value < 0)
                    ModelState.AddModelError(nameof(vm.PodPuffs), "Puffs inválido.");

                if (!string.IsNullOrWhiteSpace(vm.PodCapacidadeBateria) && vm.PodCapacidadeBateria.Length > 40)
                    ModelState.AddModelError(nameof(vm.PodCapacidadeBateria), "Bateria inválida.");

                if (!string.IsNullOrWhiteSpace(vm.PodTipo) && vm.PodTipo.Length > 40)
                    ModelState.AddModelError(nameof(vm.PodTipo), "Tipo inválido.");
            }
        }

        private static int CalcEstoqueTotal(ProdutoModel produto)
        {
            var varsAtivas = produto.Variacoes.Where(v => v.Ativo).ToList();

            if (IsPod(produto.TipoProduto))
            {
                // POD: cada linha é sabor, estoque é direto
                return varsAtivas.Sum(v => v.Estoque);
            }

            // PADRÃO/BEBIDA: estoque = estoque * mult
            return varsAtivas.Sum(v => v.Estoque * v.Multiplicador);
        }

        private static void ApplyPrecoPromoFromVariacoes(ProdutoModel produto)
        {
            var varsAtivas = produto.Variacoes.Where(v => v.Ativo).ToList();
            if (varsAtivas.Count == 0)
            {
                produto.Preco = produto.Preco <= 0 ? 0.01m : produto.Preco;
                produto.PrecoPromocional = null;
                produto.EmPromocao = false;
                return;
            }

            produto.Preco = varsAtivas.Min(v => v.Preco);

            var promosValidas = varsAtivas
                .Where(v => v.PrecoPromocional.HasValue && v.PrecoPromocional.Value > 0 && v.PrecoPromocional.Value < v.Preco)
                .Select(v => v.PrecoPromocional!.Value)
                .ToList();

            produto.PrecoPromocional = promosValidas.Count > 0 ? promosValidas.Min() : null;
            produto.EmPromocao = produto.PrecoPromocional.HasValue;
        }

        private static List<SelectListItem> ObterTodosSabores()
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

        private static List<SelectListItem> MesclarSabores(List<SelectListItem> baseSabores, IEnumerable<string> saboresDoProduto)
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
