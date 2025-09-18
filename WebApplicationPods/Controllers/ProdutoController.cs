using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Globalization;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Repository.Repository;


namespace WebApplicationPods.Controllers
{
    public class ProdutoController : Controller
    {
        private readonly IProdutoRepository _produtoRepository;
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly BancoContext _context;

        public ProdutoController(
            IProdutoRepository produtoRepository,
            ICategoriaRepository categoriaRepository,
            IWebHostEnvironment hostEnvironment,
            BancoContext context)
        {
            _produtoRepository = produtoRepository;
            _categoriaRepository = categoriaRepository;
            _hostEnvironment = hostEnvironment;
            _context = context;
        }

        public async Task<IActionResult> Index(
    string? q,                // busca por nome/descrição
    int? categoriaId,         // filtro por categoria
    bool? emPromocao,         // filtrar se está em promoção
    string? sort = "nome",    // nome|preco|preco_desc|promo|novidades
    int page = 1,             // página atual
    int pageSize = 12)        // itens por página
        {
            // Sanitize
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 60 ? 12 : pageSize;

            var query = _context.Produtos.AsNoTracking().Where(p => p.Ativo);

            // Busca (q) — nome/descrição
            if (!string.IsNullOrWhiteSpace(q))
            {
                var termo = q.Trim();
                query = query.Where(p =>
                    p.Nome.Contains(termo) ||
                    (p.Descricao != null && p.Descricao.Contains(termo)));
            }

            // Filtro por categoria
            if (categoriaId.HasValue)
                query = query.Where(p => p.CategoriaId == categoriaId.Value);

            // Em promoção
            if (emPromocao.HasValue)
            {
                if (emPromocao.Value)
                    query = query.Where(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco);
                else
                    query = query.Where(p => !p.PrecoPromocional.HasValue || p.PrecoPromocional >= p.Preco);
            }

            // Ordenação
            query = sort switch
            {
                "preco" => query.OrderBy(p => p.Preco),
                "preco_desc" => query.OrderByDescending(p => p.Preco),
                "promo" => query
                                   .OrderByDescending(p => p.PrecoPromocional.HasValue && p.PrecoPromocional < p.Preco)
                                   .ThenBy(p => p.Nome),
                "novidades" => query.OrderByDescending(p => p.Id), // ou DataCriacao, se existir
                _ => query.OrderBy(p => p.Nome),
            };

            // Total p/ paginação
            var total = await query.CountAsync();

            // Página atual
            var itens = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Desserializa sabores apenas do que foi trazido
            foreach (var p in itens)
                p.DeserializarSaboresQuantidades();

            // ViewBag para UI (pode trocar por um ViewModel de paginação)
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

        public IActionResult Detalhes(int id)
        {
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                TempData["MensagemErro"] = "Produto não encontrado";
                return RedirectToAction(nameof(Index));
            }
            return View(produto);
        }

