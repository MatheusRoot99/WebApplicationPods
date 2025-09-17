using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;

namespace WebApplicationPods.Data
{
    // Contexto único: domínio + Identity<int>
    public class BancoContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public BancoContext(DbContextOptions<BancoContext> options) : base(options)
        {
              //Cria o banco se não existir (útil para desenvolvimento)

        }

        public DbSet<MerchantPaymentConfig> MerchantPaymentConfigs => Set<MerchantPaymentConfig>();

        // ====== Suas entidades de domínio ======
        public DbSet<CategoriaModel> Categorias { get; set; }
        public DbSet<ProdutoModel> Produtos { get; set; }
        public DbSet<ClienteModel> Clientes { get; set; }
        public DbSet<EnderecoModel> Enderecos { get; set; }
        public DbSet<PedidoModel> Pedidos { get; set; }
        public DbSet<PedidoItemModel> PedidoItens { get; set; }
        public DbSet<UsuarioModel> Usuarios { get; set; } // se você usa este modelo “cliente sem identity”
        public DbSet<CarrinhoModel> Carrinhos { get; set; }
        public DbSet<PaymentModel> Pagamentos { get; set; }
        public DbSet<LojaConfig> LojaConfigs { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1) Deixe o Identity se configurar
            base.OnModelCreating(modelBuilder);

            // 2) Constraints e índices de ApplicationUser
            // ATENÇÃO: se CPF/PhoneNumber forem opcionais, um índice UNIQUE pode bloquear múltiplos NULLs no SQL Server.
            // Se forem opcionais, troque por índice filtrado (HasFilter("[CPF] IS NOT NULL"))
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

            // Ajustes em Payment (colunas opcionais)
            modelBuilder.Entity<PaymentModel>(e =>
            {
                e.Property(p => p.CardBrand).IsRequired(false);
                e.Property(p => p.CardLast4).IsRequired(false);
                e.Property(p => p.ClientSecretOrToken).IsRequired(false);
                e.Property(p => p.PixQrBase64Png).IsRequired(false);
                e.Property(p => p.FailureReason).IsRequired(false);
            });

            // MerchantPaymentConfig -> ApplicationUser (FK forte, int)
            modelBuilder.Entity<MerchantPaymentConfig>(e =>
            {
                e.Property(p => p.Provider).HasMaxLength(100).IsRequired();
                e.Property(p => p.ConfigJson).IsRequired();

                e.HasIndex(p => new { p.UserId, p.Provider }).IsUnique();

                e.HasOne<ApplicationUser>()     // FK para o seu user (Identity<int>)
                 .WithMany()
                 .HasForeignKey(p => p.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // dentro do OnModelCreating, após o base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<CategoriaModel>(b =>
            {
                b.ToTable("Categorias");                 // usa a tabela que já existe
                b.Property(x => x.Nome).HasMaxLength(50).IsRequired();
                b.Property(x => x.Descricao).HasMaxLength(200);
            });

            // Oculta pedidos soft-deletados em TODAS as queries
            modelBuilder.Entity<PedidoModel>()
                .HasQueryFilter(p => !p.IsDeleted);

            // Garanta cascade para hard delete (caso algum dia precise)
            modelBuilder.Entity<PedidoItemModel>()
                .HasOne(i => i.Pedido)
                .WithMany(p => p.PedidoItens)
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PedidoModel>()
                .HasOne(p => p.Endereco)
                .WithMany() // ou .WithMany(e => e.Pedidos) se existir
                .HasForeignKey(p => p.EnderecoId)
                .OnDelete(DeleteBehavior.Restrict); // evita cascata acidental

            modelBuilder.Entity<EnderecoModel>(b =>
            {
                b.Property(e => e.Principal)
                 .HasDefaultValue(false)
                 .IsRequired();
                // (opcional) reforçar tamanhos/requireds conforme DataAnnotations
            });

            // TODO: adicione aqui outras configurações de domínio (tamanhos, required, etc.)
        }
    }
}
