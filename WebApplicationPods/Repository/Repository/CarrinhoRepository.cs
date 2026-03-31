using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebApplicationPods.Data;
using WebApplicationPods.DTO;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Repositories
{
    public class CarrinhoRepository : ICarrinhoRepository
    {
        private readonly IHttpContextAccessor _http;
        private readonly IProdutoRepository _produtoRepository;
        private readonly ILogger<CarrinhoRepository> _logger;
        private readonly BancoContext _context;
        private readonly ICurrentLojaService _currentLoja;
        private readonly IWebHostEnvironment _env;

        private const string CarrinhoSessionKeyPrefix = "Carrinho_";
        private const int LojaFallbackDev = 0;

        public CarrinhoRepository(
            IHttpContextAccessor httpContextAccessor,
            IProdutoRepository produtoRepository,
            ILogger<CarrinhoRepository> logger,
            BancoContext context,
            ICurrentLojaService currentLoja,
            IWebHostEnvironment env)
        {
            _http = httpContextAccessor;
            _produtoRepository = produtoRepository;
            _logger = logger;
            _context = context;
            _currentLoja = currentLoja;
            _env = env;
        }

        // ===================== Helpers =====================

        private bool IgnorarLojaNoAmbienteAtual()
            => _env.IsDevelopment();

        private int GetLojaIdOrFallback()
        {
            if (_currentLoja?.LojaId is int lojaId && lojaId > 0)
                return lojaId;

            if (IgnorarLojaNoAmbienteAtual())
                return LojaFallbackDev;

            throw new InvalidOperationException("Loja atual não definida. Verifique o middleware multi-loja.");
        }

        private string GetSessionKey()
        {
            var lojaId = GetLojaIdOrFallback();
            return $"{CarrinhoSessionKeyPrefix}{lojaId}";
        }

        private ISession SessionOrFail()
        {
            var session = _http.HttpContext?.Session;
            if (session == null)
                throw new InvalidOperationException("Sessão não disponível. Verifique UseSession().");

            return session;
        }

        private static string NormSabor(string? s) => (s ?? "").Trim();

        // ===================== Public =====================

        public CarrinhoModel ObterCarrinho()
        {
            var lojaId = GetLojaIdOrFallback();
            var sessionKey = GetSessionKey();
            var session = SessionOrFail();

            var json = session.GetString(sessionKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CarrinhoModel
                {
                    LojaId = lojaId,
                    SessionId = _http.HttpContext?.Session?.Id ?? string.Empty
                };
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var carrinhoDTO = JsonSerializer.Deserialize<CarrinhoDTO>(json, options);

                var carrinho = new CarrinhoModel
                {
                    LojaId = lojaId,
                    SessionId = _http.HttpContext?.Session?.Id ?? string.Empty
                };

                if (carrinhoDTO?.Itens != null)
                {
                    foreach (var itemDTO in carrinhoDTO.Itens)
                    {
                        var produto = _produtoRepository.ObterPorId(itemDTO.ProdutoId);
                        if (produto == null)
                            continue;

                        // Em produção: protege multi-loja
                        if (!IgnorarLojaNoAmbienteAtual())
                        {
                            if (produto.LojaId != 0 && produto.LojaId != lojaId)
                                continue;
                        }

                        carrinho.Itens.Add(new CarrinhoItemViewModel
                        {
                            Produto = produto,
                            Quantidade = itemDTO.Quantidade,
                            PrecoUnitario = itemDTO.PrecoUnitario,
                            Observacoes = itemDTO.Observacoes,
                            Sabor = itemDTO.Sabor,
                            ImagemUrl = itemDTO.ImagemUrl
                        });
                    }
                }

                return carrinho;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Erro ao desserializar carrinho da sessão. Limpando carrinho.");

                session.Remove(sessionKey);

                return new CarrinhoModel
                {
                    LojaId = lojaId,
                    SessionId = _http.HttpContext?.Session?.Id ?? string.Empty
                };
            }
        }

        public void SalvarCarrinho(CarrinhoModel carrinho)
        {
            var lojaId = GetLojaIdOrFallback();
            var sessionKey = GetSessionKey();
            var session = SessionOrFail();

            carrinho.LojaId = lojaId;

            _logger.LogInformation("Salvando carrinho LojaId={LojaId} com {Count} itens",
                lojaId, carrinho.Itens?.Count ?? 0);

            var carrinhoDTO = new CarrinhoDTO
            {
                Total = carrinho.Total,
                Itens = (carrinho.Itens ?? new List<CarrinhoItemViewModel>())
                    .Select(i => new CarrinhoItemDTO
                    {
                        ProdutoId = i.Produto.Id,
                        Nome = i.Produto.Nome,
                        PrecoUnitario = i.PrecoUnitario,
                        ImagemUrl = i.Produto.ImagemUrl,
                        Quantidade = i.Quantidade,
                        Sabor = i.Sabor,
                        Observacoes = i.Observacoes
                    })
                    .ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(carrinhoDTO, options);
            session.SetString(sessionKey, json);
        }

        public void AdicionarItem(ProdutoModel produto, int quantidade, string? sabor = null, string? observacoes = null)
        {
            if (produto == null)
                throw new ArgumentNullException(nameof(produto));

            if (quantidade <= 0)
                throw new ArgumentException("Quantidade deve ser maior que zero", nameof(quantidade));

            var lojaId = GetLojaIdOrFallback();

            // Só valida loja em produção
            if (!IgnorarLojaNoAmbienteAtual())
            {
                if (produto.LojaId != 0 && produto.LojaId != lojaId)
                    throw new InvalidOperationException("Produto não pertence à loja atual.");
            }

            var carrinho = ObterCarrinho();
            var saborKey = NormSabor(sabor);

            var itemExistente = carrinho.Itens.FirstOrDefault(i =>
                i.Produto.Id == produto.Id &&
                string.Equals(NormSabor(i.Sabor), saborKey, StringComparison.OrdinalIgnoreCase));

            if (itemExistente != null)
            {
                itemExistente.Quantidade += quantidade;

                if (!string.IsNullOrWhiteSpace(observacoes))
                    itemExistente.Observacoes = observacoes;
            }
            else
            {
                carrinho.Itens.Add(new CarrinhoItemViewModel
                {
                    Produto = produto,
                    Quantidade = quantidade,
                    PrecoUnitario = produto.PrecoPromocional ?? produto.Preco,
                    Observacoes = observacoes,
                    Sabor = sabor,
                    ImagemUrl = produto.ImagemUrl
                });
            }

            SalvarCarrinho(carrinho);
        }

        public void AtualizarItem(ProdutoModel produto, int quantidade, string? sabor = null)
        {
            if (produto == null)
                throw new ArgumentNullException(nameof(produto));

            var carrinho = ObterCarrinho();
            var saborKey = NormSabor(sabor);

            var item = carrinho.Itens.FirstOrDefault(i =>
                i.Produto.Id == produto.Id &&
                string.Equals(NormSabor(i.Sabor), saborKey, StringComparison.OrdinalIgnoreCase));

            if (item == null)
                return;

            item.Quantidade = quantidade;

            if (item.Quantidade <= 0)
                carrinho.Itens.Remove(item);

            SalvarCarrinho(carrinho);
        }

        public void RemoverItem(int produtoId, string? sabor = null)
        {
            var carrinho = ObterCarrinho();
            var saborKey = NormSabor(sabor);

            var item = carrinho.Itens.FirstOrDefault(i =>
                i.Produto.Id == produtoId &&
                string.Equals(NormSabor(i.Sabor), saborKey, StringComparison.OrdinalIgnoreCase));

            if (item == null)
                return;

            carrinho.Itens.Remove(item);
            SalvarCarrinho(carrinho);
        }

        public void LimparCarrinho()
        {
            var sessionKey = GetSessionKey();
            var session = SessionOrFail();
            session.Remove(sessionKey);
        }

        // =====================
        // Extras
        // =====================

        public CarrinhoModel? ObterCarrinhoPorTelefone(string telefone)
        {
            return _context.Carrinhos
                .Include(c => c.Itens)
                .ThenInclude(i => i.Produto)
                .FirstOrDefault(c => c.ClienteTelefone == telefone);
        }

        public void Atualizar(CarrinhoModel carrinho)
        {
            _context.Carrinhos.Update(carrinho);
            _context.SaveChanges();
        }

        public void Remover(CarrinhoModel carrinho)
        {
            _context.Carrinhos.Remove(carrinho);
            _context.SaveChanges();
        }
    }
}