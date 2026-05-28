using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Data;
using WebApplicationPods.DTO;
using WebApplicationPods.Enum;
using WebApplicationPods.Hubs;
using WebApplicationPods.Models;
using WebApplicationPods.Payments;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Services.Interface;
using static WebApplicationPods.DTO.ReportsDTO;

namespace WebApplicationPods.Tests;

public class PaymentServiceWebhookTests
{
    [Fact]
    public async Task Webhook_com_pagamento_desconhecido_nao_altera_pedido_nem_baixa_estoque()
    {
        await using var db = CriarDb();
        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var gateway = new FakePaymentGateway("pagamento-inexistente", PaymentStatus.Paid);
        var service = CriarService(db, gateway, pedidoApp, estoque);

        db.Clientes.Add(Cliente());
        db.Pedidos.Add(Pedido(id: 1, lojaId: 10, status: "Pendente"));
        db.Pagamentos.Add(Pagamento(id: 1, pedidoId: 1, providerPaymentId: "pagamento-conhecido"));
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

        await service.ApplyWebhookAsync(new DefaultHttpContext().Request);

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(item.EstoqueBaixado);
        Assert.Equal(0, estoque.Chamadas);
        Assert.Empty(pedidoApp.StatusAtualizados);
    }

    [Fact]
    public async Task Webhook_aprovado_baixa_estoque_uma_unica_vez()
    {
        await using var db = CriarDb();
        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var gateway = new FakePaymentGateway("mp-123", PaymentStatus.Paid);
        var service = CriarService(db, gateway, pedidoApp, estoque);

        db.Clientes.Add(Cliente());
        db.Pedidos.Add(Pedido(id: 1, lojaId: 10, status: "Pendente"));
        db.Pagamentos.Add(Pagamento(id: 1, pedidoId: 1, providerPaymentId: "mp-123"));
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

        await service.ApplyWebhookAsync(new DefaultHttpContext().Request);
        await service.ApplyWebhookAsync(new DefaultHttpContext().Request);

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.NotNull(payment.PaidAt);
        Assert.True(item.EstoqueBaixado);
        Assert.Equal(1, estoque.Chamadas);
        Assert.Equal(2, pedidoApp.StatusAtualizados.Count(x => x.PedidoId == 1 && x.Status == "Pago"));
    }

    private static PaymentService CriarService(
        BancoContext db,
        IPaymentGateway gateway,
        IPedidoAppService pedidoApp,
        IEstoqueService estoque)
    {
        return new PaymentService(
            _ => gateway,
            new FakePedidoRepository(),
            db,
            new NoOpHubContext<PedidosHub>(),
            new HttpContextAccessor(),
            creds: null!,
            estoque,
            pedidoApp);
    }

    private static BancoContext CriarDb()
    {
        var options = new DbContextOptionsBuilder<BancoContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BancoContext(options, new StaticCurrentLojaService());
    }

    private static PedidoModel Pedido(int id, int lojaId, string status)
    {
        return new PedidoModel
        {
            Id = id,
            LojaId = lojaId,
            ClienteId = 1,
            Status = status,
            MetodoPagamento = "Pix",
            ValorTotal = 10m,
            TaxaEntrega = 5m,
            DataPedido = DateTime.UtcNow
        };
    }

    private static ClienteModel Cliente()
    {
        return new ClienteModel
        {
            Id = 1,
            Nome = "Cliente teste",
            Email = "cliente@teste.local",
            Telefone = "11999999999"
        };
    }

