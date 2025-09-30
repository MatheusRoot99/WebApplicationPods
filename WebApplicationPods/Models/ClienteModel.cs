// Models/ClienteModel.cs
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore; // para [Index] (EF Core 5+)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using WebApplicationPods.Validation;

namespace WebApplicationPods.Models
{
    // Índice único opcional para CPF (recomendado). Se sua versão do EF não suportar [Index], remova e crie via migration.
    [Index(nameof(Cpf), IsUnique = true, Name = "IX_Clientes_CPF")]
    public class ClienteModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome do cliente é obrigatório")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [StringLength(100, ErrorMessage = "O email deve ter no máximo 100 caracteres")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Formato de telefone inválido")]
        public string Telefone { get; set; } = string.Empty;

        [Display(Name = "Data de Cadastro")]
        [DataType(DataType.DateTime)]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        // ========= CPF e Data de Nascimento =========
        private string? _cpf;

        /// <summary>
        /// CPF armazenado somente com dígitos (11). Use CpfFormatado para exibir.
        /// </summary>
        [Cpf] // atributo custom que valida DV/estrutura
        [StringLength(11, ErrorMessage = "CPF deve conter 11 dígitos")]
        [Display(Name = "CPF")]
        // Se sua coluna no banco chama "CPF", descomente a linha abaixo:
        // [Column("CPF")]
        public string? Cpf
        {
            get => _cpf;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _cpf = null;
                }
                else
                {
                    var dig = Regex.Replace(value, "[^0-9]", "");
                    _cpf = string.IsNullOrWhiteSpace(dig) ? null : dig;
                }
            }
        }

        [Display(Name = "Data de nascimento")]
        [DataType(DataType.Date)]
        public DateTime? DataNascimento { get; set; }

        // ========= Helpers (não mapeados) =========
        [NotMapped, ValidateNever]
        public string CpfFormatado =>
            string.IsNullOrWhiteSpace(Cpf) || Cpf.Length != 11
                ? (Cpf ?? string.Empty)
                : $"{Cpf.Substring(0, 3)}.{Cpf.Substring(3, 3)}.{Cpf.Substring(6, 3)}-{Cpf.Substring(9, 2)}";

        [NotMapped, ValidateNever]
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

        /// <summary>
        /// Alias não-mapeado para compat com código legado que usa "CPF".
        /// NUNCA use este alias em LINQ para o banco; use sempre 'Cpf'.
        /// </summary>
        [NotMapped]
        public string? CPF
        {
            get => Cpf;
            set => Cpf = value;
        }

        // ========= Relacionamentos =========
        public ICollection<EnderecoModel> Enderecos { get; set; } = new List<EnderecoModel>();
        public ICollection<PedidoModel> Pedidos { get; set; } = new List<PedidoModel>();
    }
}
