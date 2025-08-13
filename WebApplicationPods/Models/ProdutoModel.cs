using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SitePodsInicial.Models
{
    public class ProdutoModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome do produto é obrigatório")]
        [StringLength(100, ErrorMessage = "O nome do produto deve ter no máximo 100 caracteres")]
        public string Nome { get; set; }

        [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres")]
        public string Descricao { get; set; }

        [Required(ErrorMessage = "O preço do produto é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "O preço deve ser maior que zero")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Preco { get; set; }

        [Display(Name = "Preço Promocional")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecoPromocional { get; set; }

        [Display(Name = "URL da Imagem")]
        [ValidateNever]
        public string ImagemUrl { get; set; }

        [NotMapped]
        [Display(Name = "Imagem do Produto")]
        public IFormFile ImagemUpload { get; set; }

        [Required(ErrorMessage = "A categoria é obrigatória")]
        [Display(Name = "Categoria")]
        public int CategoriaId { get; set; }

        [ForeignKey("CategoriaId")]
        [ValidateNever]
        public virtual CategoriaModel Categoria { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "O estoque não pode ser negativo")]
        public int Estoque { get; set; } = 0;

        [Display(Name = "Sabor")]
        [StringLength(50)]
        [ValidateNever]
        public string Sabor { get; set; } = string.Empty;

        [Display(Name = "Cor")]
        [StringLength(30)]
        public string Cor { get; set; }

        [Display(Name = "Avaliação")]
        [Range(0, 5)]
        public double Avaliacao { get; set; } = 0;

        [Display(Name = "Quantidade de Puffs")]
        public int Puffs { get; set; }

        [Display(Name = "Bateria (mAh)")]
        public int CapacidadeBateria { get; set; }

        [Display(Name = "Data de Cadastro")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        [Display(Name = "Em Promoção?")]
        public bool EmPromocao { get; set; }

        [Display(Name = "Mais Vendido?")]
        public bool MaisVendido { get; set; }

        [Display(Name = "Ativo?")]
        public bool Ativo { get; set; } = true;

        [ValidateNever]
        public virtual ICollection<PedidoItemModel> PedidoItens { get; set; }

        // Método para verificar se está em promoção
        public bool EstaEmPromocao() => EmPromocao && PrecoPromocional.HasValue && PrecoPromocional < Preco;

        // Método para calcular o preço com desconto à vista
        public decimal PrecoAVista(decimal percentualDesconto = 0.1m)
        {
            var precoBase = EstaEmPromocao() ? PrecoPromocional.Value : Preco;
            return precoBase * (1 - percentualDesconto);
        }

        // Método para calcular parcelamento
        public decimal ValorParcela(int numeroParcelas)
        {
            var precoBase = EstaEmPromocao() ? PrecoPromocional.Value : Preco;
            return precoBase / numeroParcelas;
        }


        // ... outras propriedades existentes ...

        [Display(Name = "Sabores Disponíveis")]
        [ValidateNever] // Adicione esta anotação para evitar validação
        public string SaboresDisponiveis { get; set; } = string.Empty;// Armazenará os sabores como JSON

        [Display(Name = "Sabores e Quantidades")]
        [Column("SaboresQuantidades")]
        [Required(ErrorMessage = "Adicione pelo menos um sabor com quantidade válida.")]
        public string SaboresQuantidades { get; set; }

        [NotMapped]
        [ValidateNever]
        public List<SaborQuantidade> SaboresQuantidadesList { get; set; } = new List<SaborQuantidade>();

        [NotMapped]
        [ValidateNever] // Adicione esta anotação para evitar validação
        public List<string> SaboresSelecionados { get; set; } = new List<string>();

        [NotMapped]
        [ValidateNever] // Adicione esta anotação para evitar validação
        public List<SelectListItem> TodosSabores { get; set; } = new List<SelectListItem>();

        // Substitua a classe SaborQuantidade por:
        public class SaborQuantidade
        {
            [JsonProperty("sabor")] // Adicione isso se os nomes forem diferentes
            public string Sabor { get; set; } = string.Empty;
            [JsonProperty("quantidade")]
            public int Quantidade { get; set; } = 0;

            public SaborQuantidade() { }

            public SaborQuantidade(string sabor, int quantidade)
            {
                Sabor = sabor;
                Quantidade = quantidade;
            }
        }

        // Atualize os métodos de serialização:
        public void SerializarSaboresQuantidades()
        {
            SaboresQuantidades = JsonConvert.SerializeObject(SaboresQuantidadesList ?? new List<SaborQuantidade>());
        }

        public void DeserializarSaboresQuantidades()
        {
            try
            {
                SaboresQuantidadesList = !string.IsNullOrEmpty(SaboresQuantidades)
                    ? JsonConvert.DeserializeObject<List<SaborQuantidade>>(SaboresQuantidades)
                    : new List<SaborQuantidade>();
            }
            catch
            {
                SaboresQuantidadesList = new List<SaborQuantidade>();
            }
        }

        // Método para validar sabores e quantidades
        public bool ValidarSaboresQuantidades()
        {
            if (SaboresQuantidadesList == null || SaboresQuantidadesList.Count == 0)
                return false;

            return SaboresQuantidadesList.All(sq =>
                !string.IsNullOrWhiteSpace(sq.Sabor) && sq.Quantidade > 0);
        }
    }
}