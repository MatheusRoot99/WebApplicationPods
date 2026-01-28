using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services
{
    public class LojaConfigService : ILojaConfigService
    {
        private readonly BancoContext _db;
        private readonly ICurrentLojaService _currentLoja;

        public LojaConfigService(BancoContext db, ICurrentLojaService currentLoja)
        {
            _db = db;
            _currentLoja = currentLoja;
        }

        public async Task<LojaConfig?> GetAsync()
        {
            var lojaId = _currentLoja.LojaId;
            if (!lojaId.HasValue) return null;

            return await _db.LojaConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.LojaId == lojaId.Value);
        }

        public async Task<LojaConfig?> UpsertAsync(LojaConfig input)
        {
            var lojaId = _currentLoja.LojaId;
            if (!lojaId.HasValue) return null;

            // garante que sempre salva na loja atual
            input.LojaId = lojaId.Value;

            var exists = await _db.LojaConfigs
                .FirstOrDefaultAsync(x => x.LojaId == lojaId.Value);

            if (exists == null)
            {
                input.UpdatedAt = DateTime.UtcNow;
                _db.LojaConfigs.Add(input);
            }
            else
            {
                var id = exists.Id;
                _db.Entry(exists).CurrentValues.SetValues(input);
                exists.Id = id;
                exists.LojaId = lojaId.Value;
                exists.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return await GetAsync();
        }

        public bool EstaAberto(LojaConfig cfg, DateTime agoraLocal)
        {
            if (!cfg.Ativo || cfg.ForcarFechado) return false;

            var flagDia = agoraLocal.DayOfWeek switch
            {
                DayOfWeek.Sunday => DiasSemanaFlags.Domingo,
                DayOfWeek.Monday => DiasSemanaFlags.Segunda,
                DayOfWeek.Tuesday => DiasSemanaFlags.Terca,
                DayOfWeek.Wednesday => DiasSemanaFlags.Quarta,
                DayOfWeek.Thursday => DiasSemanaFlags.Quinta,
                DayOfWeek.Friday => DiasSemanaFlags.Sexta,
                DayOfWeek.Saturday => DiasSemanaFlags.Sabado,
                _ => DiasSemanaFlags.Nenhum
            };

            if (!cfg.DiasAbertos.HasFlag(flagDia)) return false;

            var agora = agoraLocal.TimeOfDay;
            var abre = cfg.HorarioAbertura;
            var fecha = cfg.HorarioFechamento;

            if (fecha <= abre)
                return (agora >= abre) || (agora <= fecha);
            else
                return (agora >= abre) && (agora <= fecha);
        }

        public StoreHeaderViewModel? BuildHeader(LojaConfig? cfg, string baseUrl, string? perfilUrl)
        {
            if (cfg == null) return null;

            var now = DateTime.Now;
            var aberto = EstaAberto(cfg, now);

            return new StoreHeaderViewModel
            {
                Nome = cfg.Nome,
                LogoUrl = string.IsNullOrWhiteSpace(cfg.LogoPath) ? null : cfg.LogoPath,
                AbertoAgora = aberto,
                FechaAs = cfg.HorarioFechamento,
                PerfilDaLojaUrl = perfilUrl,
                UrlParaCompartilhar = baseUrl,
                MensagemStatus = cfg.MensagemStatus
            };
        }
    }
}
