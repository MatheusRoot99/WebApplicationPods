using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplicationPods.Models
{
    public class ProdutoModel
    {
        public int Id { get; set; }

        // ===== MULTI-LOJA =====
        public int LojaId { get; set; }

        // ===== Conveniência (genérico) =====
        [Required(ErrorMessage = "O nome do produto é obrigatório")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        public bool RequerMaioridade { get; set; } = false;


        [StringLength(2000)]
        public string? Descricao { get; set; }

        [StringLength(80)]
        public string? Marca { get; set; }

        [StringLength(40)]
        public string? SKU { get; set; }

        [StringLength(30)]
        public string? CodigoBarras { get; set; }

        [Required(ErrorMessage = "O preço do produto é obrigatório")]
        [Range(0.01, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Preco { get; set; }

        [Display(Name = "Preço Promocional")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PrecoPromocional { get; set; }

        [Display(Name = "URL da Imagem")]
        [ValidateNever]
        public string? ImagemUrl { get; set; }

        [NotMapped]
        [Display(Name = "Imagem do Produto")]
        [ValidateNever]
        public IFormFile? ImagemUpload { get; set; }

        [Required(ErrorMessage = "A categoria é obrigatória")]
        [Display(Name = "Categoria")]
        public int CategoriaId { get; set; }

        [ForeignKey("CategoriaId")]
        [ValidateNever]
        public virtual CategoriaModel Categoria { get; set; } = default!;

        [Range(0, int.MaxValue)]
        public int Estoque { get; set; } = 0;

        [Display(Name = "Data de Cadastro")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        public bool EmPromocao { get; set; }
        public bool MaisVendido { get; set; }
        public bool Ativo { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Custo { get; set; }

        [ValidateNever]
        public virtual ICollection<PedidoItemModel> PedidoItens { get; set; } = new List<PedidoItemModel>();
        [ValidateNever]
        public ICollection<ProdutoVariacaoModel> Variacoes { get; set; } = new List<ProdutoVariacaoModel>();

        // ===== Pods (legado) - mantém, mas não obriga no novo fluxo =====
        [Display(Name = "Sabor")]
        [StringLength(50)]
        public string Sabor { get; set; } = string.Empty;

        [Display(Name = "Cor")]
        [StringLength(30)]
        public string? Cor { get; set; }

        [Display(Name = "Avaliação")]
        [Range(0, 5)]
        public double Avaliacao { get; set; } = 0;

        [Display(Name = "Quantidade de Puffs")]
        public int Puffs { get; set; }

        [Display(Name = "Bateria (mAh)")]
        public int CapacidadeBateria { get; set; }

        // ======= Sabores com Quantidade (agora OPCIONAL) =======
        [Display(Name = "Sabores e Quantidades")]
        [Column("SaboresQuantidades")]
        public string? SaboresQuantidades { get; set; }

        [NotMapped]
        [ValidateNever]
        public List<SaborQuantidade> SaboresQuantidadesList { get; set; } = new();

        [NotMapped]
        [ValidateNever]
        public List<SelectListItem> TodosSabores { get; set; } = new();

        // ===== Atributos genéricos (conveniência) =====
        public ICollection<ProdutoAtributoModel> Atributos { get; set; } = new List<ProdutoAtributoModel>();

        // ======= Métodos Auxiliares =======
        public bool EstaEmPromocao() => EmPromocao && PrecoPromocional.HasValue && PrecoPromocional < Preco;

        public void SerializarSaboresQuantidades()
        {
            SaboresQuantidades = JsonConvert.SerializeObject(SaboresQuantidadesList ?? new List<SaborQuantidade>());
        }

        public void DeserializarSaboresQuantidades()
        {
            try
            {
                SaboresQuantidadesList = !string.IsNullOrEmpty(SaboresQuantidades)
                    ? JsonConvert.DeserializeObject<List<SaborQuantidade>>(SaboresQuantidades) ?? new()
                    : new();
            }
            catch
            {
                SaboresQuantidadesList = new();
            }
        }

        public class SaborQuantidade
        {
            [JsonProperty("sabor")]
            public string Sabor { get; set; } = string.Empty;

            [JsonProperty("quantidade")]
            public int Quantidade { get; set; } = 0;
        }
    }
}
