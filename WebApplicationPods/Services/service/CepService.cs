using System.Text.Json;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Services.service
{
    public class CepService : ICepService
    {
        private readonly HttpClient _httpClient;

        public CepService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<EnderecoModel?> BuscarCepAsync(string cep)
        {
            var resp = await _httpClient.GetAsync($"{cep}/json/");
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<ViaCepDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null || dto.Erro == true) return null;

            return new EnderecoModel
            {
                CEP = dto.Cep ?? string.Empty,
                Logradouro = dto.Logradouro ?? string.Empty,
                Complemento = dto.Complemento ?? string.Empty,
                Bairro = dto.Bairro ?? string.Empty,
                Cidade = dto.Localidade ?? string.Empty,
                Estado = dto.Uf ?? string.Empty
            };
        }
    }
}