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
            var resp = await _httpClient.GetAsync($"{cep}/json/"); // BaseAddress já está em https://viacep.com.br/ws/
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<ViaCepDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto == null || dto.Erro == true) return null;

            // 🔁 MAPEAMENTO explícito para seu modelo
            return new EnderecoModel
            {
                CEP = dto.Cep,
                Logradouro = dto.Logradouro,
                Complemento = dto.Complemento,
                Bairro = dto.Bairro,
                Cidade = dto.Localidade, // <— aqui está o pulo do gato
                Estado = dto.Uf          // <— idem
            };
        }
    }
}
