using System.Globalization;

namespace WebApplicationPods.Models
{
    public class PainelLojistaDashboardViewModel
    {
        public string LojaNome { get; set; } = "Minha loja";
        public string SaudacaoNome { get; set; } = "Lojista";
        public bool LojaOnline { get; set; }
        public string StatusLojaTexto { get; set; } = "Configuração pendente";
        public string? StatusLojaMensagem { get; set; }

        public int PedidosHoje { get; set; }
        public decimal FaturamentoHoje { get; set; }
        public int PedidosPendentes { get; set; }
        public int EntregasEmAndamento { get; set; }
        public decimal TicketMedio7Dias { get; set; }
        public int ProdutosAtivos { get; set; }

        public string ProdutoMaisVendidoNome { get; set; } = "Sem vendas ainda";
        public int ProdutoMaisVendidoQuantidade { get; set; }

        public int PedidosUltimos7Dias { get; set; }
        public decimal FaturamentoUltimos7Dias { get; set; }

        public List<DashboardSerieDiaViewModel> Serie7Dias { get; set; } = new();
        public List<DashboardStatusResumoViewModel> PedidosPorStatus { get; set; } = new();
        public List<DashboardMovimentacaoViewModel> UltimasMovimentacoes { get; set; } = new();

        public string FaturamentoHojeFormatado => FaturamentoHoje.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
        public string TicketMedio7DiasFormatado => TicketMedio7Dias.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
        public string FaturamentoUltimos7DiasFormatado => FaturamentoUltimos7Dias.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
        public bool TemSerie => Serie7Dias.Count > 0;
    }

    public class DashboardSerieDiaViewModel
    {
        public DateTime Dia { get; set; }
        public int Quantidade { get; set; }
        public decimal Total { get; set; }
    }

    public class DashboardStatusResumoViewModel
    {
        public string Status { get; set; } = string.Empty;
        public int Quantidade { get; set; }
    }

    public class DashboardMovimentacaoViewModel
    {
        public int PedidoId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public DateTime Data { get; set; }
    }
}