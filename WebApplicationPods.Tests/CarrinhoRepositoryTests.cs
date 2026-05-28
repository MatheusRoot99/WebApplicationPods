using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WebApplicationPods.Models;
using WebApplicationPods.Repositories;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Tests;

public class CarrinhoRepositoryTests
{
    [Fact]
    public void Carrinho_fica_separado_por_loja_na_mesma_sessao()
    {
        var session = new FakeSession();
        var currentLoja = new FakeCurrentLojaService();
        var produtos = new FakeProdutoRepository(
            Produto(id: 10, lojaId: 1, nome: "Produto loja A"),
            Produto(id: 20, lojaId: 2, nome: "Produto loja B"));

        currentLoja.SetLojaId(1);
        var repoLojaA = CriarRepositorio(session, currentLoja, produtos);
        repoLojaA.AdicionarItem(produtos.ObterPorId(10)!, 2);

        currentLoja.SetLojaId(2);
        var repoLojaB = CriarRepositorio(session, currentLoja, produtos);
        repoLojaB.AdicionarItem(produtos.ObterPorId(20)!, 1);

        currentLoja.SetLojaId(1);
        var carrinhoA = CriarRepositorio(session, currentLoja, produtos).ObterCarrinho();

        currentLoja.SetLojaId(2);
        var carrinhoB = CriarRepositorio(session, currentLoja, produtos).ObterCarrinho();

        Assert.Equal(1, carrinhoA.LojaId);
        Assert.Single(carrinhoA.Itens);
        Assert.Equal(10, carrinhoA.Itens[0].Produto.Id);
        Assert.Equal(2, carrinhoA.Itens[0].Quantidade);

        Assert.Equal(2, carrinhoB.LojaId);
        Assert.Single(carrinhoB.Itens);
        Assert.Equal(20, carrinhoB.Itens[0].Produto.Id);
        Assert.Equal(1, carrinhoB.Itens[0].Quantidade);
    }

    [Fact]
    public void Produto_de_outra_loja_nao_entra_no_carrinho()
    {
        var session = new FakeSession();
        var currentLoja = new FakeCurrentLojaService();
        var produtos = new FakeProdutoRepository(
            Produto(id: 20, lojaId: 2, nome: "Produto loja B"));

        currentLoja.SetLojaId(1);
        var repo = CriarRepositorio(session, currentLoja, produtos);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            repo.AdicionarItem(produtos.ObterPorId(20)!, 1));

        Assert.Contains("Produto não pertence à loja atual", ex.Message);
        Assert.Empty(repo.ObterCarrinho().Itens);
    }

    [Fact]
    public void Pedido_total_com_entrega_soma_subtotal_e_taxa()
    {
        var pedido = new PedidoModel
        {
            ValorTotal = 42.50m,
            TaxaEntrega = 5m
        };

        Assert.Equal(47.50m, pedido.ValorTotalComEntrega);
    }

    private static CarrinhoRepository CriarRepositorio(
        ISession session,
        ICurrentLojaService currentLoja,
        IProdutoRepository produtos)
    {
        var httpContext = new DefaultHttpContext
        {
            Session = session
        };

        var http = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevelopmentSettings:LocalhostOnly"] = "false"
            })
            .Build();

        return new CarrinhoRepository(
            http,
            produtos,
            NullLogger<CarrinhoRepository>.Instance,
            context: null!,
            currentLoja,
            config);
    }

    private static ProdutoModel Produto(int id, int lojaId, string nome)
    {
        return new ProdutoModel
        {
            Id = id,
            LojaId = lojaId,
            Nome = nome,
            Preco = 10m,
            Estoque = 10,
            Ativo = true
        };
    }

    private sealed class FakeCurrentLojaService : ICurrentLojaService
    {
        public int? LojaId { get; private set; }
        public bool HasLoja => LojaId.HasValue && LojaId.Value > 0;

        public void SetLojaId(int lojaId) => LojaId = lojaId;
        public void ClearLoja() => LojaId = null;
    }

    private sealed class FakeProdutoRepository : IProdutoRepository
    {
        private readonly Dictionary<int, ProdutoModel> _produtos;

        public FakeProdutoRepository(params ProdutoModel[] produtos)
        {
            _produtos = produtos.ToDictionary(x => x.Id);
        }

        public ProdutoModel? ObterPorId(int id) =>
            _produtos.TryGetValue(id, out var produto) ? produto : null;

        public IEnumerable<ProdutoModel> ObterTodos() => _produtos.Values;
        public void Adicionar(ProdutoModel produto) => _produtos[produto.Id] = produto;
        public void Atualizar(ProdutoModel produto) => _produtos[produto.Id] = produto;
        public void Remover(int id) => _produtos.Remove(id);
        public IEnumerable<ProdutoModel> ObterPorCategoria(int categoriaId) => _produtos.Values.Where(x => x.CategoriaId == categoriaId);
        public IEnumerable<ProdutoModel> ObterMaisVendidos(int quantidade) => _produtos.Values.Take(quantidade);
        public IQueryable<ProdutoModel> Query() => _produtos.Values.AsQueryable();
        public IEnumerable<ProdutoModel> ObterMaisPopulares(int take = 8) => _produtos.Values.Take(take);
        public List<ProdutoModel> FiltrarProdutos(FiltrosModel filtros) => _produtos.Values.ToList();
        public List<string> ObterCategoriasDistintas() => new();
        public List<string> ObterSaboresDistintos() => new();
        public List<string> ObterCoresDistintas() => new();
    }

    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
