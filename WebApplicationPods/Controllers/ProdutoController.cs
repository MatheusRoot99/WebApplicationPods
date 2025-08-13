using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Hosting;
using SitePodsInicial.Data;
using SitePodsInicial.Models;
using SitePodsInicial.Repository.Interface;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SitePodsInicial.Models;
using Newtonsoft.Json;

namespace SitePodsInicial.Controllers
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

        public IActionResult Index()
        {
            var produtos = _produtoRepository.ObterTodos();
            return View(produtos);
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

            CarregarCategorias();
            return View(produto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, ProdutoModel produto)
        {
            if (id != produto.Id)
            {
                TempData["MensagemErro"] = "ID do produto não corresponde";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Remove as validações desnecessárias
                ModelState.Remove("Categoria");
                ModelState.Remove("PedidoItens");
                ModelState.Remove("ImagemUrl");
                ModelState.Remove("ImagemUpload");
                ModelState.Remove("Sabor"); // Adicione esta linha se não removeu a propriedade
                ModelState.Remove("SaboresDisponiveis"); // Adicione esta linha
                ModelState.Remove("SaboresSelecionados"); // Adicione esta linha
                ModelState.Remove("TodosSabores"); // Adicione esta linha

                // Validação de categoria
                if (produto.CategoriaId == 0 || !_context.Categorias.Any(c => c.Id == produto.CategoriaId))
                {
                    ModelState.AddModelError("CategoriaId", "Selecione uma categoria válida");
                }

                // Validação de sabores
                var saboresSelecionados = Request.Form["SaboresSelecionados"].ToList();
                if (saboresSelecionados.Count == 0)
                {
                    ModelState.AddModelError("", "Selecione pelo menos um sabor");
                }

                // Obter o produto existente
                var produtoExistente = _produtoRepository.ObterPorId(id);
                if (produtoExistente == null)
                {
                    TempData["MensagemErro"] = "Produto não encontrado";
                    return RedirectToAction(nameof(Index));
                }

                // Validação da imagem apenas se uma nova for enviada
                if (produto.ImagemUpload != null && produto.ImagemUpload.Length > 0)
                {
                    if (produto.ImagemUpload.Length > 2 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImagemUpload", "O tamanho da imagem não pode exceder 2MB");
                    }

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(produto.ImagemUpload.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImagemUpload", "Apenas arquivos JPG, JPEG e PNG são permitidos");
                    }
                }

                if (ModelState.IsValid)
                {
                    // Processamento da imagem (se enviada)
                    if (produto.ImagemUpload != null && produto.ImagemUpload.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        // Remove a imagem antiga se existir
                        if (!string.IsNullOrEmpty(produtoExistente.ImagemUrl))
                        {
                            var imagemExistentePath = Path.Combine(_hostEnvironment.WebRootPath,
                                produtoExistente.ImagemUrl.TrimStart('/'));
                            if (System.IO.File.Exists(imagemExistentePath))
                            {
                                System.IO.File.Delete(imagemExistentePath);
                            }
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + produto.ImagemUpload.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await produto.ImagemUpload.CopyToAsync(fileStream);
                        }

                        produtoExistente.ImagemUrl = $"/imagens/produtos/{uniqueFileName}";
                    }

                    // Atualiza os outros campos
                    produtoExistente.Nome = produto.Nome;
                    produtoExistente.Descricao = produto.Descricao;
                    produtoExistente.Preco = produto.Preco;
                    produtoExistente.Estoque = produto.Estoque;
                    produtoExistente.CategoriaId = produto.CategoriaId;
                    produtoExistente.Ativo = produto.Ativo;

                    _produtoRepository.Atualizar(produtoExistente);
                    TempData["MensagemSucesso"] = "Produto atualizado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao atualizar produto: {ex.Message}";
            }

            CarregarCategorias();
            return View(produto);
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
            var produto = _produtoRepository.ObterPorId(id);
            if (produto == null)
            {
                return NotFound();
            }

            var saboresDisponiveis = produto.SaboresQuantidadesList?
                .Where(sq => sq.Quantidade > 0)
                .Select(sq => sq.Sabor)
                .ToList() ?? new List<string>();

            var viewModel = new ProdutoDetalhesViewModel
            {
                Produto = produto,
                SaboresDisponiveis = saboresDisponiveis,
                ProdutosRelacionados = _context.Produtos
                    .Where(p => p.CategoriaId == produto.CategoriaId && p.Id != produto.Id)
                    .Take(4)
                    .ToList()
            };

            return View("Detalhes", viewModel);
        }



    }
}