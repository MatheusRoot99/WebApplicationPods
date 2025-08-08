using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SitePodsInicial.Models;
using SitePodsInicial.Repository.Interface;
using System;

namespace SitePodsInicial.Controllers
{
    public class CategoriaController : Controller
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public CategoriaController(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
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
            // Remove a validação da propriedade Produtos
            ModelState.Remove("Produtos");

            if (ModelState.IsValid)
            {
                try
                {
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

            // Log detalhado dos erros (para desenvolvimento)
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                Console.WriteLine($"Erro de validação: {error.ErrorMessage}");
            }

            TempData["MensagemErro"] = "Por favor, corrija os erros no formulário";
            return View(categoria);
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
            ModelState.Remove("Produtos");

            if (ModelState.IsValid)
            {
                try
                {
                    _categoriaRepository.Atualizar(categoria);
                    TempData["MensagemSucesso"] = "Categoria atualizada com sucesso!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["MensagemErro"] = $"Erro ao atualizar categoria: {ex.Message}";
                    return View(categoria);
                }
            }

            // Log detalhado dos erros (para desenvolvimento)
            var errors = ModelState.Values.SelectMany(v => v.Errors);
            foreach (var error in errors)
            {
                Console.WriteLine($"Erro de validação: {error.ErrorMessage}");
            }

            TempData["MensagemErro"] = "Por favor, corrija os erros no formulário";
            return View(categoria);
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
                return RedirectToAction(nameof(Index));
            }

            return View(categoria);
        }

        [HttpPost, ActionName("Excluir")]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmarExcluir(int id)
        {
            _categoriaRepository.Remover(id);
            return RedirectToAction(nameof(Index));
        }
    }
}