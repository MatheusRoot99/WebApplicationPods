using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using WebApplicationPods.Validation;

namespace WebApplicationPods.Models
{
    public class ClienteModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome do cliente é obrigatório")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "O email é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [StringLength(100, ErrorMessage = "O email deve ter no máximo 100 caracteres")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Formato de telefone inválido")]
        public string Telefone { get; set; }

        [Display(Name = "Data de Cadastro")]
        [DataType(DataType.DateTime)]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        // ========= NOVO: CPF =========
        private string _cpf;

        /// <summary>
        /// Armazenado somente com dígitos (11). Use CpfFormatado para exibir.
        /// </summary>
        [Cpf] // valida estrutura/dígitos verificadores
        [StringLength(11, ErrorMessage = "CPF deve conter 11 dígitos")]
        [Display(Name = "CPF")]
        public string? Cpf
        {
            get => _cpf;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _cpf = null;
                    return;
                }
                var dig = Regex.Replace(value, "[^0-9]", "");
                _cpf = string.IsNullOrWhiteSpace(dig) ? null : dig;
            }
        }

        [ValidateNever]
        public string CpfFormatado =>
            string.IsNullOrWhiteSpace(Cpf) || Cpf.Length != 11
                ? (Cpf ?? string.Empty)
                : $"{Cpf.Substring(0, 3)}.{Cpf.Substring(3, 3)}.{Cpf.Substring(6, 3)}-{Cpf.Substring(9, 2)}";

        // Relacionamentos
        public ICollection<EnderecoModel> Enderecos { get; set; } = new List<EnderecoModel>();
        public ICollection<PedidoModel> Pedidos { get; set; }
    }
}
