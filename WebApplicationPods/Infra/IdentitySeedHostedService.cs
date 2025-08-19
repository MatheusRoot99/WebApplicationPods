using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using WebApplicationPods.Data;
using WebApplicationPods.Models;

namespace WebApplicationPods.Infra
{
    public class IdentitySeedHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        public IdentitySeedHostedService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BancoContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1) Roles
            string[] roles = new[] { "Admin", "Lojista", "Cliente" };
            foreach (var role in roles)
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole<int>(role));

            // 2) Admin padrão
            const string cpfAdmin = "12345678901";    // 11 dígitos
            const string telAdmin = "11999999999";    // DDD + número, só dígitos
            const string senhaAdmin = "123456";    // troque depois!

            var admin = await userManager.Users.FirstOrDefaultAsync(u => u.CPF == cpfAdmin, cancellationToken);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = cpfAdmin,     // você pode usar CPF como UserName
                    CPF = cpfAdmin,
                    PhoneNumber = telAdmin,
                    Email = "admin@sualoja.com",
                    Nome = "Administrador"
                };

                var resCreate = await userManager.CreateAsync(admin, senhaAdmin);
                if (resCreate.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
                // opcional: também adicionar "Lojista" se quiser
                // await userManager.AddToRoleAsync(admin, "Lojista");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
