namespace WebApplicationPods.DTO
{
    public class ReportsDTO
    {
        public class ResumoVendas
        {
            public int Recebidos { get; set; }
            public int Pagos { get; set; }
            public decimal TotalVendido { get; set; }
        }

        public class SerieDia
        {
            public DateTime Dia { get; set; }
            public int Quantidade { get; set; }
            public decimal Total { get; set; }
        }

        public class MetodoPagamentoResumo
        {
            public string Metodo { get; set; } = "";
            public int Quantidade { get; set; }
            public decimal Total { get; set; }
        }

        public class TopClienteResumo
        {
            public int ClienteId { get; set; }
            public string? Nome { get; set; }
            public int Quantidade { get; set; }
            public decimal Total { get; set; }
        }

        public class AdminReportViewModel
        {
            public string PeriodoDescricao { get; set; } = "";
            public DateTime Inicio { get; set; }
            public DateTime Fim { get; set; }

            public ResumoVendas Resumo { get; set; } = new();
            public List<SerieDia> SeriePorDia { get; set; } = new();
            public List<MetodoPagamentoResumo> Metodos { get; set; } = new();
            public List<TopClienteResumo> TopClientes { get; set; } = new();
        }
    }
}
