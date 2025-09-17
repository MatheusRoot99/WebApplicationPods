using System.Globalization;
using WebApplicationPods.Models;

namespace WebApplicationPods.Helper
{
    public class MapsHelper
    {
        public static string BuildMapsUrl(LojaConfig loja)
        {
            // prioridade: lat/lng -> place url -> endereço codificado
            if (loja.Latitude.HasValue && loja.Longitude.HasValue)
                return $"https://www.google.com/maps/dir/?api=1&destination={loja.Latitude.Value.ToString(CultureInfo.InvariantCulture)},{loja.Longitude.Value.ToString(CultureInfo.InvariantCulture)}";

            if (!string.IsNullOrWhiteSpace(loja.MapsPlaceUrl))
                return loja.MapsPlaceUrl;

            var endereco = $"{loja.Logradouro}, {loja.Numero} - {loja.Bairro}, {loja.Cidade} - {loja.Estado}, {loja.Cep}";
            var q = Uri.EscapeDataString(endereco);
            return $"https://www.google.com/maps/dir/?api=1&destination={q}";
        }

        public static string BuildEnderecoTexto(LojaConfig loja)
            => $"{loja.Logradouro}, {loja.Numero} - {loja.Bairro}, {loja.Cidade}/{loja.Estado} - {loja.Cep}".Trim();
    }
}
