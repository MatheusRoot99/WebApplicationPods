using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;
using EntregaStatusConst = WebApplicationPods.Enum.EntregaStatus;

namespace WebApplicationPods.Services.service
{
    public class EntregaAppService : IEntregaAppService
    {
        private readonly BancoContext _context;
        private readonly IPedidoAppService _pedidoAppService;
        private readonly IHubContext<PedidosHub> _hub;

        public EntregaAppService(
            BancoContext context,
            IPedidoAppService pedidoAppService,
            IHubContext<PedidosHub> hub)
        {
            _context = context;
            _pedidoAppService = pedidoAppService;
            _hub = hub;
        }

        public async Task<bool> AtribuirEntregadorAsync(int pedidoId, int entregadorId, string? responsavel = null)
        {
            var pedido = await _context.Pedidos
                .Include(x => x.Cliente)
                .Include(x => x.Entrega)
                .FirstOrDefaultAsync(x => x.Id == pedidoId);

            if (pedido == null)
                return false;

            var entregador = await _context.Entregadores
                .Include(x => x.Usuario)
                .FirstOrDefaultAsync(x => x.Id == entregadorId && x.Ativo);

            if (entregador == null)
                return false;

            if (pedido.Entrega == null)
            {
                pedido.Entrega = new EntregaModel
                {
                    PedidoId = pedido.Id,
                    EntregadorId = entregador.Id,
                    Status = EntregaStatusConst.Atribuida,
                    DataAtribuicao = DateTime.Now,
                    DataCadastro = DateTime.Now,
                    DataAtualizacao = DateTime.Now,
                    Observacao = $"Entregador atribuído: {entregador.Nome}",
                    NomeRecebedor = null,
                    ObservacaoEntrega = null,
                    ComprovanteEntregaUrl = null
                };

                _context.Entregas.Add(pedido.Entrega);
            }
            else
            {
                pedido.Entrega.EntregadorId = entregador.Id;
                pedido.Entrega.Status = EntregaStatusConst.Atribuida;
                pedido.Entrega.DataAtribuicao = DateTime.Now;
                pedido.Entrega.DataAceite = null;
                pedido.Entrega.DataColeta = null;
                pedido.Entrega.DataSaidaParaEntrega = null;
                pedido.Entrega.DataConclusao = null;
                pedido.Entrega.DataAtualizacao = DateTime.Now;
                pedido.Entrega.Observacao = $"Entregador atribuído: {entregador.Nome}";
                pedido.Entrega.NomeRecebedor = null;
                pedido.Entrega.ObservacaoEntrega = null;
                pedido.Entrega.ComprovanteEntregaUrl = null;
            }

            // compatibilidade temporária com o pedido
            pedido.EntregadorId = entregador.Id;
            pedido.DataAtribuicaoEntregador = DateTime.Now;

            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.Atribuido,
                nomeResponsavel: responsavel,
                observacao: $"Entregador atribuído: {entregador.Nome}",
                origem: "PainelLojista");

            if (entregador.Usuario != null)
            {
                await _hub.Clients.User(entregador.Usuario.Id.ToString()).SendAsync("EntregaAtribuida", new
                {
                    pedidoId = pedido.Id,
                    cliente = pedido.Cliente?.Nome,
                    total = pedido.ValorTotal,
                    status = EntregaStatusConst.Atribuida
                });
            }

            return true;
        }

        public async Task<bool> AceitarEntregaAsync(int pedidoId, int entregadorUserId)
        {
            var pedido = await ObterPedidoComEntregaAsync(pedidoId);
            if (pedido == null || !EntregaPertenceAoUsuario(pedido, entregadorUserId) || pedido.Entrega == null)
                return false;

            if (!StatusEh(pedido.Entrega.Status, EntregaStatusConst.Atribuida))
                return false;

            pedido.Entrega.Status = EntregaStatusConst.Aceita;
            pedido.Entrega.DataAceite = DateTime.Now;
            pedido.Entrega.DataAtualizacao = DateTime.Now;

            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.Atribuido,
                nomeResponsavel: pedido.Entregador?.Nome,
                usuarioResponsavelId: entregadorUserId.ToString(),
                observacao: "Entrega aceita pelo entregador.",
                origem: "PainelEntregador");

