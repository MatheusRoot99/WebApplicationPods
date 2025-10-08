using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebApplicationPods.Models;

namespace WebApplicationPods.Data
{
    // Contexto único: domínio + Identity<int>
    public class BancoContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public BancoContext(DbContextOptions<BancoContext> options) : base(options)
        {
            // Em DEV: crie se não existir (opcional)
             Database.EnsureCreated();
        }

        // ====== Suas entidades ======
        public DbSet<MerchantPaymentConfig> MerchantPaymentConfigs => Set<MerchantPaymentConfig>();
        public DbSet<CategoriaModel> Categorias { get; set; }
        public DbSet<ProdutoModel> Produtos { get; set; }
        public DbSet<ClienteModel> Clientes { get; set; }
        public DbSet<EnderecoModel> Enderecos { get; set; }
        public DbSet<PedidoModel> Pedidos { get; set; }
        public DbSet<PedidoItemModel> PedidoItens { get; set; }
        public DbSet<UsuarioModel> Usuarios { get; set; }
        public DbSet<CarrinhoModel> Carrinhos { get; set; }
        public DbSet<PaymentModel> Pagamentos { get; set; }
        public DbSet<LojaConfig> LojaConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1) Deixe o Identity se configurar
            base.OnModelCreating(modelBuilder);

            // 2) Índices de ApplicationUser (filtrados para permitir múltiplos NULL)
            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.CPF)
                .IsUnique()
                .HasFilter("[CPF] IS NOT NULL");

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique()
                .HasFilter("[PhoneNumber] IS NOT NULL");

            // Campos opcionais do ApplicationUser
            modelBuilder.Entity<ApplicationUser>(b =>
            {
                b.Property(u => u.Endereco).IsRequired(false);
                b.Property(u => u.Complemento).IsRequired(false);
                b.Property(u => u.Cidade).IsRequired(false);
                b.Property(u => u.Estado).IsRequired(false);
                b.Property(u => u.CEP).IsRequired(false);
            });

            // ====== Pedido / Payment ======
            // Pedido -> Endereco (sem cascade delete). EnderecoId é opcional (retirada no local).
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

            // Ajustes em Payment (colunas opcionais)
            modelBuilder.Entity<PaymentModel>(e =>
            {
                e.Property(p => p.CardBrand).IsRequired(false);
                e.Property(p => p.CardLast4).IsRequired(false);
                e.Property(p => p.ClientSecretOrToken).IsRequired(false);
                e.Property(p => p.PixQrBase64Png).IsRequired(false);
                e.Property(p => p.FailureReason).IsRequired(false);
            });

            // MerchantPaymentConfig -> ApplicationUser
            modelBuilder.Entity<MerchantPaymentConfig>(e =>
            {
                e.Property(p => p.Provider).HasMaxLength(100).IsRequired();
                e.Property(p => p.ConfigJson).IsRequired();

                e.HasIndex(p => new { p.UserId, p.Provider }).IsUnique();

                e.HasOne<ApplicationUser>()
                 .WithMany()
                 .HasForeignKey(p => p.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Categoria (exemplo)
            modelBuilder.Entity<CategoriaModel>(b =>
            {
                b.ToTable("Categorias");
                b.Property(x => x.Nome).HasMaxLength(50).IsRequired();
                b.Property(x => x.Descricao).HasMaxLength(200);
            });

            // ====== Endereco: índices e defaults ======
            // Índice normal do FK (não único)
            modelBuilder.Entity<EnderecoModel>()
                .HasIndex(e => e.ClienteId)
                .HasDatabaseName("IX_Enderecos_ClienteId");

            // Índice ÚNICO FILTRADO: no máximo 1 principal ATIVO por cliente
            modelBuilder.Entity<EnderecoModel>()
                .HasIndex(e => e.ClienteId)
                .HasFilter("[Principal] = 1 AND [Ativo] = 1")
                .IsUnique()
                .HasDatabaseName("UX_Enderecos_PrincipalPorCliente");

            modelBuilder.Entity<EnderecoModel>(b =>
            {
                b.Property(e => e.Principal).HasDefaultValue(false).IsRequired();
                b.Property(e => e.Ativo).HasDefaultValue(true).IsRequired();
            });

            // ====== Query Filters (soft delete) ======
            modelBuilder.Entity<PedidoModel>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<EnderecoModel>().HasQueryFilter(e => e.Ativo);
        }
    }
}
