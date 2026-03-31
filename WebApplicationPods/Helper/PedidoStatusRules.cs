using WebApplicationPods.Constants;

namespace WebApplicationPods.Helper
{
    public static class PedidoStatusRules
    {
        public static readonly Dictionary<string, string[]> AllowedTransitions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [PedidoStatus.AguardandoConfirmacaoDinheiro] = new[] { PedidoStatus.EmPreparacao, PedidoStatus.Cancelado },
                [PedidoStatus.Pago] = new[] { PedidoStatus.EmPreparacao, PedidoStatus.Cancelado },
                [PedidoStatus.EmPreparacao] = new[] { PedidoStatus.Pronto, PedidoStatus.Cancelado },
                [PedidoStatus.Pronto] = new[] { PedidoStatus.SaiuParaEntrega, PedidoStatus.Concluido, PedidoStatus.Cancelado },
                [PedidoStatus.SaiuParaEntrega] = new[] { PedidoStatus.Concluido, PedidoStatus.Cancelado },
                [PedidoStatus.AguardandoPagamentoEntrega] = new[] { PedidoStatus.Pago, PedidoStatus.Cancelado },
                [PedidoStatus.AguardandoPagamento] = new[] { PedidoStatus.Pago, PedidoStatus.Cancelado },
                [PedidoStatus.Concluido] = Array.Empty<string>(),
                [PedidoStatus.Cancelado] = Array.Empty<string>(),
                [PedidoStatus.PagamentoFalhou] = Array.Empty<string>(),
                [PedidoStatus.Pendente] = new[] { PedidoStatus.AguardandoPagamento, PedidoStatus.Pago, PedidoStatus.Cancelado }
            };

        public static bool PodeTransicionar(string atual, string proximo)
        {
            if (string.IsNullOrWhiteSpace(atual) || string.IsNullOrWhiteSpace(proximo))
                return false;

            if (!AllowedTransitions.TryGetValue(atual, out var proximos))
                return false;

            return proximos.Contains(proximo, StringComparer.OrdinalIgnoreCase);
        }
    }
}