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
                // Carrega as categorias antes de exibir a view
                var categorias = _categoriaRepository.ObterTodos()
                    .OrderBy(c => c.Nome)
                    .ToList();

                ViewBag.Categorias = new SelectList(categorias, "Id", "Nome");

                return View();
            }
            catch (Exception ex)
            {
                // Logar o erro (ex.: usando ILogger)
                TempData["MensagemErro"] = "Erro ao carregar o formulário de cadastro";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(ProdutoModel produto)
        {
            try
            {
                // Remove as propriedades que não precisam ser validadas
                ModelState.Remove("Categoria");
                ModelState.Remove("PedidoItens");
                ModelState.Remove("ImagemUrl");

                // Processa os preços
                if (Request.Form.ContainsKey("Preco"))
                {
                    var precoString = Request.Form["Preco"].ToString()
                        .Replace("R$", "")
                        .Replace(".", "")
                        .Replace(",", ".");

                    if (decimal.TryParse(precoString, NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var parsedPrice))
                    {
                        produto.Preco = parsedPrice;
                    }
                    else
                    {
                        ModelState.AddModelError("Preco", "O preço informado é inválido.");
                    }
                }

                if (Request.Form.ContainsKey("PrecoPromocional") && !string.IsNullOrEmpty(Request.Form["PrecoPromocional"]))
                {
                    var precoPromoString = Request.Form["PrecoPromocional"].ToString()
                        .Replace("R$", "")
                        .Replace(".", "")
                        .Replace(",", ".");

                    if (decimal.TryParse(precoPromoString, NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var parsedPromoPrice))
                    {
                        produto.PrecoPromocional = parsedPromoPrice;
                    }
                    else
                    {
                        ModelState.AddModelError("PrecoPromocional", "O preço promocional informado é inválido.");
                    }
                }

                // Validação de categoria
                if (produto.CategoriaId == 0 || !_context.Categorias.Any(c => c.Id == produto.CategoriaId))
                {
                    ModelState.AddModelError("CategoriaId", "Selecione uma categoria válida");
                }

                // Validação da imagem (opcional)
                if (produto.ImagemUpload != null && produto.ImagemUpload.Length > 0)
                {
                    // Verifica o tamanho do arquivo (máximo 2MB)
                    if (produto.ImagemUpload.Length > 2 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImagemUpload", "O tamanho da imagem não pode exceder 2MB");
                    }

                    // Verifica o tipo do arquivo
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(produto.ImagemUpload.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImagemUpload", "Apenas arquivos JPG, JPEG e PNG são permitidos");
                    }
                }

                // Validação adicional para preço promocional
                if (produto.EmPromocao && (!produto.PrecoPromocional.HasValue || produto.PrecoPromocional >= produto.Preco))
                {
                    ModelState.AddModelError("PrecoPromocional", "O preço promocional deve ser menor que o preço normal");
                }

                if (ModelState.IsValid)
                {
                    // Processa a imagem se foi enviada
                    if (produto.ImagemUpload != null && produto.ImagemUpload.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "imagens/produtos");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + produto.ImagemUpload.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await produto.ImagemUpload.CopyToAsync(fileStream);
                        }

                        produto.ImagemUrl = $"/imagens/produtos/{uniqueFileName}";
                    }

                    // Define a data de cadastro
                    produto.DataCadastro = DateTime.Now;

                    // Adiciona o produto no banco de dados
                    _produtoRepository.Adicionar(produto);

                    TempData["MensagemSucesso"] = "Produto cadastrado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao cadastrar produto: {ex.Message}";
            }

            // Carregar categorias para a view em caso de erro
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
                ModelState.Remove("ImagemUpload"); // Remove a validação padrão do ImagemUpload

                // Validação de categoria
                if (produto.CategoriaId == 0 || !_context.Categorias.Any(c => c.Id == produto.CategoriaId))
                {
                    ModelState.AddModelError("CategoriaId", "Selecione uma categoria válida");
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

            // Crie o ViewModel e preencha com os dados necessários
            var viewModel = new ProdutoDetalhesViewModel
            {
                Produto = produto,
                SaboresDisponiveis = new List<string>
        {
            "Aloe Grape - Aloe Vera e Uva",
            "Banana Coconut - Banana e Água de Coco",
            "Banana Ice",
            "Blueberry Ice - Mirtilo Ice",
            "Blueberry Straw Coco - Mirtilo, Morango, Coco",
            "Grape Ice - Uva Ice",
            "Green Apple - Maçã Verde",
            "Icy Mint - Menta Ice",
            "Menthal - Menta e Hortelã Ice",
            "Pineapple Ice - Abacaxi Ice",
            "Strawberry Banana - Morango e Banana",
            "Strawberry Ice - Morango Ice",
            "Watermelon Ice - Melancia Ice"
        },
                ProdutosRelacionados = _context.Produtos
                    .Where(p => p.CategoriaId == produto.CategoriaId && p.Id != produto.Id)
                    .Take(4)
                    .ToList()
            };

            return View("Detalhes", viewModel); // Passe o ViewModel para a view
        }


    }
}