    private static PaymentModel Pagamento(int id, int pedidoId, string providerPaymentId)
    {
        return new PaymentModel
        {
            Id = id,
            PedidoId = pedidoId,
            Metodo = PaymentMethod.Pix,
            Provider = "MercadoPago",
            ProviderPaymentId = providerPaymentId,
            Status = PaymentStatus.Pending,
            Amount = 15m
        };
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        private readonly string _providerPaymentId;
        private readonly PaymentStatus _status;

        public FakePaymentGateway(string providerPaymentId, PaymentStatus status)
        {
            _providerPaymentId = providerPaymentId;
            _status = status;
        }

        public string Provider => "MercadoPago";

        public Task<PixInitResult> CreatePixAsync(PedidoModel pedido, decimal amount) => throw new NotSupportedException();
        public Task<CardInitResult> CreateCardPaymentAsync(PedidoModel pedido, PaymentMethod method, decimal amount) => throw new NotSupportedException();
        public Task<ConfirmCardResult> ConfirmCardPaymentAsync(string providerPaymentId, string clientPayloadJson) => throw new NotSupportedException();
        public Task<PaymentStatus> GetStatusAsync(string providerPaymentId) => Task.FromResult(_status);
        public Task<string> CreateCheckoutAsync(ClaimsPrincipal user, CheckoutRequest req) => throw new NotSupportedException();

        public Task<(string providerPaymentId, PaymentStatus newStatus, decimal? paidAmount, string extra)> HandleWebhookAsync(HttpRequest request)
        {
            return Task.FromResult((_providerPaymentId, _status, (decimal?)null, string.Empty));
        }
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
        public List<(int PedidoId, string Status)> StatusAtualizados { get; } = new();

        public Task<PedidoModel> CriarPedidoAsync(PedidoModel pedido, string? origem = null) =>
            Task.FromResult(pedido);

        public Task<bool> AtualizarStatusAsync(
            int pedidoId,
            string novoStatus,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null)
        {
            StatusAtualizados.Add((pedidoId, novoStatus));
            return Task.FromResult(true);
        }

        public Task<bool> MarcarComoPagoAsync(
            int pedidoId,
            string? nomeResponsavel = null,
            string? usuarioResponsavelId = null,
            string? observacao = null,
            string? origem = null)
        {
            StatusAtualizados.Add((pedidoId, "Pago"));
            return Task.FromResult(true);
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

    private sealed class FakePedidoRepository : IPedidoRepository
    {
        public PedidoModel? ObterPorId(int id) => null;
        public IEnumerable<PedidoModel> ObterPorCliente(int clienteId) => Enumerable.Empty<PedidoModel>();
        public void Adicionar(PedidoModel pedido) { }
        public void AtualizarStatus(int pedidoId, string status, string? nomeResponsavel = null, string? usuarioResponsavelId = null, string? observacao = null, string? origem = null) { }
        public IEnumerable<PedidoHistoricoModel> ObterHistorico(int pedidoId) => Enumerable.Empty<PedidoHistoricoModel>();
        public decimal ObterTotalVendasHoje() => 0m;
        public PedidoModel? ObterPorToken(string token) => null;
        public IEnumerable<PedidoModel> ObterAbertos() => Enumerable.Empty<PedidoModel>();
        public IEnumerable<PedidoModel> ObterDoDia() => Enumerable.Empty<PedidoModel>();
        public ResumoVendas ObterResumo(DateTime inicio, DateTime fim) => new();
        public IEnumerable<SerieDia> ObterSeriePorDia(DateTime inicio, DateTime fim) => Enumerable.Empty<SerieDia>();
        public IEnumerable<MetodoPagamentoResumo> ObterMetodosPagamentoResumo(DateTime inicio, DateTime fim) => Enumerable.Empty<MetodoPagamentoResumo>();
        public IEnumerable<TopClienteResumo> ObterTopClientes(DateTime inicio, DateTime fim, int take = 5) => Enumerable.Empty<TopClienteResumo>();
        public IEnumerable<PedidoModel> Buscar(AdminOrdersFilterDTO f) => Enumerable.Empty<PedidoModel>();
        public void ExcluirLogico(int id, string? usuario = null) { }
        public int PurgaCanceladosAntigos(int dias = 30) => 0;
        public void Restaurar(int id) { }
    }
}
