using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    public class ConfiguracoesViewModel
    {
        [Display(Name = "Nome do usuário")]
        [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
        public string? NomeUsuario { get; set; }

        public string? Email { get; set; }

        [Display(Name = "Telefone")]
        [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
        public string? Telefone { get; set; }

        public string? Cpf { get; set; }

        public string? NomeLoja { get; set; }

        public string? TemaAtual { get; set; }

        [DataType(DataType.Password)]
        public string? SenhaAtual { get; set; }

        [DataType(DataType.Password)]
        public string? NovaSenha { get; set; }

        [DataType(DataType.Password)]
        public string? ConfirmarSenha { get; set; }
    }
}
