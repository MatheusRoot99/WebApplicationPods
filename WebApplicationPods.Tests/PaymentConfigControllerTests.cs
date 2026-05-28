using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Payments.Options;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Tests;

public class PaymentConfigControllerTests
{
    [Fact]
    public async Task Admin_com_loja_selecionada_salva_credenciais_no_dono_da_loja()
    {
        await using var db = CriarDb(lojaIdAtual: 10);
        db.Lojas.Add(new LojaModel
        {
            Id = 10,
            Nome = "Loja A",
            Subdominio = "loja-a",
            Ativa = true,
            DonoUserId = 200,
            Config = new LojaConfig
            {
                Id = 1,
                LojaId = 10,
                LojistaUserId = 201,
                Nome = "Loja A"
            }
        });
        await db.SaveChangesAsync();

        var controller = CriarController(
            db,
            lojaIdAtual: 10,
            user: Usuario(id: 1, nome: "Admin"),
            role: "Admin");

        var result = await controller.Edit(new PaymentConfigEditViewModel
        {
            Provider = "Stripe",
            StripePublishableKey = "pk_loja",
            StripeSecretKey = "sk_loja",
            StripeWebhookSecret = "whsec_loja"
        });

        var config = await db.MerchantPaymentConfigs.SingleAsync();
        var stripe = JsonSerializer.Deserialize<StripeOptions>(config.ConfigJson)!;

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(200, config.UserId);
        Assert.Equal("Stripe", config.Provider);
        Assert.Equal("pk_loja", stripe.PublishableKey);
        Assert.Equal("sk_loja", stripe.SecretKey);
        Assert.Equal("whsec_loja", stripe.WebhookSecret);
    }

    [Fact]
    public async Task Lojista_salva_credenciais_no_proprio_usuario()
    {
        await using var db = CriarDb(lojaIdAtual: 10);

        var controller = CriarController(
            db,
            lojaIdAtual: 10,
            user: Usuario(id: 300, nome: "Lojista"),
            role: "Lojista");

        var result = await controller.Edit(new PaymentConfigEditViewModel
        {
            Provider = "MercadoPago",
            MpPublicKey = "APP_USR-public",
            MpAccessToken = "APP_USR-access",
            MpWebhookSecret = "mp-webhook"
        });

        var config = await db.MerchantPaymentConfigs.SingleAsync();
        var mp = JsonSerializer.Deserialize<MercadoPagoOptions>(config.ConfigJson)!;

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(300, config.UserId);
        Assert.Equal("MercadoPago", config.Provider);
        Assert.Equal("APP_USR-public", mp.PublicKey);
        Assert.Equal("APP_USR-access", mp.AccessToken);
        Assert.Equal("mp-webhook", mp.WebhookSecret);
    }

    [Fact]
    public async Task Editar_stripe_sem_secret_key_nao_apaga_secret_key_existente()
    {
        await using var db = CriarDb(lojaIdAtual: 10);
        db.MerchantPaymentConfigs.Add(new MerchantPaymentConfig
        {
            UserId = 300,
            Provider = "Stripe",
            ConfigJson = JsonSerializer.Serialize(new StripeOptions
            {
                PublishableKey = "pk_antiga",
                SecretKey = "sk_antiga",
                WebhookSecret = "whsec_antigo"
            })
        });
        await db.SaveChangesAsync();

        var controller = CriarController(
            db,
            lojaIdAtual: 10,
            user: Usuario(id: 300, nome: "Lojista"),
            role: "Lojista");

        await controller.Edit(new PaymentConfigEditViewModel
        {
            Provider = "Stripe",
            StripePublishableKey = "pk_nova",
            StripeSecretKey = "",
            StripeWebhookSecret = "whsec_novo"
        });

        var config = await db.MerchantPaymentConfigs.SingleAsync();
        var stripe = JsonSerializer.Deserialize<StripeOptions>(config.ConfigJson)!;

        Assert.Equal("pk_nova", stripe.PublishableKey);
        Assert.Equal("sk_antiga", stripe.SecretKey);
        Assert.Equal("whsec_novo", stripe.WebhookSecret);
    }

    private static PaymentConfigController CriarController(
        BancoContext db,
        int lojaIdAtual,
        ApplicationUser user,
        string role)
    {
        var httpContext = new DefaultHttpContext
        {
            User = Principal(user.Id, role)
        };

        var controller = new PaymentConfigController(
            db,
            new FakeUserManager(user),
            new FakeCurrentLojaService(lojaIdAtual))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
        };

        return controller;
    }

    private static BancoContext CriarDb(int lojaIdAtual)
    {
        var options = new DbContextOptionsBuilder<BancoContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BancoContext(options, new FakeCurrentLojaService(lojaIdAtual));
    }

    private static ApplicationUser Usuario(int id, string nome)
    {
        return new ApplicationUser
        {
            Id = id,
            Nome = nome,
            UserName = nome,
            CPF = "12345678901"
        };
    }

    private static ClaimsPrincipal Principal(int userId, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"user-{userId}"),
            new Claim(ClaimTypes.Role, role)
        }, "Test");

        return new ClaimsPrincipal(identity);
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

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
