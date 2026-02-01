using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services;

namespace WebApplicationPods.Controllers
{
    [Area("PainelLojista")]
    [Authorize(Roles = "Lojista,Admin")]
    public class LojaController : Controller
    {
        private readonly ILojaConfigService _svc;
        private readonly IWebHostEnvironment _env;
        private readonly BancoContext _db;

        public LojaController(ILojaConfigService svc, IWebHostEnvironment env, BancoContext db)
        {
            _svc = svc; _env = env; _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Editar()
        {
            var cfg = await _svc.GetAsync() ?? new LojaConfig();
            return View(cfg);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            LojaConfig model,
            IFormFile? logoFile,
            [FromForm] int[]? DiasAbertosSelecionados)
        {
            // 1) Dias Abertos (Flags)
            model.DiasAbertos = DiasSemanaFlags.Nenhum;
            if (DiasAbertosSelecionados is { Length: > 0 })
            {
                foreach (var v in DiasAbertosSelecionados)
                    model.DiasAbertos |= (DiasSemanaFlags)v;
            }

            // 2) Normalizações de endereço
            model.Estado = (model.Estado ?? "").Trim().ToUpper();
            var dig = new string((model.Cep ?? "").Where(char.IsDigit).ToArray());
            if (dig.Length == 8) model.Cep = $"{dig[..5]}-{dig[5..]}";

            // 3) Carrega config atual pra reaproveitar/remover logo antiga
            var cfgAtual = await _svc.GetAsync() ?? new LojaConfig();
            var oldLogoPath = cfgAtual.LogoPath;

            // 4) Upload de logo (com temp + move atômico)
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

                    var tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
                    await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        await logoFile.CopyToAsync(fs);

                    System.IO.File.Move(tempPath, finalPath, overwrite: true);
                    model.LogoPath = $"/img/loja/{fileName}";

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
                // sem nova logo -> mantém a existente
                model.LogoPath = oldLogoPath;
            }

            if (!ModelState.IsValid)
                return View(model);

            // 5) Persiste (o service garante LojistaUserId do usuário atual)
            await _svc.UpsertAsync(model);
            TempData["Sucesso"] = "Configurações da loja atualizadas!";
            return RedirectToAction(nameof(Editar));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimparDuplicatas()
        {
            var all = await _db.LojaConfigs
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();

            if (all.Count > 1)
            {
                var keep = all.First();
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
