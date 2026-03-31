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

        public CategoriaController(ICategoriaRepository categoriaRepository, ICurrentLojaService currentLoja)
        {
            _categoriaRepository = categoriaRepository;
            _currentLoja = currentLoja;
        }

        private int GetLojaIdOrFail()
        {
            // Multi-loja desativado por enquanto
            return 1;
        }

        public IActionResult Index()
        {
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
            ModelState.Remove(nameof(CategoriaModel.Produtos));

            if (!ModelState.IsValid)
                return View(categoria);

            try
            {
                categoria.LojaId = GetLojaIdOrFail();
                _categoriaRepository.Adicionar(categoria);

                TempData["MensagemSucesso"] = "Categoria cadastrada com sucesso!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao cadastrar categoria: {ex.Message}";
                return View(categoria);
            }
        }

        public IActionResult Editar(int id)
        {
            var categoria = _categoriaRepository.ObterPorId(id);
            if (categoria == null) return NotFound();

            return View(categoria);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, CategoriaModel categoria)
        {
            if (id != categoria.Id) return NotFound();

            ModelState.Remove(nameof(CategoriaModel.Produtos));
            if (!ModelState.IsValid) return View(categoria);

            try
            {
                var lojaId = GetLojaIdOrFail();

                var existente = _categoriaRepository.ObterPorId(id);
                if (existente == null) return NotFound();

                if (existente.LojaId != lojaId && !User.IsInRole("Admin"))
                    return Forbid();

                existente.Nome = categoria.Nome;
                existente.Descricao = categoria.Descricao;

                _categoriaRepository.Atualizar(existente);

                TempData["MensagemSucesso"] = "Categoria atualizada com sucesso!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["MensagemErro"] = $"Erro ao atualizar categoria: {ex.Message}";
                return View(categoria);
            }
        }

        public IActionResult Excluir(int id)
        {
            var categoria = _categoriaRepository.ObterPorId(id);
            if (categoria == null) return NotFound();

            if (categoria.Produtos?.Any() == true)
            {
                TempData["MensagemErro"] = "Não é possível excluir esta categoria pois existem produtos vinculados.";
                return RedirectToAction(nameof(Index));
            }

            return View(categoria);
        }

        [HttpPost, ActionName("Excluir")]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarExcluir(int id)
        {
            var categoria = _categoriaRepository.ObterPorId(id);
            if (categoria == null) return NotFound();

            var lojaId = GetLojaIdOrFail();

            if (categoria.LojaId != lojaId && !User.IsInRole("Admin"))
                return Forbid();

            _categoriaRepository.Remover(id);

            TempData["MensagemSucesso"] = "Categoria removida com sucesso!";
            return RedirectToAction(nameof(Index));
        }
    }
}