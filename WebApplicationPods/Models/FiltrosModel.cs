using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class FiltrosModel
    {
        // Filtros básicos
        public string Categoria { get; set; }
        public string Sabor { get; set; }
        public string Cor { get; set; }

        // Filtros de preço
        [Display(Name = "Preço Mínimo")]
        [Range(0, 10000)]
        public decimal? PrecoMin { get; set; }

        [Display(Name = "Preço Máximo")]
        [Range(0, 10000)]
        public decimal? PrecoMax { get; set; }

        // Filtros de avaliação
        [Display(Name = "Avaliação Mínima")]
        [Range(0, 5)]
        public int? AvaliacaoMin { get; set; }

        // Filtros booleanos
        [Display(Name = "Apenas Promoções")]
        public bool ApenasPromocoes { get; set; }

        [Display(Name = "Apenas em Estoque")]
        public bool ApenasEstoque { get; set; }

        // Ordenação
        [Display(Name = "Ordenar Por")]
        public string OrdenarPor { get; set; } = "popularidade";

        // Opções para UI
        public List<string> CategoriasDisponiveis { get; set; }
        public List<string> SaboresDisponiveis { get; set; }
        public List<string> CoresDisponiveis { get; set; }

        public Dictionary<string, string> OpcoesOrdenacao { get; } = new()
        {
            {"popularidade", "Popularidade"},
            {"avaliacao", "Melhor Avaliação"},
            {"recente", "Mais Recentes"},
            {"preco-asc", "Preço: Menor para Maior"},
            {"preco-desc", "Preço: Maior para Menor"}
        };
    }
}