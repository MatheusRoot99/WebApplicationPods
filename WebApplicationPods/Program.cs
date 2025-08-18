using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using WebApplicationPods.Data;
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

// ===== MVC + JSON (evita ciclo em navegações EF)
builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
    });

// ===== EF Core
builder.Services.AddDbContext<BancoContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataBase")));

// ===== Sessão
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "SitePods.Session";
});

// ===== HTTP Client para ViaCEP (exemplo seu)
builder.Services.AddHttpClient<ICepService, CepService>(client =>
{
    client.BaseAddress = new Uri("https://viacep.com.br/ws/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ===== Payment Gateway (Typed HttpClient) + Domain Service
// Importante: seu MercadoPagoGateway deve ter construtor (HttpClient http, IConfiguration cfg)
// e dentro dele setar o Authorization Bearer com o AccessToken do appsettings.
builder.Services.AddHttpClient<IPaymentGateway, MercadoPagoGateway>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
});

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
