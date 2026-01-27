// Models/ViaCepDto.cs
namespace WebApplicationPods.Models
{
    public class ViaCepDto
    {
        public string? Cep { get; set; }          // "86300-464"
        public string? Logradouro { get; set; }   // "Rua Major Pedro Bernardino"
        public string? Complemento { get; set; }  // "(Universitário)"
        public string? Bairro { get; set; }       // "Setor 02"
        public string? Localidade { get; set; }   // "Cornélio Procópio"
        public string? Uf { get; set; }           // "PR"
        public bool? Erro { get; set; }           // quando CEP não existe
    }
}
