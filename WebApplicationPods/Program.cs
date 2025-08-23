using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

using WebApplicationPods.Data;                 // BancoContext
using WebApplicationPods.Infra;                // IdentitySeedHostedService
using WebApplicationPods.Models;               // ApplicationUser

using WebApplicationPods.Payments;
using WebApplicationPods.Payments.Gateways;
using WebApplicationPods.Payments.Options;

using WebApplicationPods.Repositories;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Repository.Repository;

using WebApplicationPods.Services.Interface;
using WebApplicationPods.Services.service;

var builder = WebApplication.CreateBuilder(args);

// ============ Cultura global pt-BR ============
var ptBR = new CultureInfo("pt-BR");
ptBR.NumberFormat.NumberDecimalSeparator = ",";
ptBR.NumberFormat.CurrencyDecimalSeparator = ",";

// Cultura padrão de threads (.NET) — garante binder/ToString/validação em pt-BR
CultureInfo.DefaultThreadCurrentCulture = ptBR;
CultureInfo.DefaultThreadCurrentUICulture = ptBR;

// RequestLocalization (permite querystring/cookie/accept-language)
builder.Services.Configure<RequestLocalizationOptions>(opts =>
{
    opts.DefaultRequestCulture = new RequestCulture(ptBR);
    opts.SupportedCultures = new[] { ptBR };
    opts.SupportedUICultures = new[] { ptBR };

    opts.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// ============ Logging ============
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ============ MVC + JSON + AntiForgery ============
builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
    });

// ============ EF Core ============
builder.Services.AddDbContext<BancoContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataBase")));

// ============ Identity (com BancoContext) ============
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;

        options.User.RequireUniqueEmail = false;       // login por Telefone/CPF, se quiser
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<BancoContext>()
    .AddDefaultTokenProviders();

// ============ Cookie de autenticação ============
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Conta/Login";
    options.AccessDeniedPath = "/Conta/AcessoNegado";
    options.SlidingExpiration = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // em produção, mantenha Always
});

// ============ Sessão ============
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "SitePods.Session";
});

// ============ HTTP Client ViaCEP ============
builder.Services.AddHttpClient<ICepService, CepService>(client =>
{
    client.BaseAddress = new Uri("https://viacep.com.br/ws/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ============ Pagamentos ============
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection("Payments"));

// Gateways concretos
builder.Services.AddHttpClient<MercadoPagoGateway>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var token = cfg["Payments:MercadoPago:AccessToken"];
    http.BaseAddress = new Uri("https://api.mercadopago.com/");
    if (!string.IsNullOrWhiteSpace(token))
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
});
builder.Services.AddScoped<StripeGateway>();

// Factory para escolher o gateway em runtime
builder.Services.AddScoped<Func<string, IPaymentGateway>>(sp => provider =>
{
    if (provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<MercadoPagoGateway>();
    if (provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<StripeGateway>();
    throw new InvalidOperationException($"Provedor não suportado: {provider}");
});

// ============ Infra ============
builder.Services.AddHttpContextAccessor();

// ============ Repositórios / Serviços ============
builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ICarrinhoRepository, CarrinhoRepository>();
builder.Services.AddScoped<ICarrinhoService, CarrinhoService>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<IEmailSenderService, GmailEmailSenderService>();
builder.Services.AddScoped<IPaymentCredentialsResolver, PaymentCredentialsResolver>();

// Seed de Roles/Admin
builder.Services.AddHostedService<IdentitySeedHostedService>();

var app = builder.Build();

// ============ Localização (precisa vir cedo no pipeline) ============
app.UseRequestLocalization(app.Services
    .GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

// ============ Pipeline ============
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // (Opcional) no-cache em dev
    app.Use(async (context, next) =>
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "-1";
        await next();
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();   // antes de Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