            return true;
        }

        public async Task<bool> MarcarColetadaAsync(int pedidoId, int entregadorUserId)
        {
            var pedido = await ObterPedidoComEntregaAsync(pedidoId);
            if (pedido == null || !EntregaPertenceAoUsuario(pedido, entregadorUserId) || pedido.Entrega == null)
                return false;

            if (!StatusEh(pedido.Entrega.Status, EntregaStatusConst.Aceita))
                return false;

            pedido.Entrega.Status = EntregaStatusConst.Coletada;
            pedido.Entrega.DataColeta = DateTime.Now;
            pedido.Entrega.DataAtualizacao = DateTime.Now;

            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.Atribuido,
                nomeResponsavel: pedido.Entregador?.Nome,
                usuarioResponsavelId: entregadorUserId.ToString(),
                observacao: "Pedido coletado pelo entregador.",
                origem: "PainelEntregador");

            return true;
        }

        public async Task<bool> MarcarSaiuParaEntregaAsync(int pedidoId, int entregadorUserId)
        {
            var pedido = await ObterPedidoComEntregaAsync(pedidoId);
            if (pedido == null || !EntregaPertenceAoUsuario(pedido, entregadorUserId) || pedido.Entrega == null)
                return false;

            if (!StatusEh(pedido.Entrega.Status, EntregaStatusConst.Coletada))
                return false;

            pedido.Entrega.Status = EntregaStatusConst.EmRota;
            pedido.Entrega.DataSaidaParaEntrega = DateTime.Now;
            pedido.Entrega.DataAtualizacao = DateTime.Now;

            // compatibilidade temporária
            pedido.DataSaiuParaEntrega = DateTime.Now;

            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.SaiuParaEntrega,
                nomeResponsavel: pedido.Entregador?.Nome,
                usuarioResponsavelId: entregadorUserId.ToString(),
                observacao: "Entregador saiu para entrega.",
                origem: "PainelEntregador");

            return true;
        }

        public async Task<bool> MarcarEntregueAsync(
            int pedidoId,
            int entregadorUserId,
            string nomeRecebedor,
            string? observacaoEntrega = null,
            string? comprovanteEntregaUrl = null)
        {
            var pedido = await ObterPedidoComEntregaAsync(pedidoId);
            if (pedido == null || !EntregaPertenceAoUsuario(pedido, entregadorUserId) || pedido.Entrega == null)
                return false;

            if (!StatusEh(pedido.Entrega.Status, EntregaStatusConst.EmRota))
                return false;

            if (string.IsNullOrWhiteSpace(nomeRecebedor))
                return false;

            var nomeRecebedorFinal = nomeRecebedor.Trim();
            var observacaoEntregaFinal = string.IsNullOrWhiteSpace(observacaoEntrega)
                ? null
                : observacaoEntrega.Trim();

            pedido.Entrega.Status = EntregaStatusConst.Entregue;
            pedido.Entrega.DataConclusao = DateTime.Now;
            pedido.Entrega.DataAtualizacao = DateTime.Now;
            pedido.Entrega.NomeRecebedor = nomeRecebedorFinal;
            pedido.Entrega.ObservacaoEntrega = observacaoEntregaFinal;
            pedido.Entrega.ComprovanteEntregaUrl = comprovanteEntregaUrl;
            pedido.Entrega.Observacao = $"Pedido entregue para {nomeRecebedorFinal}.";

            // compatibilidade temporária
            pedido.DataEntregue = DateTime.Now;

            await _context.SaveChangesAsync();

            var observacaoHistorico = $"Pedido entregue com sucesso. Recebido por: {nomeRecebedorFinal}.";
            if (!string.IsNullOrWhiteSpace(observacaoEntregaFinal))
                observacaoHistorico += $" Observação: {observacaoEntregaFinal}";
            if (!string.IsNullOrWhiteSpace(comprovanteEntregaUrl))
                observacaoHistorico += " Foto do comprovante anexada.";

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.Entregue,
                nomeResponsavel: pedido.Entregador?.Nome,
                usuarioResponsavelId: entregadorUserId.ToString(),
                observacao: observacaoHistorico,
                origem: "PainelEntregador");

            return true;
        }

        public async Task<bool> MarcarNaoEntregueAsync(int pedidoId, int entregadorUserId, string? motivo = null)
        {
            var pedido = await ObterPedidoComEntregaAsync(pedidoId);
            if (pedido == null || !EntregaPertenceAoUsuario(pedido, entregadorUserId) || pedido.Entrega == null)
                return false;

            if (!PodeMarcarNaoEntregue(pedido.Entrega.Status))
                return false;

            if (string.IsNullOrWhiteSpace(motivo))
                return false;

            var motivoFinal = motivo.Trim();

            pedido.Entrega.Status = EntregaStatusConst.NaoEntregue;
            pedido.Entrega.DataAtualizacao = DateTime.Now;
            pedido.Entrega.Observacao = motivoFinal;
            pedido.Entrega.NomeRecebedor = null;
            pedido.Entrega.ObservacaoEntrega = null;
            pedido.Entrega.ComprovanteEntregaUrl = null;

            // devolve o pedido para o lojista reatribuir
            pedido.EntregadorId = null;
            pedido.DataAtribuicaoEntregador = null;

            await _context.SaveChangesAsync();

            await _pedidoAppService.AtualizarStatusAsync(
                pedido.Id,
                PedidoEntregaStatus.AguardandoAtribuicao,
                nomeResponsavel: pedido.Entregador?.Nome,
                usuarioResponsavelId: entregadorUserId.ToString(),
                observacao: $"Tentativa de entrega sem sucesso. Motivo: {motivoFinal}",
                origem: "PainelEntregador");

            return true;
        }

        private async Task<PedidoModel?> ObterPedidoComEntregaAsync(int pedidoId)
        {
            return await _context.Pedidos
                .Include(x => x.Entrega)
                .Include(x => x.Entregador)
                    .ThenInclude(x => x!.Usuario)
                .FirstOrDefaultAsync(x => x.Id == pedidoId);
        }

        private static bool EntregaPertenceAoUsuario(PedidoModel pedido, int entregadorUserId)
        {
            return pedido.Entregador?.Usuario != null &&
                   pedido.Entregador.Usuario.Id == entregadorUserId;
        }

        private static bool StatusEh(string? atual, string esperado)
        {
            return string.Equals(atual, esperado, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PodeMarcarNaoEntregue(string? status)
        {
            return StatusEh(status, EntregaStatusConst.Atribuida)
                || StatusEh(status, EntregaStatusConst.Aceita)
                || StatusEh(status, EntregaStatusConst.Coletada)
                || StatusEh(status, EntregaStatusConst.EmRota);
        }
    }
}