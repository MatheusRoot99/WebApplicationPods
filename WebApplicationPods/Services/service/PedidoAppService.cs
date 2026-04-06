using Microsoft.AspNetCore.SignalR;
using System.Globalization;
using WebApplicationPods.Constants;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class PedidoAppService : IPedidoAppService
    {
        private readonly IPedidoRepository _pedidoRepository;
        private readonly IHubContext<PedidosHub> _hub;
        private readonly INotificationAppService _notificationAppService;
        private readonly IWhatsAppService _whatsAppService;

        public PedidoAppService(
            IPedidoRepository pedidoRepository,
            IHubContext<PedidosHub> hub,
            INotificationAppService notificationAppService,
            IWhatsAppService whatsAppService)
        {
            _pedidoRepository = pedidoRepository;
            _hub = hub;
            _notificationAppService = notificationAppService;
            _whatsAppService = whatsAppService;
        }

        public async Task<PedidoModel> CriarPedidoAsync(PedidoModel pedido, string? origem = null)
        {
            if (pedido == null)
                throw new ArgumentNullException(nameof(pedido));

            if (pedido.PedidoItens == null || !pedido.PedidoItens.Any())
                throw new ArgumentException("O pedido precisa ter itens.");

            _pedidoRepository.Adicionar(pedido);

            var pedidoCriado = _pedidoRepository.ObterPorId(pedido.Id) ?? pedido;

            var group = pedidoCriado.LojaId > 0
                ? PedidosHub.LojaGroup(pedidoCriado.LojaId)
                : PedidosHub.GlobalLojistasGroup;

            await _hub.Clients.Group(group).SendAsync("NewOrder", new
            {
                id = pedidoCriado.Id,
                status = pedidoCriado.Status,
                total = pedidoCriado.ValorTotal,
                metodo = pedidoCriado.MetodoPagamento,
                data = pedidoCriado.DataPedido,
                cliente = new
                {
                    id = pedidoCriado.ClienteId,
                    nome = pedidoCriado.Cliente?.Nome ?? $"Cliente #{pedidoCriado.ClienteId}"
                },
                origem = origem
            });

            await _hub.Clients.Group(group).SendAsync("PedidosChanged", new
            {
                id = pedidoCriado.Id,
                status = pedidoCriado.Status
            });

            await CriarNotificacaoNovoPedidoAsync(pedidoCriado);
            await DispararWhatsAppNovoPedidoAsync(pedidoCriado);

            return pedidoCriado;
        }

        public async Task<bool> AtualizarStatusAsync(
            int pedidoId,
            string novoStatus,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null)
        {
            var pedido = _pedidoRepository.ObterPorId(pedidoId);
            if (pedido == null)
                return false;

            if (string.Equals(pedido.Status, novoStatus, StringComparison.OrdinalIgnoreCase))
                return true;

            _pedidoRepository.AtualizarStatus(
                pedidoId,
                novoStatus,
                nomeResponsavel,
                usuarioResponsavelId,
                observacao,
                origem);

            var atualizado = _pedidoRepository.ObterPorId(pedidoId);
            if (atualizado == null)
                return false;

            var group = atualizado.LojaId > 0
                ? PedidosHub.LojaGroup(atualizado.LojaId)
                : PedidosHub.GlobalLojistasGroup;

            await _hub.Clients.Group(group).SendAsync("PedidosChanged", new
            {
                id = atualizado.Id,
                status = atualizado.Status
            });

            await CriarNotificacaoStatusAsync(atualizado, novoStatus);
            await DispararWhatsAppPorStatusAsync(atualizado, novoStatus);

            return true;
        }

        public async Task<bool> MarcarComoPagoAsync(
            int pedidoId,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null)
        {
            return await AtualizarStatusAsync(
                pedidoId,
                PedidoStatus.Pago,
                nomeResponsavel,
                usuarioResponsavelId,
                observacao,
                origem);
        }

        private Task CriarNotificacaoNovoPedidoAsync(PedidoModel pedido)
        {
            if (pedido.LojaId <= 0)
                return Task.CompletedTask;

            var cliente = pedido.Cliente?.Nome ?? $"Cliente #{pedido.ClienteId}";
            var total = pedido.ValorTotal.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

            return _notificationAppService.CriarAsync(
                pedido.LojaId,
                $"Novo pedido #{pedido.Id}",
                $"{cliente} fez um pedido no valor de {total}.",
                tipo: "novo-pedido",
                pedidoId: pedido.Id);
        }

        private Task CriarNotificacaoStatusAsync(PedidoModel pedido, string novoStatus)
        {
            if (pedido.LojaId <= 0 || string.IsNullOrWhiteSpace(novoStatus))
                return Task.CompletedTask;

            string? tipo = null;
            string? titulo = null;
            string? mensagem = null;

            if (string.Equals(novoStatus, PedidoStatus.AguardandoConfirmacaoDinheiro, StringComparison.OrdinalIgnoreCase))
            {
                tipo = "pagamento";
                titulo = $"Pedido #{pedido.Id} aguardando confirmação";
                mensagem = "Pagamento em dinheiro pendente de confirmação manual.";
            }
            else if (string.Equals(novoStatus, PedidoStatus.Pago, StringComparison.OrdinalIgnoreCase))
            {
                tipo = "pagamento";
                titulo = $"Pedido #{pedido.Id} pago";
                mensagem = "O pagamento do pedido foi confirmado.";
            }
            else if (string.Equals(novoStatus, PedidoStatus.PagamentoFalhou, StringComparison.OrdinalIgnoreCase))
            {
                tipo = "pagamento";
                titulo = $"Pagamento do pedido #{pedido.Id} falhou";
                mensagem = "O pedido teve falha na confirmação do pagamento.";
            }
            else if (string.Equals(novoStatus, PedidoStatus.Cancelado, StringComparison.OrdinalIgnoreCase))
            {
                tipo = "cancelamento";
                titulo = $"Pedido #{pedido.Id} cancelado";
                mensagem = "O pedido foi cancelado.";
            }

            if (string.IsNullOrWhiteSpace(tipo) ||
                string.IsNullOrWhiteSpace(titulo) ||
                string.IsNullOrWhiteSpace(mensagem))
            {
                return Task.CompletedTask;
            }

            return _notificationAppService.CriarAsync(
                pedido.LojaId,
                titulo,
                mensagem,
                tipo,
                pedidoId: pedido.Id);
        }

        private async Task DispararWhatsAppNovoPedidoAsync(PedidoModel pedido)
        {
            await _whatsAppService.EnviarNovoPedidoClienteAsync(pedido);
            await _whatsAppService.EnviarNovoPedidoLojistaAsync(pedido);
        }

        private async Task DispararWhatsAppPorStatusAsync(PedidoModel pedido, string novoStatus)
        {
            if (string.IsNullOrWhiteSpace(novoStatus))
                return;

            if (string.Equals(novoStatus, PedidoStatus.Pago, StringComparison.OrdinalIgnoreCase))
            {
                await _whatsAppService.EnviarPagamentoAprovadoClienteAsync(pedido);
                await _whatsAppService.EnviarPagamentoAprovadoLojistaAsync(pedido);
                return;
            }

            if (string.Equals(novoStatus, PedidoStatus.PagamentoFalhou, StringComparison.OrdinalIgnoreCase))
            {
                await _whatsAppService.EnviarPagamentoFalhouClienteAsync(pedido);
                return;
            }

            if (string.Equals(novoStatus, PedidoStatus.EmPreparacao, StringComparison.OrdinalIgnoreCase))
            {
                await _whatsAppService.EnviarPedidoEmPreparacaoClienteAsync(pedido);
                return;
            }

            if (string.Equals(novoStatus, PedidoStatus.Pronto, StringComparison.OrdinalIgnoreCase))
            {
                await _whatsAppService.EnviarPedidoProntoClienteAsync(pedido);
                return;
            }

            if (string.Equals(novoStatus, PedidoStatus.Cancelado, StringComparison.OrdinalIgnoreCase))
            {
                await _whatsAppService.EnviarPedidoCanceladoClienteAsync(pedido);
                return;
            }

            if (string.Equals(novoStatus, PedidoStatus.SaiuParaEntrega, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(novoStatus, PedidoEntregaStatus.SaiuParaEntrega, StringComparison.OrdinalIgnoreCase))
            {
                await _whatsAppService.EnviarSaiuParaEntregaClienteAsync(pedido);
                return;
            }

            if (string.Equals(novoStatus, PedidoEntregaStatus.Entregue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(novoStatus, PedidoStatus.Concluido, StringComparison.OrdinalIgnoreCase))
            {
                await _whatsAppService.EnviarPedidoEntregueClienteAsync(pedido);
            }
        }
    }
}