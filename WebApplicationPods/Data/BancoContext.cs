using Microsoft.EntityFrameworkCore;
using SitePodsInicial.Models;

namespace SitePodsInicial.Data
{
    public class BancoContext : DbContext
    {

        public BancoContext(DbContextOptions<BancoContext> options) : base(options)
        {

        }

        public DbSet<CategoriaModel> Categorias { get; set; }
        public DbSet<ProdutoModel> Produtos { get; set; }
        public DbSet<ClienteModel> Clientes { get; set; }
        public DbSet<EnderecoModel> Enderecos { get; set; }
        public DbSet<PedidoModel> Pedidos { get; set; }
        public DbSet<PedidoItemModel> PedidoItens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // O código abaixo configura a relação para não ter exclusão em cascata.
            // A relação é entre Endereco (Principal) e Pedido (Dependente).
            modelBuilder.Entity<PedidoModel>()
                .HasOne(p => p.Endereco)
                .WithMany()
                .HasForeignKey(p => p.EnderecoId)
                .OnDelete(DeleteBehavior.Restrict); // ESTA É A LINHA QUE EVITA O ERRO

            // Configurações adicionais do modelo podem ser feitas aqui
            base.OnModelCreating(modelBuilder);
        }
    }
}
