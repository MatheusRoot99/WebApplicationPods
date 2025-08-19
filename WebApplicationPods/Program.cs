using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

using WebApplicationPods.Data;       // BancoContext
using WebApplicationPods.Infra;      // IdentitySeedHostedService (seu seed)
using WebApplicationPods.Models;     // ApplicationUser

using WebApplicationPods.Payments;
using WebApplicationPods.Payments.Gateways;

using WebApplicationPods.Repositories;
using WebApplicationPods.Repository.Interface;
using WebApplicationPods.Repository.Repository;

using WebApplicationPods.Services.Interface;
using WebApplicationPods.Services.service;

var builder = WebApplication.CreateBuilder(args);

// ===== Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ===== MVC + JSON + AntiForgery
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

// ===== EF Core (ÚNICO contexto: domínio + Identity)
builder.Services.AddDbContext<BancoContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataBase")));

// ===== Identity + Roles, usando BancoContext
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;

        options.User.RequireUniqueEmail = false;         // login por Telefone/CPF
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<BancoContext>()   // << AQUI: usa seu BancoContext
    .AddDefaultTokenProviders();

// ===== Cookies de autenticação
// No seu Program.cs, adicione/verifique esta configuração:
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Conta/Login";
    options.AccessDeniedPath = "/Conta/AcessoNegado";
    options.SlidingExpiration = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Use Always em produção
});

// ===== Sessão
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "SitePods.Session";
});

// ===== HTTP Client ViaCEP
builder.Services.AddHttpClient<ICepService, CepService>(client =>
{
    client.BaseAddress = new Uri("https://viacep.com.br/ws/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ===== Payment Gateway + Service
builder.Services.AddHttpClient<IPaymentGateway, MercadoPagoGateway>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var token = cfg["Payments:MercadoPago:AccessToken"];

    http.BaseAddress = new Uri("https://api.mercadopago.com/");
    if (!string.IsNullOrWhiteSpace(token))
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
});
builder.Services.AddScoped<IPaymentService, PaymentService>();

// ===== Infra
builder.Services.AddHttpContextAccessor();

// ===== Repositórios / Serviços do domínio
builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ICarrinhoRepository, CarrinhoRepository>();
builder.Services.AddScoped<ICarrinhoService, CarrinhoService>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<IEmailSenderService, GmailEmailSenderService>(); // <--- NOVO

// ===== Seed (roles + admin padrão)
builder.Services.AddHostedService<IdentitySeedHostedService>();

var app = builder.Build();

// ===== Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // (opcional) desabilitar cache em dev
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

app.UseAuthentication(); // antes de Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
