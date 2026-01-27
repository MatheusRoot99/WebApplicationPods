using Microsoft.AspNetCore.Mvc;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class CepController : Controller
    {
        private readonly ICepService _cepService;

        public CepController(ICepService cepService)
        {
            _cepService = cepService;
        }

        [HttpGet("{cep}")]
        public async Task<IActionResult> Get(string cep)
        {
            // Remove caracteres não numéricos
            cep = new string(cep.Where(char.IsDigit).ToArray());

            if (cep.Length != 8)
                return BadRequest("CEP deve conter 8 dígitos");

            var endereco = await _cepService.BuscarCepAsync(cep);

            if (endereco == null)
                return NotFound("CEP não encontrado");

            return Ok(endereco); // Obs.: normalmente vem com PascalCase (Logradouro, Bairro, Cidade, Estado)
        }
    }
}
