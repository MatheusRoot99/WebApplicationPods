using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebApplicationPods.Data;
using WebApplicationPods.DTO;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Interface;

namespace WebApplicationPods.Repositories
{
    public class CarrinhoRepository : ICarrinhoRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IProdutoRepository _produtoRepository;
        private readonly ILogger<CarrinhoRepository> _logger;
        private const string CarrinhoSessionKey = "Carrinho";
        private readonly BancoContext _context;

        public CarrinhoRepository(IHttpContextAccessor httpContextAccessor, IProdutoRepository produtoRepository, ILogger<CarrinhoRepository> logger, BancoContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _produtoRepository = produtoRepository;
            _logger = logger;
            _context = context;
        }

        public CarrinhoModel ObterCarrinho()
        {
            var session = _httpContextAccessor.HttpContext.Session.GetString(CarrinhoSessionKey);

            if (string.IsNullOrEmpty(session))
            {
                return new CarrinhoModel();
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var carrinhoDTO = JsonSerializer.Deserialize<CarrinhoDTO>(session, options);

                var carrinho = new CarrinhoModel();

                if (carrinhoDTO?.Itens != null)
                {
                    foreach (var itemDTO in carrinhoDTO.Itens)
                    {
                        var produto = _produtoRepository.ObterPorId(itemDTO.ProdutoId);
                        if (produto != null)
                        {
                            carrinho.Itens.Add(new CarrinhoItemViewModel
                            {
                                Produto = produto,
                                Quantidade = itemDTO.Quantidade,
                                PrecoUnitario = itemDTO.PrecoUnitario,
                                Observacoes = itemDTO.Observacoes,
                                Sabor = itemDTO.Sabor
                            });
                        }
                    }
                }

                return carrinho;
            }
            catch (JsonException ex)
            {
                // Log the error if needed
                Console.WriteLine($"Erro ao desserializar carrinho: {ex.Message}");
                return new CarrinhoModel();
            }
        }

        public void SalvarCarrinho(CarrinhoModel carrinho)
        {
            _logger.LogInformation($"Salvando carrinho com {carrinho.Itens.Count} itens");
            var carrinhoDTO = new CarrinhoDTO
            {
                Total = carrinho.Total,
                Itens = carrinho.Itens.Select(i => new CarrinhoItemDTO
                {
                    ProdutoId = i.Produto.Id,
                    Nome = i.Produto.Nome,
                    PrecoUnitario = i.PrecoUnitario,
                    ImagemUrl = i.Produto.ImagemUrl,
                    Quantidade = i.Quantidade,
                    Sabor = i.Sabor,
                    Observacoes = i.Observacoes
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var session = JsonSerializer.Serialize(carrinhoDTO, options);
            _httpContextAccessor.HttpContext.Session.SetString(CarrinhoSessionKey, session);
        }

        public void AdicionarItem(ProdutoModel produto, int quantidade, string sabor = null, string observacoes = null)
        {
            if (produto == null)
                throw new ArgumentNullException(nameof(produto));

            if (quantidade <= 0)
                throw new ArgumentException("Quantidade deve ser maior que zero", nameof(quantidade));

            var carrinho = ObterCarrinho();

            // Verifica se já existe um item com mesmo produto e sabor
            var itemExistente = carrinho.Itens.FirstOrDefault(i =>
                i.Produto.Id == produto.Id &&
                i.Sabor == sabor);

            if (itemExistente != null)
            {
                itemExistente.Quantidade += quantidade;
                if (!string.IsNullOrEmpty(observacoes))
                {
                    itemExistente.Observacoes = observacoes;
                }
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
                    // Adiciona a imagem principal do produto
                    ImagemUrl = produto.ImagemUrl
                });
            }

            SalvarCarrinho(carrinho);
        }

        public void AtualizarItem(ProdutoModel produto, int quantidade, string sabor = null)
        {
            var carrinho = ObterCarrinho();
            var item = carrinho.Itens.FirstOrDefault(i =>
                i.Produto.Id == produto.Id &&
                i.Sabor == sabor);

            if (item != null)
            {
                item.Quantidade = quantidade;

                // Se a quantidade for zero ou negativa, remove o item
                if (item.Quantidade <= 0)
                {
                    carrinho.Itens.Remove(item);
                }

                SalvarCarrinho(carrinho);
            }
        }

        public void RemoverItem(int produtoId, string sabor = null)
        {
            var carrinho = ObterCarrinho();
            var item = carrinho.Itens.FirstOrDefault(i =>
                i.Produto.Id == produtoId &&
                i.Sabor == sabor);

            if (item != null)
            {
                carrinho.Itens.Remove(item);
                SalvarCarrinho(carrinho);
            }
        }

        public void LimparCarrinho()
        {
            _httpContextAccessor.HttpContext.Session.Remove(CarrinhoSessionKey);
        }


        public CarrinhoModel ObterCarrinhoPorTelefone(string telefone)
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