namespace WebApplicationPods.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int LojasAtivas { get; set; }
        public int LojasCriadasMes { get; set; }

        public int LojistasAtivos { get; set; }

        // Receita: por enquanto fica 0 até você me mandar o model/tabela de pedidos pagos
        public decimal ReceitaTotal { get; set; }
        public decimal ReceitaMesAtual { get; set; }

        public List<LojaResumoItem> LojasRecentes { get; set; } = new();
    }

    public class LojaResumoItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string? DonoNome { get; set; }
        public bool Ativa { get; set; }
        public DateTime CriadaEm { get; set; }
    }
}
