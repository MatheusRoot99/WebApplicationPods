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

                // Processa o preço
                if (Request.Form.ContainsKey("Preco"))
                {
                    // Limpa o valor do preço, removendo "R$", pontos e substituindo a vírgula por ponto
                    var precoString = Request.Form["Preco"].ToString().Replace("R$", "").Replace(".", "").Replace(",", ".");

                    // Tenta converter o preço para decimal
                    if (decimal.TryParse(precoString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedPrice))
                    {
                        produto.Preco = parsedPrice;
                    }
                    else
                    {
                        ModelState.AddModelError("Preco", "O preço informado é inválido.");
                    }
                }

                // Validação de categoria
                if (produto.CategoriaId == 0 || !_context.Categorias.Any(c => c.Id == produto.CategoriaId))
                {
                    ModelState.AddModelError("CategoriaId", "Selecione uma categoria válida");
                }

                // Validação da imagem
                if (produto.ImagemUpload != null)
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

                // Verifica se o modelo está válido após as validações
                if (ModelState.IsValid)
                {
                    // Se uma imagem foi enviada, processa o upload
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

                        // Salvar o caminho da imagem no banco (se necessário)
                        // produto.ImagemUrl = "/imagens/produtos/" + uniqueFileName;
                    }

                    // Adiciona o produto no banco de dados
                    _produtoRepository.Adicionar(produto);

                    // Mensagem de sucesso
                    TempData["MensagemSucesso"] = "Produto cadastrado com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                // Captura erros genéricos e exibe mensagem de erro
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
                ModelState.Remove("Categoria");
                ModelState.Remove("PedidoItens");

                // Validação de categoria
                if (produto.CategoriaId == 0 || !_context.Categorias.Any(c => c.Id == produto.CategoriaId))
                {
                    ModelState.AddModelError("CategoriaId", "Selecione uma categoria válida");
                }

                // Validação da imagem
                if (produto.ImagemUpload != null)
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

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + produto.ImagemUpload.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await produto.ImagemUpload.CopyToAsync(fileStream);
                        }

                        // Aqui você pode adicionar lógica para atualizar o caminho se necessário
                    }

                    _produtoRepository.Atualizar(produto);
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
    }
}