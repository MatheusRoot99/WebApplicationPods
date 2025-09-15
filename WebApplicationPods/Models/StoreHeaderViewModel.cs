using System;

namespace WebApplicationPods.Models
{
    public class StoreHeaderViewModel
    {
        public string Nome { get; set; } = "Minha Loja";
        public string? LogoUrl { get; set; }
        public bool AbertoAgora { get; set; }
        public TimeSpan? FechaAs { get; set; } // usado só para exibir “até 23h59”
        public string? PerfilDaLojaUrl { get; set; }
        public string? UrlParaCompartilhar { get; set; }
        public string? MensagemStatus { get; set; }
    }
}
