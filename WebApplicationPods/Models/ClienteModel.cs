// Models/ClienteModel.cs
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WebApplicationPods.Validation;

namespace WebApplicationPods.Models
{
    [Index(nameof(Cpf), IsUnique = true, Name = "IX_Clientes_CPF")]
    public class ClienteModel
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string Telefone { get; set; } = string.Empty;

        [DataType(DataType.DateTime)]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        private string? _cpf;

        [Cpf]
        [StringLength(11)]
        [Display(Name = "CPF")]
        // 👇 NÃO force "CPF": deixe por convenção ou especifique "Cpf" (como está no DB)
        // [Column("Cpf")]  // (opcional, só se quiser explicitar)
        [JsonPropertyName("cpf")]
        public string? Cpf
        {
            get => _cpf;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) { _cpf = null; return; }
                var dig = Regex.Replace(value, "[^0-9]", "");
                _cpf = string.IsNullOrWhiteSpace(dig) ? null : dig;
            }
        }

        [DataType(DataType.Date)]
        [Display(Name = "Data de nascimento")]
        public DateTime? DataNascimento { get; set; }

        [NotMapped, ValidateNever, JsonIgnore]
        public string CpfFormatado =>
            string.IsNullOrWhiteSpace(Cpf) || Cpf.Length != 11
                ? (Cpf ?? string.Empty)
                : $"{Cpf[..3]}.{Cpf.Substring(3, 3)}.{Cpf.Substring(6, 3)}-{Cpf.Substring(9, 2)}";

        [NotMapped, ValidateNever, JsonIgnore]
        public int? Idade
        {
            get
            {
                if (DataNascimento == null) return null;
                var hoje = DateTime.Today;
                var idade = hoje.Year - DataNascimento.Value.Year;
                if (DataNascimento.Value.Date > hoje.AddYears(-idade)) idade--;
                return Math.Max(0, idade);
            }
        }

        // Alias legacy — oculto na serialização para não colidir com "Cpf"
        [NotMapped, BindNever, ValidateNever, JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? CPF
        {
            get => Cpf;
            set => Cpf = value;
        }

        [ValidateNever, JsonIgnore]
        public ICollection<EnderecoModel> Enderecos { get; set; } = new List<EnderecoModel>();

        [ValidateNever, JsonIgnore]
        public ICollection<PedidoModel> Pedidos { get; set; } = new List<PedidoModel>();
    }
}
