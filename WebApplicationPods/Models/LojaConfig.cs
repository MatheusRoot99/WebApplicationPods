using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;

namespace WebApplicationPods.Models
{
    [Flags]
    public enum DiasSemanaFlags
    {
        Nenhum = 0,
        Domingo = 1 << 0,
        Segunda = 1 << 1,
        Terca = 1 << 2,
        Quarta = 1 << 3,
        Quinta = 1 << 4,
        Sexta = 1 << 5,
        Sabado = 1 << 6,
        Todos = Domingo | Segunda | Terca | Quarta | Quinta | Sexta | Sabado
    }

    public class LojaConfig
    {
        public int Id { get; set; }

        // ✅ FK do 1:1 com Loja
        public int LojaId { get; set; }

        // ✅ Navegação (FK será configurada via Fluent no BancoContext)
        public LojaModel? Loja { get; set; }

        // ✅ Lojista (Identity int)
        public int? LojistaUserId { get; set; }
        public ApplicationUser? Lojista { get; set; }

        // Identidade visual
        [Required, StringLength(120)]
        public string Nome { get; set; } = "Minha Loja";

        [StringLength(300)]
        public string? LogoPath { get; set; }

        // Funcionamento
        [Required]
        public TimeSpan HorarioAbertura { get; set; } = new(18, 0, 0);

        [Required]
        public TimeSpan HorarioFechamento { get; set; } = new(23, 59, 0);

        [Required]
        public DiasSemanaFlags DiasAbertos { get; set; } = DiasSemanaFlags.Todos;

        public bool Ativo { get; set; } = true;
        public bool ForcarFechado { get; set; } = false;

        [StringLength(160)]
        public string? MensagemStatus { get; set; }

        // ===== Endereço da LOJA (para retirada) =====
        [StringLength(120)] public string? Logradouro { get; set; }
        [StringLength(30)] public string? Numero { get; set; }
        [StringLength(120)] public string? Complemento { get; set; }
        [StringLength(120)] public string? Bairro { get; set; }
        [StringLength(120)] public string? Cidade { get; set; }
        [StringLength(2)] public string? Estado { get; set; }
        [StringLength(9)] public string? Cep { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [StringLength(500)]
        public string? MapsPlaceUrl { get; set; }

        [NotMapped]
        public string EnderecoTexto =>
            MontarEnderecoTexto(Logradouro, Numero, Complemento, Bairro, Cidade, Estado, Cep);

        [NotMapped]
        public string MapsUrl =>
            MontarMapsUrl(Latitude, Longitude, MapsPlaceUrl, EnderecoTexto);

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public static string MontarEnderecoTexto(
            string? logradouro, string? numero, string? complemento,
            string? bairro, string? cidade, string? estado, string? cep)
        {
            var partes = new[]
            {
                Juntar(", ", logradouro, numero),
                complemento,
                Juntar(" - ", bairro, Juntar("/", cidade, estado)),
                cep
            };

            return string.Join(", ", partes.Where(p => !string.IsNullOrWhiteSpace(p))).Trim();
        }

        public static string MontarMapsUrl(double? lat, double? lng, string? mapsPlaceUrl, string enderecoTexto)
        {
            if (lat.HasValue && lng.HasValue)
            {
                var la = lat.Value.ToString(CultureInfo.InvariantCulture);
                var lo = lng.Value.ToString(CultureInfo.InvariantCulture);
                return $"https://www.google.com/maps/dir/?api=1&destination={la},{lo}";
            }

            if (!string.IsNullOrWhiteSpace(mapsPlaceUrl))
                return mapsPlaceUrl!;

            var q = Uri.EscapeDataString(enderecoTexto ?? "");
            return $"https://www.google.com/maps/dir/?api=1&destination={q}";
        }

        private static string Juntar(string separador, string? a, string? b)
        {
            a = (a ?? "").Trim();
            b = (b ?? "").Trim();
            if (string.IsNullOrWhiteSpace(a)) return b ?? "";
            if (string.IsNullOrWhiteSpace(b)) return a ?? "";
            return $"{a}{separador}{b}";
        }
    }
}
