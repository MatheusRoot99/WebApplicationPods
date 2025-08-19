using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;

namespace WebApplicationPods.Data
{
    // Agora seu único contexto faz TUDO: domínio + Identity
    public class BancoContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public BancoContext(DbContextOptions<BancoContext> options) : base(options) { }

        // ====== Suas entidades de domínio ======
        public DbSet<CategoriaModel> Categorias { get; set; }
        public DbSet<ProdutoModel> Produtos { get; set; }
        public DbSet<ClienteModel> Clientes { get; set; }
        public DbSet<EnderecoModel> Enderecos { get; set; }
        public DbSet<PedidoModel> Pedidos { get; set; }
        public DbSet<PedidoItemModel> PedidoItens { get; set; }

        // Se você usa UsuarioModel para CLIENTE “sem identity”, pode manter:
        public DbSet<UsuarioModel> Usuarios { get; set; }

        public DbSet<CarrinhoModel> Carrinhos { get; set; }
        public DbSet<PaymentModel> Pagamentos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1) Deixe o Identity se configurar
            base.OnModelCreating(modelBuilder);

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.CPF).IsUnique();

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.PhoneNumber).IsUnique();

            // deixar explícito que endereço é opcional
            modelBuilder.Entity<ApplicationUser>(b =>
            {
                b.Property(u => u.Endereco).IsRequired(false);
                b.Property(u => u.Complemento).IsRequired(false);
                b.Property(u => u.Cidade).IsRequired(false);
                b.Property(u => u.Estado).IsRequired(false);
                b.Property(u => u.CEP).IsRequired(false);
            });

            // Pedido -> Endereco (sem cascade delete)
            modelBuilder.Entity<PedidoModel>()
                .HasOne(p => p.Endereco)
                .WithMany()
                .HasForeignKey(p => p.EnderecoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment -> Pedido (sem cascade delete)
            modelBuilder.Entity<PaymentModel>()
                .HasOne(p => p.Pedido)
                .WithMany(o => o.Pagamentos)
                .HasForeignKey(p => p.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ajustes em Payment
            modelBuilder.Entity<PaymentModel>(e =>
            {
                e.Property(p => p.CardBrand).IsRequired(false);
                e.Property(p => p.CardLast4).IsRequired(false);
                e.Property(p => p.ClientSecretOrToken).IsRequired(false);
                e.Property(p => p.PixQrBase64Png).IsRequired(false);
                e.Property(p => p.FailureReason).IsRequired(false);
            });

            // Aqui você pode adicionar outras configs de domínio (tamanho de campos, etc.)
        }
    }
}