        [HttpGet]
        public IActionResult Criar()
        {
            try
            {
                // Carrega as categorias
                var categorias = _categoriaRepository.ObterTodos()
                    .OrderBy(c => c.Nome)
                    .ToList();

                ViewBag.Categorias = new SelectList(categorias, "Id", "Nome");

                // Cria o modelo com a lista de sabores disponíveis
                var model = new ProdutoModel
                {
                    TodosSabores = ObterTodosSabores()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = "Erro ao carregar o formulário de cadastro";
                return RedirectToAction(nameof(Index));
            }
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
        new SelectListItem { Value = "Watermelon Ice - Melancia Ice", Text = "Watermelon Ice - Melancia Ice" }};
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(ProdutoModel produto)
        {
            try
            {
                // 1. Processar Sabores e Quantidades a partir do form
                var saboresList = new List<ProdutoModel.SaborQuantidade>();
                produto.SaboresQuantidadesList = saboresList;

                if (Request.Form.TryGetValue("SaboresQuantidadesList", out var valoresForm))
                {
                    foreach (var item in valoresForm)
                    {
                        if (!string.IsNullOrWhiteSpace(item))
                        {
                            try
                            {
                                var saborQuantidade = JsonConvert.DeserializeObject<ProdutoModel.SaborQuantidade>(item);
                                if (saborQuantidade != null &&
                                    !string.IsNullOrWhiteSpace(saborQuantidade.Sabor) &&
                                    saborQuantidade.Quantidade > 0)
                                {
                                    saboresList.Add(saborQuantidade);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao desserializar sabor: {ex.Message}");
                                ModelState.AddModelError("", "Houve um erro ao processar os sabores.");
                            }
                        }
                    }
                }

                // 2. Validação de sabores
                if (saboresList.Count == 0)
                {
                    ModelState.AddModelError("", "Adicione pelo menos um sabor com quantidade válida.");
                }

                // 3. Atualizar estoque total com base nas quantidades
                produto.Estoque = saboresList.Sum(s => s.Quantidade);

                // 4. Remover validações de propriedades que não serão validadas diretamente
                ModelState.Remove("Categoria");
                ModelState.Remove("ImagemUrl");
                ModelState.Remove("SaboresQuantidadesList");
                ModelState.Remove("SaboresQuantidades");

                // 5. Serializar sabores para armazenar no banco
                produto.SerializarSaboresQuantidades();

                // 6. Verificar se o ModelState está válido
                if (ModelState.IsValid)
                {
                    // 6.1. Processar imagem se enviada
                    if (produto.ImagemUpload != null && produto.ImagemUpload.Length > 0)
                    {
                        var pastaUploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                        if (!Directory.Exists(pastaUploads))
                        {
                            Directory.CreateDirectory(pastaUploads);
                        }

                        var nomeArquivo = $"{Guid.NewGuid()}_{Path.GetFileName(produto.ImagemUpload.FileName)}";
                        var caminhoArquivo = Path.Combine(pastaUploads, nomeArquivo);

                        using (var fileStream = new FileStream(caminhoArquivo, FileMode.Create))
                        {
                            await produto.ImagemUpload.CopyToAsync(fileStream);
                        }

                        produto.ImagemUrl = $"/imagens/produtos/{nomeArquivo}";
                    }

                    // 6.2. Salvar no banco
                    _produtoRepository.Adicionar(produto);

                    TempData["MensagemSucesso"] = "Produto cadastrado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no cadastro: {ex}");
                TempData["MensagemErro"] = $"Erro ao cadastrar produto: {ex.Message}";
            }

            // 7. Se chegamos aqui, houve erro → recarrega categorias e sabores
            produto.TodosSabores = ObterTodosSabores();
            CarregarCategorias();

            return View(produto);
        }




        [HttpGet]
        public IActionResult Editar(int id)
        {
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                TempData["MensagemErro"] = "Produto não encontrado";
                return RedirectToAction(nameof(Index));
            }

            // carrega listas auxiliares
            produto.DeserializarSaboresQuantidades();
            produto.TodosSabores = ObterTodosSabores();
            CarregarCategorias();

            return View(produto);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Editar")]
        public async Task<IActionResult> EditarPost(int id)
        {
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                TempData["MensagemErro"] = "Produto não encontrado";
                return RedirectToAction(nameof(Index));
            }

            // ---- Conversão de preços pt-BR
            static decimal? ParsePtBr(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return null;
                if (decimal.TryParse(raw, NumberStyles.Currency, new CultureInfo("pt-BR"), out var d))
                    return d;
                raw = raw.Replace(".", "").Replace(",", ".");
                return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : null;
            }

            var precoRaw = Request.Form[nameof(ProdutoModel.Preco)];
            var promoRaw = Request.Form[nameof(ProdutoModel.PrecoPromocional)];

            var preco = ParsePtBr(precoRaw);
            if (preco is null || preco <= 0)
                ModelState.AddModelError(nameof(ProdutoModel.Preco), "Preço inválido. Ex.: 179,90");
            else
            {
                produto.Preco = preco.Value;
                ModelState.Remove(nameof(ProdutoModel.Preco));
            }

            if (string.IsNullOrWhiteSpace(promoRaw))
            {
                produto.PrecoPromocional = null;
                ModelState.Remove(nameof(ProdutoModel.PrecoPromocional));
            }
            else
            {
                var promo = ParsePtBr(promoRaw);
                if (promo is null || promo <= 0)
                    ModelState.AddModelError(nameof(ProdutoModel.PrecoPromocional), "Preço promocional inválido. Ex.: 169,90");
                else
                {
                    produto.PrecoPromocional = promo;
                    ModelState.Remove(nameof(ProdutoModel.PrecoPromocional));
                }
            }

            // ---- Bind restante
            var ok = await TryUpdateModelAsync(produto, prefix: "",
                p => p.Nome, p => p.Descricao, p => p.CategoriaId, p => p.Ativo,
                p => p.EmPromocao, p => p.MaisVendido, p => p.Sabor, p => p.Cor,
                p => p.Puffs, p => p.CapacidadeBateria);

            if (!ok)
                ModelState.AddModelError(string.Empty, "Não foi possível vincular os dados do formulário.");

            // ---- Sabores
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
            ModelState.Remove(nameof(ProdutoModel.SaboresQuantidades)); // <- evita falso "required"

            // ---- Regras adicionais
            if (!_context.Categorias.AsNoTracking().Any(c => c.Id == produto.CategoriaId))
                ModelState.AddModelError(nameof(ProdutoModel.CategoriaId), "Selecione uma categoria válida.");

            if (produto.EmPromocao)
            {
                if (!produto.PrecoPromocional.HasValue)
                    ModelState.AddModelError(nameof(ProdutoModel.PrecoPromocional), "Informe o preço promocional.");
                else if (produto.PrecoPromocional.Value >= produto.Preco)
                    ModelState.AddModelError(nameof(ProdutoModel.PrecoPromocional), "Preço promocional deve ser menor que o preço.");
            }
            else
            {
                produto.PrecoPromocional = null; // garante consistência
            }

            // ---- Imagem opcional
            ModelState.Remove(nameof(ProdutoModel.ImagemUpload));
            var file = Request.Form.Files[nameof(ProdutoModel.ImagemUpload)];
            if (file is { Length: > 0 })
            {
                if (file.Length > 2 * 1024 * 1024)
                    ModelState.AddModelError(nameof(ProdutoModel.ImagemUpload), "O tamanho da imagem não pode exceder 2MB");

                var allowed = new[] { ".jpg", ".jpeg", ".png" };
                var ext = Path.GetExtension(file.FileName);
                if (!allowed.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    ModelState.AddModelError(nameof(ProdutoModel.ImagemUpload), "Apenas arquivos JPG, JPEG e PNG são permitidos");
            }

            if (!ModelState.IsValid)
            {
                produto.DeserializarSaboresQuantidades();
                produto.TodosSabores = ObterTodosSabores();
                CarregarCategorias();
                return View("Editar", produto);
            }

            // ---- Persistência da imagem (se enviada)
            if (file is { Length: > 0 })
            {
                var uploads = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                Directory.CreateDirectory(uploads);

                if (!string.IsNullOrEmpty(produto.ImagemUrl))
                {
                    var oldPath = Path.Combine(_hostEnvironment.WebRootPath, produto.ImagemUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                using var fs = System.IO.File.Create(Path.Combine(uploads, fileName));
                await file.CopyToAsync(fs);
                produto.ImagemUrl = $"/imagens/produtos/{fileName}";
            }

            _produtoRepository.Atualizar(produto);
            TempData["MensagemSucesso"] = "Produto atualizado com sucesso!";
            return RedirectToAction(nameof(Index));
        }


        /// <summary>
        /// //////
        /// </summary>
        // Função para converter formato brasileiro para decimal
        decimal? ConverterParaDecimal(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;

            // Remove "R$" e espaços
            valor = valor.Replace("R$", "").Trim();

            // Usa CultureInfo pt-BR para converter
            if (decimal.TryParse(valor, NumberStyles.Currency,
                new CultureInfo("pt-BR"), out decimal resultado))
            {
                return resultado;
            }

            // Tenta converter removendo pontos de milhar manualmente
            valor = valor.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out resultado))
            {
                return resultado;
            }

            return null;
        }
        /// <returns></returns>


        // helper
        private static bool TryReadDecimalAnyCulture(string raw, out decimal value)
        {
            var s = (raw ?? "").Trim();
            var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
            return decimal.TryParse(s, styles, CultureInfo.GetCultureInfo("pt-BR"), out value)
                || decimal.TryParse(s.Replace(".", "").Replace(',', '.'), styles, CultureInfo.InvariantCulture, out value);
        }



        [HttpGet]
        public IActionResult Excluir(int id)
        {
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                TempData["MensagemErro"] = "Produto não encontrado";
                return RedirectToAction(nameof(Index));
            }
            return View(produto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarExcluir(int id)
        {
            try
            {
                var produto = _produtoRepository.ObterPorId(id);
                if (produto == null)
                {
                    TempData["MensagemErro"] = "Produto não encontrado";
                    return RedirectToAction(nameof(Index));
                }

                _produtoRepository.Remover(id);
                TempData["MensagemSucesso"] = "Produto excluído com sucesso!";
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao excluir produto: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private void CarregarCategorias()
        {
            try
            {
                var categorias = _categoriaRepository.ObterTodos()
                    .OrderBy(c => c.Nome)
                    .ToList();

                ViewBag.Categorias = new SelectList(categorias, "Id", "Nome");
            }
            catch (Exception ex)
            {
                // Logar o erro se necessário
                ViewBag.Categorias = new SelectList(Enumerable.Empty<SelectListItem>());
                TempData["MensagemErro"] = $"Erro ao carregar categorias: {ex.Message}";
            }
        }

        public IActionResult DetalhesProdutos(int id)
        {
            var produto = _context.Produtos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == id);

            if (produto == null)
                return NotFound();

            // 👇 PRECISA desserializar os sabores do JSON do banco:
            produto.DeserializarSaboresQuantidades();

            // Se quiser garantir que a lista não fique null:
            produto.SaboresQuantidadesList ??= new List<ProdutoModel.SaborQuantidade>();

            // Monta a lista para a view (ordena: com estoque primeiro, depois nome)
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
                .Where(p => p.CategoriaId == produto.CategoriaId && p.Id != produto.Id)
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


        //[HttpGet]
        //public IActionResult ProdutoCard(int id)
        //{
        //    var produto = _produtoRepository.ObterPorId(id);
        //    if (produto == null)
        //    {
        //        TempData["MensagemErro"] = "Produto não encontrado";
        //        return RedirectToAction(nameof(Index));
        //    }
        //    return View(produto); // Views/Produto/ProdutoCard.cshtml -> @model ProdutoModel
        //}

    }
}