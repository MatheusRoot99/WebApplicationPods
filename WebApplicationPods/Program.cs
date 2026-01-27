// Program.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json.Serialization;
using WebApplicationPods.Data;                 // BancoContext
using WebApplicationPods.Infra;                // IdentitySeedHostedService
using WebApplicationPods.Models;               // ApplicationUser
using WebApplicationPods.Payments;             // IPaymentService, PaymentService, IPaymentGateway
using WebApplicationPods.Payments.Gateways;    // MercadoPagoGateway, StripeGateway
using WebApplicationPods.Payments.Options;     // PaymentsOptions
using WebApplicationPods.Repositories;         // ICepService, CepService
using WebApplicationPods.Repository.Interface; // Repositórios
using WebApplicationPods.Repository.Repository;
using WebApplicationPods.Services;
using WebApplicationPods.Services.Interface;   // IEmailSenderService, ICarrinhoService
using WebApplicationPods.Services.service;
using WebApplicationPods.Middlewares;          // <<< Auto-login middleware

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


// depois de AddControllersWithViews()
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "Pods.AntiForgery";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    // Em DEV, não force Secure, senão o cookie não vai em http://localhost
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    o.HeaderName = "RequestVerificationToken";
});

// Política "Admin"
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// ==================== EF Core (SQL Server / Azure) ====================
builder.Services.AddDbContext<BancoContext>(options =>
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

// ==================== Cookie de autenticação ====================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    // Em produção force HTTPS; em dev deixa conforme a requisição para não quebrar em http://localhost
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

// ==================== HTTP Client ViaCEP ====================
builder.Services.AddHttpClient<ICepService, CepService>(client =>
{
    client.BaseAddress = new Uri("https://viacep.com.br/ws/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ==================== Pagamentos (Options + Gateways + Service) ====================
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection("Payments"));

// Mercado Pago
builder.Services.AddHttpClient<MercadoPagoGateway>(http =>
{
    http.BaseAddress = new Uri("https://api.mercadopago.com/");
});

// Stripe
builder.Services.AddScoped<StripeGateway>();
// Pix Manual  <<<<<<<<<<  ADICIONE ESTA LINHA
builder.Services.AddScoped<PixManualGateway>();

// Factory para escolher o gateway em runtime
// Factory para escolher o gateway em runtime
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

// Resolver de credenciais e serviço de pagamento
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPaymentCredentialsResolver, PaymentCredentialsResolver>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// ==================== Infra / Repositórios / Serviços ====================
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


// Seed (roles/usuário admin)
builder.Services.AddHostedService<IdentitySeedHostedService>();
builder.Services.AddSignalR();

var app = builder.Build();

// ==================== Aplicar migrations automaticamente ====================
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

    // opcional: evitar cache em dev
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

app.MapHub<WebApplicationPods.Hubs.PedidosHub>("/hubs/pedidos");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ===== Sessão precisa vir antes do middleware e dos controllers
app.UseSession();

// ===== AUTO-LOGIN POR COOKIE (hidrata a sessão ClienteTelefone)
app.UseMiddleware<ClienteAutoLoginMiddleware>();

// Auth/Authorization padrões do Identity (para Admin/Lojista etc.)
app.UseAuthentication();
app.UseAuthorization();
// GARANTE LOJA CONTEXT + BLOQUEIOS
app.UseMiddleware<WebApplicationPods.Middlewares.LojaContextMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
