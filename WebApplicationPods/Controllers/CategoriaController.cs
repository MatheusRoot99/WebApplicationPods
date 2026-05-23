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
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public CategoriaController(
            ICategoriaRepository categoriaRepository,
            ICurrentLojaService currentLoja,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _categoriaRepository = categoriaRepository;
            _currentLoja = currentLoja;
            _configuration = configuration;
            _env = env;
        }

        private bool IgnorarLojaNoAmbienteAtual()
        {
            return _configuration.GetValue<bool?>("DevelopmentSettings:LocalhostOnly")
                ?? _configuration.GetValue<bool?>("AppSettings:LocalhostOnly")
                ?? _env.IsDevelopment();
        }

        private int? GetLojaIdDoUsuario()
        {
            var claimLojaId = User.FindFirst("LojaId")?.Value
                           ?? User.FindFirst("lojaId")?.Value;

            if (int.TryParse(claimLojaId, out var lojaId) && lojaId > 0)
                return lojaId;

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

            throw new InvalidOperationException("Loja atual não definida. Acesse pelo painel da loja ou vincule o usuário a uma loja.");
        }

        private bool PodeGerenciarCategoria(CategoriaModel categoria)
        {
            if (User.IsInRole("Admin"))
                return true;

            var lojaId = GetLojaIdOrFail();
            return categoria.LojaId == lojaId;
        }

        public IActionResult Index()
        {
            var categorias = _categoriaRepository.ObterTodos();
            return View(categorias);
        }

        public IActionResult Criar()
        {
            return View(new CategoriaModel());
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

            if (!PodeGerenciarCategoria(categoria))
                return Forbid();

            return View(categoria);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, CategoriaModel categoria)
        {
            if (id != categoria.Id) return NotFound();

            ModelState.Remove(nameof(CategoriaModel.Produtos));

            if (!ModelState.IsValid)
                return View(categoria);

            try
            {
                var existente = _categoriaRepository.ObterPorId(id);
                if (existente == null) return NotFound();

                if (!PodeGerenciarCategoria(existente))
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

            if (!PodeGerenciarCategoria(categoria))
                return Forbid();

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

            if (!PodeGerenciarCategoria(categoria))
                return Forbid();

            _categoriaRepository.Remover(id);

            TempData["MensagemSucesso"] = "Categoria removida com sucesso!";
            return RedirectToAction(nameof(Index));
        }
    }
}