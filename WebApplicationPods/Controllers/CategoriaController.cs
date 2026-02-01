using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Lojista,Admin")]
    public class CategoriaController : Controller
    {
        private readonly ICategoriaRepository _categoriaRepository;
        private readonly ICurrentLojaService _currentLoja;

        public CategoriaController(
            ICategoriaRepository categoriaRepository,
            ICurrentLojaService currentLoja)
        {
            _categoriaRepository = categoriaRepository;
            _currentLoja = currentLoja;
        }

        private int GetLojaIdOrFail()
        {
            if (!_currentLoja.HasLoja || !_currentLoja.LojaId.HasValue)
                throw new InvalidOperationException("Loja atual não definida. Verifique o middleware multi-loja.");

            return _currentLoja.LojaId.Value;
        }

        public IActionResult Index()
        {
            // Se o ICategoriaRepository usa BancoContext, o HasQueryFilter já filtra por LojaId
            var categorias = _categoriaRepository.ObterTodos();
            return View(categorias);
        }

        public IActionResult Criar()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Criar(CategoriaModel categoria)
        {
            // Remove a validação da propriedade Produtos
            ModelState.Remove(nameof(CategoriaModel.Produtos));

            if (!ModelState.IsValid)
            {
                // Log detalhado dos erros (para desenvolvimento)
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    Console.WriteLine($"Erro de validação: {error.ErrorMessage}");
                }

                TempData["MensagemErro"] = "Por favor, corrija os erros no formulário";
                TempData["Erro"] = TempData["MensagemErro"];
                return View(categoria);
            }

            try
            {
                var lojaId = GetLojaIdOrFail();

                // 🔴 ponto chave do multi-loja: categoria sempre pertence à loja atual
                categoria.LojaId = lojaId;

                _categoriaRepository.Adicionar(categoria);

                TempData["MensagemSucesso"] = "Categoria cadastrada com sucesso!";
                TempData["Sucesso"] = TempData["MensagemSucesso"];
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao cadastrar categoria: {ex.Message}";
                TempData["Erro"] = TempData["MensagemErro"];
                return View(categoria);
            }
        }

        public IActionResult Editar(int id)
        {
            var categoria = _categoriaRepository.ObterPorId(id);
            if (categoria == null)
            {
                return NotFound();
            }
            return View(categoria);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, CategoriaModel categoria)
        {
            if (id != categoria.Id)
            {
                return NotFound();
            }

            // Remove a validação da propriedade Produtos
            ModelState.Remove(nameof(CategoriaModel.Produtos));

            if (!ModelState.IsValid)
            {
                // Log detalhado dos erros (para desenvolvimento)
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    Console.WriteLine($"Erro de validação: {error.ErrorMessage}");
                }

                TempData["MensagemErro"] = "Por favor, corrija os erros no formulário";
                TempData["Erro"] = TempData["MensagemErro"];
                return View(categoria);
            }

            try
            {
                var lojaId = GetLojaIdOrFail();

                // Sempre busca do repositório (já filtrado pela loja) e atualiza em cima dele
                var existente = _categoriaRepository.ObterPorId(id);
                if (existente == null)
                    return NotFound();

                // segurança extra: garante que é da loja atual
                if (existente.LojaId != lojaId)
                    return Forbid();

                existente.Nome = categoria.Nome;
                existente.Descricao = categoria.Descricao;
                // NÃO mexe em LojaId

                _categoriaRepository.Atualizar(existente);

                TempData["MensagemSucesso"] = "Categoria atualizada com sucesso!";
                TempData["Sucesso"] = TempData["MensagemSucesso"];
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao atualizar categoria: {ex.Message}";
                TempData["Erro"] = TempData["MensagemErro"];
                return View(categoria);
            }
        }

        public IActionResult Excluir(int id)
        {
            var categoria = _categoriaRepository.ObterPorId(id);
            if (categoria == null)
            {
                return NotFound();
            }

            // Verifica se existem produtos vinculados
            if (categoria.Produtos?.Any() == true)
            {
                TempData["Erro"] = "Não é possível excluir esta categoria pois existem produtos vinculados a ela.";
                TempData["MensagemErro"] = TempData["Erro"];
                return RedirectToAction(nameof(Index));
            }

            return View(categoria);
        }

        [HttpPost, ActionName("Excluir")]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarExcluir(int id)
        {
            var lojaId = _currentLoja.LojaId;

            var categoria = _categoriaRepository.ObterPorId(id);
            if (categoria == null)
                return NotFound();

            // garante que só exclui da loja atual
            if (lojaId.HasValue && categoria.LojaId != lojaId.Value)
                return Forbid();

            _categoriaRepository.Remover(id);

            TempData["MensagemSucesso"] = "Categoria removida com sucesso!";
            TempData["Sucesso"] = TempData["MensagemSucesso"];
            return RedirectToAction(nameof(Index));
        }
    }
}
