using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebApplicationPods.Data;
using WebApplicationPods.Models;
using WebApplicationPods.Repository.Repository;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Tests;

public class PedidoRepositoryTenantTests
{
    [Fact]
    public void Lojista_nao_obtem_pedido_de_outra_loja()
    {
        using var db = CriarDb(lojaIdAtual: 1);

        db.Clientes.Add(new ClienteModel
        {
            Id = 1,
            Nome = "Cliente teste",
            Email = "cliente@teste.local",
            Telefone = "11999999999"
        });

        db.Pedidos.Add(Pedido(id: 101, lojaId: 1));
        db.Pedidos.Add(Pedido(id: 202, lojaId: 2));
        db.SaveChanges();

        var repository = CriarRepository(db, lojaIdAtual: 1);

        Assert.NotNull(repository.ObterPorId(101));
        Assert.Null(repository.ObterPorId(202));
    }

    private static BancoContext CriarDb(int lojaIdAtual)
    {
        var options = new DbContextOptionsBuilder<BancoContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BancoContext(options, new FakeCurrentLojaService(lojaIdAtual));
    }

    private static PedidoRepository CriarRepository(BancoContext db, int lojaIdAtual)
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevelopmentSettings:LocalhostOnly"] = "false"
            })
            .Build();

        return new PedidoRepository(
            db,
            http,
            new FakeCurrentLojaService(lojaIdAtual),
            config,
            userManager: null!);
    }

    private static PedidoModel Pedido(int id, int lojaId)
    {
        return new PedidoModel
        {
            Id = id,
            LojaId = lojaId,
            ClienteId = 1,
            Status = "Pendente",
            MetodoPagamento = "Pix",
            ValorTotal = 10m,
            TaxaEntrega = 5m,
            DataPedido = DateTime.UtcNow
        };
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
}
