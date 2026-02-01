using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json.Serialization;
using WebApplicationPods.Data;
using WebApplicationPods.Infra;
using WebApplicationPods.Models;
using WebApplicationPods.Payments;
using WebApplicationPods.Payments.Gateways;
using WebApplicationPods.Payments.Options;
using WebApplicationPods.Repositories;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Repository.Repository;
using WebApplicationPods.Services;
using WebApplicationPods.Services.Interface;
using WebApplicationPods.Services.service;
using WebApplicationPods.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// ==================== Cultura global pt-BR ====================
var ptBR = new CultureInfo("pt-BR");
ptBR.NumberFormat.NumberDecimalSeparator = ",";
ptBR.NumberFormat.CurrencyDecimalSeparator = ",";
CultureInfo.DefaultThreadCurrentCulture = ptBR;
CultureInfo.DefaultThreadCurrentUICulture = ptBR;

builder.Services.Configure<RequestLocalizationOptions>(opts =>
{
    opts.DefaultRequestCulture = new RequestCulture(ptBR);
    opts.SupportedCultures = new[] { ptBR };
    opts.SupportedUICultures = new[] { ptBR };
    opts.RequestCultureProviders = new IRequestCultureProvider[] {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// ==================== Logging ====================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ==================== MVC + JSON + AntiForgery ====================
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

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "Pods.AntiForgery";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    o.HeaderName = "RequestVerificationToken";
});

// ==================== Authorization ====================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// ==================== EF Core ====================
// ✅ DbContext principal (tudo do sistema + Identity + filtros multi-loja)
builder.Services.AddDbContext<BancoContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataBase")));

// ✅ DbContext “neutro” (só pra resolver subdomínio sem filtro multi-loja)
builder.Services.AddDbContext<TenantDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataBase")));

// ==================== Identity ====================
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<BancoContext>()
    .AddDefaultTokenProviders();

// ==================== Cookie Auth ====================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    options.LoginPath = "/Conta/Login";
    options.AccessDeniedPath = "/Conta/AcessoNegado";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
});

// ==================== Sessão ====================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "SitePods.Session";
});

// ==================== HttpContext + StoreUrlBuilder ====================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<WebApplicationPods.Services.Interface.IStoreUrlBuilder, WebApplicationPods.Services.service.StoreUrlBuilder>();

// ==================== HTTP Client ViaCEP ====================
builder.Services.AddHttpClient<ICepService, CepService>(client =>
{
    client.BaseAddress = new Uri("https://viacep.com.br/ws/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ==================== Pagamentos ====================
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection("Payments"));

builder.Services.AddHttpClient<MercadoPagoGateway>(http =>
{
    http.BaseAddress = new Uri("https://api.mercadopago.com/");
});

builder.Services.AddScoped<StripeGateway>();
builder.Services.AddScoped<PixManualGateway>();

builder.Services.AddScoped<Func<string, IPaymentGateway>>(sp => provider =>
{
    if (provider.Equals("MercadoPago", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<MercadoPagoGateway>();
    if (provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<StripeGateway>();
    if (provider.Equals("PixManual", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<PixManualGateway>();

    throw new InvalidOperationException($"Provedor de pagamento não suportado: {provider}");
});

builder.Services.AddScoped<IPaymentCredentialsResolver, PaymentCredentialsResolver>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// ==================== Repositórios / Serviços ====================
builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ICarrinhoRepository, CarrinhoRepository>();
builder.Services.AddScoped<ICarrinhoService, CarrinhoService>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<IEmailSenderService, GmailEmailSenderService>();
builder.Services.AddScoped<ILojaConfigService, LojaConfigService>();
builder.Services.AddScoped<IEstoqueService, EstoqueService>();
builder.Services.AddScoped<ILojaConfigRepository, LojaConfigRepository>();

builder.Services.AddDataProtection();
builder.Services.AddSingleton<IClienteRememberService, ClienteRememberService>();

builder.Services.AddScoped<ICurrentLojaService, CurrentLojaService>();
builder.Services.AddScoped<ITenantResolver, SubdomainTenantResolver>();

builder.Services.AddHostedService<IdentitySeedHostedService>();
builder.Services.AddSignalR();

var app = builder.Build();

// ==================== DB Migrations ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BancoContext>();
    db.Database.Migrate();
}

// ==================== Pipeline ====================
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
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

// força combinação host x role
app.UseMiddleware<RoleSubdomainEnforcerMiddleware>();

// ✅ redireciona admin/painel se alguém acessar pelo host errado
app.UseMiddleware<SubdomainPortalRedirectMiddleware>();

// Auto-login por cookie (hidrata sessão do cliente)
app.UseMiddleware<ClienteAutoLoginMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Resolve loja (subdomínio) e aplica regras (admin/lojista)
app.UseMiddleware<LojaContextMiddleware>();

// ===== Endpoints =====
app.MapControllers(); // APIs (attribute routing)

// ✅ Areas (Admin/Painel)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Site público
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Hubs
app.MapHub<WebApplicationPods.Hubs.PedidosHub>("/hubs/pedidos");

app.Run();
