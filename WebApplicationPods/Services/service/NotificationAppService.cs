using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class NotificationAppService : INotificationAppService
    {
        private readonly BancoContext _context;

        public NotificationAppService(BancoContext context)
        {
            _context = context;
        }

        public async Task<NotificacaoModel> CriarAsync(
            int lojaId,
            string titulo,
            string mensagem,
            string tipo = "info",
            int? pedidoId = null)
        {
            if (lojaId <= 0)
                throw new ArgumentException("Loja inválida.", nameof(lojaId));

            titulo = Limitar(TrimOrEmpty(titulo), 120);
            mensagem = Limitar(TrimOrEmpty(mensagem), 500);
            tipo = Limitar(string.IsNullOrWhiteSpace(tipo) ? "info" : tipo.Trim().ToLowerInvariant(), 40);

            if (string.IsNullOrWhiteSpace(titulo))
                throw new ArgumentException("O título da notificação é obrigatório.", nameof(titulo));

            if (string.IsNullOrWhiteSpace(mensagem))
                throw new ArgumentException("A mensagem da notificação é obrigatória.", nameof(mensagem));

            var notificacao = new NotificacaoModel
            {
                LojaId = lojaId,
                PedidoId = pedidoId,
                Tipo = tipo,
                Titulo = titulo,
                Mensagem = mensagem,
                Lida = false,
                DataCadastro = DateTime.Now
            };

            _context.Notificacoes.Add(notificacao);
            await _context.SaveChangesAsync();

            return notificacao;
        }

        public async Task<List<NotificacaoModel>> ObterRecentesAsync(
            int lojaId,
            int take = 8,
            bool incluirLidas = true)
        {
            if (lojaId <= 0) return new List<NotificacaoModel>();

            var query = _context.Notificacoes
                .AsNoTracking()
                .Where(x => x.LojaId == lojaId);

            if (!incluirLidas)
                query = query.Where(x => !x.Lida);

            return await query
                .OrderByDescending(x => x.DataCadastro)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<NotificacaoModel>> ObterCentralAsync(
            int lojaId,
            bool somenteNaoLidas = false,
            int take = 100)
        {
            if (lojaId <= 0) return new List<NotificacaoModel>();

            var query = _context.Notificacoes
                .AsNoTracking()
                .Where(x => x.LojaId == lojaId);

            if (somenteNaoLidas)
                query = query.Where(x => !x.Lida);

            return await query
                .OrderByDescending(x => x.DataCadastro)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> ContarNaoLidasAsync(int lojaId)
        {
            if (lojaId <= 0) return 0;

            return await _context.Notificacoes
                .AsNoTracking()
                .CountAsync(x => x.LojaId == lojaId && !x.Lida);
        }

        public async Task<NotificacaoModel?> ObterPorIdAsync(int id, int lojaId)
        {
            if (id <= 0 || lojaId <= 0) return null;

            return await _context.Notificacoes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.LojaId == lojaId);
        }

        public async Task<bool> MarcarComoLidaAsync(int id, int lojaId)
        {
            if (id <= 0 || lojaId <= 0) return false;

            var notificacao = await _context.Notificacoes
                .FirstOrDefaultAsync(x => x.Id == id && x.LojaId == lojaId);

            if (notificacao == null)
                return false;

            if (notificacao.Lida)
                return true;

            notificacao.Lida = true;
            notificacao.DataLeitura = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> MarcarTodasComoLidasAsync(int lojaId)
        {
            if (lojaId <= 0) return 0;

            var lista = await _context.Notificacoes
                .Where(x => x.LojaId == lojaId && !x.Lida)
                .ToListAsync();

            if (!lista.Any())
                return 0;

            var agora = DateTime.Now;

            foreach (var item in lista)
            {
                item.Lida = true;
                item.DataLeitura = agora;
            }

            await _context.SaveChangesAsync();
            return lista.Count;
        }

        private static string TrimOrEmpty(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string Limitar(string value, int max)
            => value.Length <= max ? value : value[..max];
    }
}