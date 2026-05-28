using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WebApplicationPods.Controllers;
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

public class PagamentoControllerTests
{
    [Fact]
    public async Task Status_pago_com_token_marca_pedido_baixa_estoque_e_limpa_carrinho_uma_vez()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Pix");
        db.Pagamentos.Add(new PaymentModel
        {
            Id = 1,
            PedidoId = 1,
            Metodo = PaymentMethod.Pix,
            Provider = "PixManual",
            ProviderPaymentId = "pix-manual-1",
            Status = PaymentStatus.Paid,
            Amount = 15m,
            PaidAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService(db);
        var estoque = new FakeEstoqueService(db);
        var carrinho = new FakeCarrinhoRepository();
        var controller = CriarController(db, lojaIdAtual: 1, role: "", pedidoApp, estoque, carrinho: carrinho);

        var result = await controller.Status(1, "token-1");
        var result2 = await controller.Status(1, "token-1");

        var item = await db.PedidoItens.SingleAsync();

        Assert.IsType<JsonResult>(result);
        Assert.IsType<JsonResult>(result2);
        Assert.True(item.EstoqueBaixado);
        Assert.Equal(1, estoque.Chamadas);
        Assert.Equal(1, carrinho.LimparChamadas);
        Assert.Equal(1, pedidoApp.Pagos.Count(x => x == 1));
    }

    [Fact]
    public async Task Status_com_token_invalido_retorna_unauthorized_e_nao_altera()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Pix");
        db.Pagamentos.Add(new PaymentModel
        {
            Id = 1,
            PedidoId = 1,
            Metodo = PaymentMethod.Pix,
            Provider = "PixManual",
            ProviderPaymentId = "pix-manual-1",
            Status = PaymentStatus.Paid,
            Amount = 15m
        });
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService(db);
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "", pedidoApp, estoque);

        var result = await controller.Status(1, "token-errado");

