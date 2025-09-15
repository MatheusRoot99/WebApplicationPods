
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services;

namespace WebApplicationPods.Controllers
{
    [Authorize(Roles = "Lojista,Admin")]
    public class LojaController : Controller
    {
        private readonly ILojaConfigService _svc;
        private readonly IWebHostEnvironment _env;
        private readonly BancoContext _db; // <-- AQUI

        public LojaController(ILojaConfigService svc, IWebHostEnvironment env, BancoContext db)
        {
            _svc = svc; _env = env; _db = db; // <-- AQUI
        }

        [HttpGet]
        public async Task<IActionResult> Editar()
        {
            var cfg = await _svc.GetAsync();
            return View(cfg);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(LojaConfig model, IFormFile? logoFile)
        {
            // Pega a config atual para, se trocar a logo, remover a antiga
            var cfgAtual = await _svc.GetAsync();
            var oldLogoPath = cfgAtual.LogoPath; // pode ser null

            // Se veio arquivo, valida e salva usando tmp + move (atômico)
            if (logoFile is { Length: > 0 })
            {
                var ext = Path.GetExtension(logoFile.FileName).ToLowerInvariant();
                var okExt = ext is ".png" or ".jpg" or ".jpeg" or ".webp";
                if (!okExt)
                    ModelState.AddModelError("LogoPath", "Use .png, .jpg, .jpeg ou .webp");

                if (ModelState.IsValid)
                {
                    var dir = Path.Combine(_env.WebRootPath, "img", "loja");
                    Directory.CreateDirectory(dir);

                    var fileName = $"logo_{DateTime.UtcNow.Ticks}{ext}";
                    var finalPath = Path.Combine(dir, fileName);

                    // 1) grava primeiro em um arquivo temporário
                    var tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
                    await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        await logoFile.CopyToAsync(fs);
                    }

                    // 2) move atômico para o destino final
                    System.IO.File.Move(tempPath, finalPath, overwrite: true);

                    // 3) atualiza o caminho público no model
                    model.LogoPath = $"/img/loja/{fileName}";

                    // 4) remove a imagem antiga com try/catch silencioso
                    if (!string.IsNullOrWhiteSpace(oldLogoPath) &&
                        !string.Equals(oldLogoPath, model.LogoPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var physOld = Path.Combine(
                                _env.WebRootPath,
                                oldLogoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
                            );
                            if (System.IO.File.Exists(physOld))
                                System.IO.File.Delete(physOld);
                        }
                        catch { /* ignora erro de IO */ }
                    }
                }
            }
            else
            {
                // Se não enviou nova logo, mantém a existente
                model.LogoPath = oldLogoPath;
            }

            if (!ModelState.IsValid)
                return View(model);

            await _svc.UpsertAsync(model);
            TempData["Sucesso"] = "Configurações da loja atualizadas!";
            return RedirectToAction(nameof(Editar));
        }


        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimparDuplicatas()
        {
            // Se for multi-loja, filtre por LojistaUserId aqui.
            var all = await _db.LojaConfigs
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();

            if (all.Count > 1)
            {
                var keep = all.First();        // mais recente
                var remove = all.Skip(1).ToList();
                _db.LojaConfigs.RemoveRange(remove);
                await _db.SaveChangesAsync();
                TempData["Sucesso"] = $"Limpou {remove.Count} duplicata(s). Mantido Id={keep.Id}.";
            }
            else
            {
                TempData["Sucesso"] = "Nenhuma duplicata encontrada.";
            }

            return RedirectToAction(nameof(Editar));
        }
    }
}
