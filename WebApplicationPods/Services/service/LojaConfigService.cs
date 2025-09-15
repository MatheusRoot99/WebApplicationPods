using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;

namespace WebApplicationPods.Services
{
    public class LojaConfigService : ILojaConfigService
    {
        private readonly BancoContext _db;

        public LojaConfigService(BancoContext db) { _db = db; }

        public async Task<LojaConfig> GetAsync()
        {
            var cfg = await _db.LojaConfigs
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();

            if (cfg == null)
            {
                cfg = new LojaConfig();
                _db.LojaConfigs.Add(cfg);
                await _db.SaveChangesAsync();
            }

            return cfg;
        }

        public async Task<LojaConfig> UpsertAsync(LojaConfig input)
        {
            var exists = await _db.LojaConfigs.FirstOrDefaultAsync();
            if (exists == null)
            {
                input.UpdatedAt = DateTime.UtcNow;
                _db.LojaConfigs.Add(input);
            }
            else
            {
                var id = exists.Id;
                _db.Entry(exists).CurrentValues.SetValues(input);
                exists.Id = id; // garante não trocar o Id
                exists.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            return await GetAsync();
        }

        public bool EstaAberto(LojaConfig cfg, DateTime agoraLocal)
        {
            if (!cfg.Ativo || cfg.ForcarFechado) return false;

            // dia da semana
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

            // suporta virada (ex.: 18:00 às 02:00)
            if (fecha <= abre)
                return (agora >= abre) || (agora <= fecha);
            else
                return (agora >= abre) && (agora <= fecha);
        }

        public StoreHeaderViewModel BuildHeader(LojaConfig cfg, string baseUrl, string? perfilUrl)
        {
            var now = DateTime.Now; // timezone do servidor; ajuste se usar TZ diferente
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
