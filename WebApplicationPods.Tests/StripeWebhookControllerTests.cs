using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebApplicationPods.Controllers;
using WebApplicationPods.Data;
using WebApplicationPods.Enum;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Payments;
using WebApplicationPods.Payments.Options;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Tests;

public class StripeWebhookControllerTests
{
    [Fact]
    public async Task Stripe_evento_aprovado_marca_pago_e_baixa_estoque_uma_vez()
    {
        await using var db = CriarDb();
        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, "whsec_global", pedidoApp, estoque, new FakeCredentialsResolver());

        await SeedPagamentoStripeAsync(db, providerPaymentId: "pi_123");

        await controller.AplicarPaymentIntentSucceededAsync("pi_123", "ch_123");
        await controller.AplicarPaymentIntentSucceededAsync("pi_123", "ch_123");

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal("ch_123", payment.ProviderOrderId);
        Assert.True(item.EstoqueBaixado);
        Assert.Equal(1, estoque.Chamadas);
        Assert.Equal(1, pedidoApp.Pagos.Count(x => x == 1));
    }

    [Fact]
    public async Task Stripe_evento_de_payment_intent_desconhecido_nao_altera_nada()
    {
        await using var db = CriarDb();
        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, "whsec_global", pedidoApp, estoque, new FakeCredentialsResolver());

        await SeedPagamentoStripeAsync(db, providerPaymentId: "pi_123");

        await controller.AplicarPaymentIntentSucceededAsync("pi_desconhecido", "ch_999");

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(item.EstoqueBaixado);
        Assert.Equal(0, estoque.Chamadas);
        Assert.Empty(pedidoApp.Pagos);
    }

    [Fact]
    public async Task Stripe_webhook_secret_usa_credencial_da_loja_do_pagamento()
    {
        await using var db = CriarDb();
        var creds = new FakeCredentialsResolver
        {
            StripeWebhookSecret = "whsec_loja_10"
        };
        var controller = CriarController(
            db,
            "whsec_global",
            new FakePedidoAppService(),
            new FakeEstoqueService(db),
            creds);

        await SeedPagamentoStripeAsync(db, providerPaymentId: "pi_123");

        var secret = await controller.ObterWebhookSecretAsync("pi_123");

        Assert.Equal("whsec_loja_10", secret);
        Assert.Equal(10, creds.UltimaLojaConsultada);
    }

    private static StripeWebhookController CriarController(
        BancoContext db,
        string fallbackSecret,
        IPedidoAppService pedidoApp,
        IEstoqueService estoque,
        IPaymentCredentialsResolver creds)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:Stripe:WebhookSecret"] = fallbackSecret
            })
            .Build();

        return new StripeWebhookController(
            db,
            cfg,
            pedidoApp,
            estoque,
            new NoOpHubContext<PedidosHub>(),
            creds);
    }

    private static BancoContext CriarDb()
    {
        var options = new DbContextOptionsBuilder<BancoContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BancoContext(options, new StaticCurrentLojaService());
    }

    private static async Task SeedPagamentoStripeAsync(BancoContext db, string providerPaymentId)
    {
        db.Clientes.Add(new ClienteModel
        {
            Id = 1,
            Nome = "Cliente teste",
            Email = "cliente@teste.local",
            Telefone = "11999999999"
        });

        db.Pedidos.Add(new PedidoModel
        {
            Id = 1,
            LojaId = 10,
            ClienteId = 1,
            Status = "Pendente",
            MetodoPagamento = "Cartão de Crédito",
            ValorTotal = 10m,
            TaxaEntrega = 5m,
            DataPedido = DateTime.UtcNow
        });

        db.Pagamentos.Add(new PaymentModel
        {
            Id = 1,
            PedidoId = 1,
            Metodo = WebApplicationPods.Enum.PaymentMethod.CardCredit,
            Provider = "Stripe",
            ProviderPaymentId = providerPaymentId,
            Status = PaymentStatus.Pending,
            Amount = 15m
        });

        db.PedidoItens.Add(new PedidoItemModel
        {
            Id = 1,
            PedidoId = 1,
            ProdutoId = 100,
            Quantidade = 1,
            PrecoUnitario = 10m,
            EstoqueBaixado = false
        });

        await db.SaveChangesAsync();
    }

    private sealed class FakeEstoqueService : IEstoqueService
    {
        private readonly BancoContext _db;

        public FakeEstoqueService(BancoContext db)
        {
            _db = db;
        }

        public int Chamadas { get; private set; }

        public async Task BaixarEstoquePedidoAsync(int pedidoId)
        {
            Chamadas++;

            var itens = await _db.PedidoItens
                .Where(x => x.PedidoId == pedidoId && !x.EstoqueBaixado)
                .ToListAsync();

            foreach (var item in itens)
            {
                item.EstoqueBaixado = true;
                item.EstoqueBaixadoEm = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }
    }

    private sealed class FakePedidoAppService : IPedidoAppService
    {
        public List<int> Pagos { get; } = new();

        public Task<PedidoModel> CriarPedidoAsync(PedidoModel pedido, string? origem = null) =>
            Task.FromResult(pedido);

        public Task<bool> AtualizarStatusAsync(int pedidoId, string novoStatus, string? nomeResponsavel = null, string? usuarioResponsavelId = null, string? observacao = null, string? origem = null) =>
            Task.FromResult(true);

        public Task<bool> MarcarComoPagoAsync(int pedidoId, string? nomeResponsavel = null, string? usuarioResponsavelId = null, string? observacao = null, string? origem = null)
        {
            Pagos.Add(pedidoId);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCredentialsResolver : IPaymentCredentialsResolver
    {
        public string? StripeWebhookSecret { get; init; }
        public int? UltimaLojaConsultada { get; private set; }

        public Task<T> GetAsync<T>(ClaimsPrincipal user, string provider) where T : class, new() =>
            Task.FromResult(new T());

        public Task<T> GetForLojaAsync<T>(int lojaId, string provider) where T : class, new()
        {
            UltimaLojaConsultada = lojaId;

            if (typeof(T) == typeof(StripeOptions))
            {
                object options = new StripeOptions
                {
                    WebhookSecret = StripeWebhookSecret ?? string.Empty
                };

                return Task.FromResult((T)options);
            }

            return Task.FromResult(new T());
        }
    }

    private sealed class StaticCurrentLojaService : ICurrentLojaService
    {
        public int? LojaId => null;
        public bool HasLoja => false;
        public void SetLojaId(int lojaId) { }
        public void ClearLoja() { }
    }

    private sealed class NoOpHubContext<THub> : IHubContext<THub> where THub : Hub
    {
        public IHubClients Clients { get; } = new NoOpHubClients();
        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class NoOpHubClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NoOpClientProxy();

        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NoOpClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