        var item = await db.PedidoItens.SingleAsync();

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.False(item.EstoqueBaixado);
        Assert.Equal(0, estoque.Chamadas);
        Assert.Empty(pedidoApp.Pagos);
    }

    [Fact]
    public async Task Status_pix_expirado_cancela_pagamento_e_pedido_sem_baixar_estoque()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Pix");
        db.Pagamentos.Add(new PaymentModel
        {
            Id = 1,
            PedidoId = 1,
            Metodo = PaymentMethod.Pix,
            Provider = "PixManual",
            ProviderPaymentId = "pix-manual-1",
            Status = PaymentStatus.Pending,
            Amount = 15m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-20)
        });
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService(db);
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "", pedidoApp, estoque);

        var result = await controller.Status(1, "token-1");

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.IsType<JsonResult>(result);
        Assert.Equal(PaymentStatus.Canceled, payment.Status);
        Assert.NotNull(payment.CanceledAt);
        Assert.False(item.EstoqueBaixado);
        Assert.Equal(0, estoque.Chamadas);
        Assert.Contains(pedidoApp.StatusAtualizados, x => x.PedidoId == 1 && x.Status == "Cancelado");
    }

    [Fact]
    public async Task Cancelar_com_token_valido_cancela_pagamento_e_pedido()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Pix");
        db.Pagamentos.Add(PagamentoPixManual());
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService(db);
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "", pedidoApp, estoque);

        var result = await controller.Cancelar(1, "token-1");

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.IsType<JsonResult>(result);
        Assert.Equal(PaymentStatus.Canceled, payment.Status);
        Assert.NotNull(payment.CanceledAt);
        Assert.False(item.EstoqueBaixado);
        Assert.Contains(pedidoApp.StatusAtualizados, x => x.PedidoId == 1 && x.Status == "Cancelado");
    }

    [Fact]
    public async Task Cancelar_pagamento_pago_nao_cancela()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Pix");
        db.Pagamentos.Add(new PaymentModel
        {
            Id = 1,
            PedidoId = 1,
            Metodo = PaymentMethod.Pix,
            Provider = "PixManual",
            ProviderPaymentId = "pix-manual-1",
            Status = PaymentStatus.Paid,
            Amount = 15m,
            PaidAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService(db);
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "", pedidoApp, estoque);

        var result = await controller.Cancelar(1, "token-1");

        var payment = await db.Pagamentos.SingleAsync();

        Assert.IsType<JsonResult>(result);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Null(payment.CanceledAt);
        Assert.Empty(pedidoApp.StatusAtualizados);
    }

    [Fact]
    public async Task ConfirmCard_com_token_valido_marca_pago_baixa_estoque_e_limpa_carrinho()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Cartão de Crédito");
        db.Pagamentos.Add(new PaymentModel
        {
            Id = 1,
            PedidoId = 1,
            Metodo = PaymentMethod.CardCredit,
            Provider = "Stripe",
            ProviderPaymentId = "pi_123",
            Status = PaymentStatus.RequiresAction,
            Amount = 15m
        });
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService(db);
        var estoque = new FakeEstoqueService(db);
        var carrinho = new FakeCarrinhoRepository();
        var payments = new FakePaymentService(db) { ConfirmCardOk = true };
        var controller = CriarController(db, lojaIdAtual: 1, role: "", pedidoApp, estoque, payments, carrinho);

        var result = await controller.ConfirmCard(1, "token-1", new { token = "tok_test" });

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.True(item.EstoqueBaixado);
        Assert.Equal(1, estoque.Chamadas);
        Assert.Equal(1, carrinho.LimparChamadas);
        Assert.Equal(1, pedidoApp.Pagos.Count(x => x == 1));
    }

    [Fact]
    public async Task Confirmar_pix_manual_marca_pago_e_baixa_estoque_uma_vez()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Pix");
        db.Pagamentos.Add(PagamentoPixManual());
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "Lojista", pedidoApp, estoque);

        var result = await controller.ConfirmarPixManual(1);
        var result2 = await controller.ConfirmarPixManual(1);

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        AssertRedirectPedidosAdmin(result);
        AssertRedirectPedidosAdmin(result2);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.NotNull(payment.PaidAt);
        Assert.True(item.EstoqueBaixado);
        Assert.Equal(1, estoque.Chamadas);
        Assert.Equal(1, pedidoApp.Pagos.Count(x => x == 1));
    }

    [Fact]
    public async Task Confirmar_pix_manual_de_outra_loja_retorna_forbid_e_nao_altera()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 2, metodoPagamento: "Pix");
        db.Pagamentos.Add(PagamentoPixManual());
        await db.SaveChangesAsync();

        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "Lojista", pedidoApp, estoque);

        var result = await controller.ConfirmarPixManual(1);

        var payment = await db.Pagamentos.SingleAsync();
        var item = await db.PedidoItens.SingleAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(item.EstoqueBaixado);
        Assert.Equal(0, estoque.Chamadas);
        Assert.Empty(pedidoApp.Pagos);
    }

    [Fact]
    public async Task Aprovar_pagamento_na_entrega_marca_pago_e_baixa_estoque_uma_vez()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 1, metodoPagamento: "Dinheiro");

        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "Lojista", pedidoApp, estoque);

        var result = await controller.AprovarPagamentoEntrega(1);
        var result2 = await controller.AprovarPagamentoEntrega(1);

        var item = await db.PedidoItens.SingleAsync();

        AssertRedirectPedidosAdmin(result);
        AssertRedirectPedidosAdmin(result2);
        Assert.True(item.EstoqueBaixado);
        Assert.Equal(1, estoque.Chamadas);
        Assert.Equal(2, pedidoApp.Pagos.Count(x => x == 1));
    }

    [Fact]
    public async Task Aprovar_pagamento_na_entrega_de_outra_loja_retorna_forbid_e_nao_baixa_estoque()
    {
        await using var db = CriarDb(lojaIdAtual: 1);
        await SeedPedidoAsync(db, lojaId: 2, metodoPagamento: "Dinheiro");

        var pedidoApp = new FakePedidoAppService();
        var estoque = new FakeEstoqueService(db);
        var controller = CriarController(db, lojaIdAtual: 1, role: "Lojista", pedidoApp, estoque);

        var result = await controller.AprovarPagamentoEntrega(1);

        var item = await db.PedidoItens.SingleAsync();

        Assert.IsType<ForbidResult>(result);
        Assert.False(item.EstoqueBaixado);
        Assert.Equal(0, estoque.Chamadas);
        Assert.Empty(pedidoApp.Pagos);
    }

    private static PagamentoController CriarController(
        BancoContext db,
        int lojaIdAtual,
        string role,
        IPedidoAppService pedidoApp,
        IEstoqueService estoque,
        IPaymentService? payments = null,
        FakeCarrinhoRepository? carrinho = null)
    {
        var currentLoja = new FakeCurrentLojaService(lojaIdAtual);
        var httpContext = new DefaultHttpContext
        {
            User = Principal(userId: 100, role, lojaIdAtual),
            Session = new FakeSession()
        };

        return new PagamentoController(
            payments ?? new FakePaymentService(),
            new FakePedidoRepository(),
            carrinho ?? new FakeCarrinhoRepository(),
            db,
            new ConfigurationBuilder().Build(),
            new FakeCredentialsResolver(),
            new FakeUserManager(new ApplicationUser
            {
                Id = 100,
                Nome = "Lojista",
                UserName = "lojista",
                CPF = "12345678901",
                LojaId = lojaIdAtual
            }),
            estoque,
            new NoOpHubContext<PedidosHub>(),
            pedidoApp,
            currentLoja)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
            Url = new FakeUrlHelper()
        };
    }

    private static BancoContext CriarDb(int lojaIdAtual)
    {
        var options = new DbContextOptionsBuilder<BancoContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BancoContext(options, new FakeCurrentLojaService(lojaIdAtual));
    }

    private static async Task SeedPedidoAsync(BancoContext db, int lojaId, string metodoPagamento)
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
            LojaId = lojaId,
            ClienteId = 1,
            Status = "Aguardando Pagamento (Entrega)",
            MetodoPagamento = metodoPagamento,
            RastreioToken = "token-1",
            ValorTotal = 10m,
            TaxaEntrega = 5m,
            DataPedido = DateTime.UtcNow
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

    private static PaymentModel PagamentoPixManual()
    {
        return new PaymentModel
        {
            Id = 1,
            PedidoId = 1,
            Metodo = PaymentMethod.Pix,
            Provider = "PixManual",
            ProviderPaymentId = "pix-manual-1",
            Status = PaymentStatus.Pending,
            Amount = 15m
        };
    }

    private static ClaimsPrincipal Principal(int userId, string role, int lojaId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"user-{userId}"),
            new Claim(ClaimTypes.Role, role),
            new Claim("LojaId", lojaId.ToString())
        }, "Test");

        return new ClaimsPrincipal(identity);
    }

    private static void AssertRedirectPedidosAdmin(IActionResult result)
    {
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("PedidosAdmin", redirect.ControllerName);
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
        private readonly BancoContext? _db;

        public FakePedidoAppService(BancoContext? db = null)
        {
            _db = db;
        }

        public List<int> Pagos { get; } = new();
        public List<(int PedidoId, string Status)> StatusAtualizados { get; } = new();

        public Task<PedidoModel> CriarPedidoAsync(PedidoModel pedido, string? origem = null) =>
            Task.FromResult(pedido);

        public Task<bool> AtualizarStatusAsync(int pedidoId, string novoStatus, string? nomeResponsavel = null, string? usuarioResponsavelId = null, string? observacao = null, string? origem = null) =>
            AtualizarAsync(pedidoId, novoStatus);

        public Task<bool> MarcarComoPagoAsync(int pedidoId, string? nomeResponsavel = null, string? usuarioResponsavelId = null, string? observacao = null, string? origem = null)
        {
            Pagos.Add(pedidoId);
            return AtualizarAsync(pedidoId, "Pago");
        }

        private async Task<bool> AtualizarAsync(int pedidoId, string status)
        {
            StatusAtualizados.Add((pedidoId, status));

            if (_db != null)
            {
                var pedido = await _db.Pedidos.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == pedidoId);
                if (pedido != null)
                {
                    pedido.Status = status;
                    await _db.SaveChangesAsync();
                }
            }

            return true;
        }
    }

    private sealed class FakeCurrentLojaService : ICurrentLojaService
    {
        public FakeCurrentLojaService(int lojaId)
        {
            LojaId = lojaId;
        }

        public int? LojaId { get; private set; }
        public bool HasLoja => LojaId.HasValue && LojaId.Value > 0;
        public void SetLojaId(int lojaId) => LojaId = lojaId;
        public void ClearLoja() => LojaId = null;
    }

    private sealed class FakePaymentService : IPaymentService
    {
        private readonly BancoContext? _db;

        public FakePaymentService(BancoContext? db = null)
        {
            _db = db;
        }

        public bool ConfirmCardOk { get; init; }

        public Task<PaymentModel> StartPaymentAsync(PedidoModel pedido, PaymentMethod metodo) => throw new NotSupportedException();
        public async Task<bool> ConfirmCardAsync(int paymentId, string clientPayloadJson)
        {
            if (!ConfirmCardOk)
                return false;

            if (_db != null)
            {
                var payment = await _db.Pagamentos.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == paymentId);
                if (payment != null)
                {
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }

            return true;
        }
        public Task ApplyWebhookAsync(HttpRequest request) => Task.CompletedTask;
    }

    private sealed class FakeCredentialsResolver : IPaymentCredentialsResolver
    {
        public Task<T> GetAsync<T>(ClaimsPrincipal user, string provider) where T : class, new() =>
            Task.FromResult(new T());

        public Task<T> GetForLojaAsync<T>(int lojaId, string provider) where T : class, new() =>
            Task.FromResult(new T());
    }

    private sealed class FakeCarrinhoRepository : ICarrinhoRepository
    {
        public int LimparChamadas { get; private set; }

        public CarrinhoModel ObterCarrinho() => new();
        public void SalvarCarrinho(CarrinhoModel carrinho) { }
        public void AdicionarItem(ProdutoModel produto, int quantidade, string? sabor = null, string? observacoes = null) { }
        public void AtualizarItem(ProdutoModel produto, int quantidade, string? sabor = null) { }
        public void RemoverItem(int produtoId, string? sabor = null) { }
        public void LimparCarrinho() => LimparChamadas++;
    }

    private sealed class FakeUserManager : UserManager<ApplicationUser>
    {
        private readonly ApplicationUser _user;

        public FakeUserManager(ApplicationUser user)
            : base(
                new FakeUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<ApplicationUser>>.Instance)
        {
            _user = user;
        }

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<ApplicationUser?>(_user);
    }

    private sealed class FakeUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
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

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/fake-url";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/fake-url";
        public string? RouteUrl(UrlRouteContext routeContext) => "/fake-url";
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
