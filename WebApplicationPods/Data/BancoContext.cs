using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Data
{
    public class BancoContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        private readonly int _lojaId;      // nunca null
        private readonly bool _hasLoja;    // indica se tem loja
        private readonly bool _designTime; // migrations

        // ✅ Runtime
        public BancoContext(DbContextOptions<BancoContext> options, ICurrentLojaService currentLoja)
            : base(options)
        {
            _designTime = false;
            var loja = currentLoja?.LojaId;
            _hasLoja = loja.HasValue;
            _lojaId = loja ?? 0;
        }

        // ✅ Design-time (migrations)
        public BancoContext(DbContextOptions<BancoContext> options)
            : base(options)
        {
            _designTime = true;
            _hasLoja = false;
            _lojaId = 0;
        }

        public DbSet<LojaModel> Lojas => Set<LojaModel>();
        public DbSet<LojaConfig> LojaConfigs => Set<LojaConfig>();

        public DbSet<MerchantPaymentConfig> MerchantPaymentConfigs => Set<MerchantPaymentConfig>();
        public DbSet<CategoriaModel> Categorias => Set<CategoriaModel>();
        public DbSet<ProdutoModel> Produtos => Set<ProdutoModel>();
        public DbSet<ProdutoAtributoModel> ProdutoAtributos => Set<ProdutoAtributoModel>();
        public DbSet<ClienteModel> Clientes => Set<ClienteModel>();
        public DbSet<EnderecoModel> Enderecos => Set<EnderecoModel>();
        public DbSet<PedidoModel> Pedidos => Set<PedidoModel>();
        public DbSet<PedidoItemModel> PedidoItens => Set<PedidoItemModel>();
        public DbSet<UsuarioModel> Usuarios => Set<UsuarioModel>();
        public DbSet<CarrinhoModel> Carrinhos => Set<CarrinhoModel>();
        public DbSet<PaymentModel> Pagamentos => Set<PaymentModel>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== Identity / ApplicationUser =====
            modelBuilder.Entity<ApplicationUser>(b =>
            {
                b.HasIndex(u => u.CPF)
                    .IsUnique()
                    .HasFilter("[CPF] IS NOT NULL");

                b.HasIndex(u => u.PhoneNumber)
                    .IsUnique()
                    .HasFilter("[PhoneNumber] IS NOT NULL");

                // FK opcional do usuário para a loja
                b.HasOne(u => u.Loja)
                    .WithMany()
                    .HasForeignKey(u => u.LojaId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== LojaModel =====
            modelBuilder.Entity<LojaModel>(b =>
            {
                b.ToTable("Lojas");

                b.Property(x => x.Nome).HasMaxLength(120).IsRequired();

                b.Property(x => x.Subdominio)
                    .HasMaxLength(60)
                    .IsRequired();

                b.HasIndex(x => x.Subdominio).IsUnique();

                b.Property(x => x.Plano).HasMaxLength(30);

                b.HasOne(x => x.Dono)
                    .WithMany()
                    .HasForeignKey(x => x.DonoUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // ✅ 1:1 Loja <-> LojaConfig
                b.HasOne(x => x.Config)
                    .WithOne(c => c.Loja)
                    .HasForeignKey<LojaConfig>(c => c.LojaId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LojaConfig>(b =>
            {
                b.ToTable("LojaConfigs");
                b.HasIndex(x => x.LojaId).IsUnique();

                b.HasOne(x => x.Lojista)
                    .WithMany()
                    .HasForeignKey(x => x.LojistaUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // ✅ CORRIGIDO: se NÃO tem loja definida, NÃO filtra (admin/landing)
                b.HasQueryFilter(x => _designTime || (!_hasLoja || x.LojaId == _lojaId));
            });

            // ===== Categoria =====
            modelBuilder.Entity<CategoriaModel>(b =>
            {
                b.ToTable("Categorias");
                b.Property(x => x.Nome).HasMaxLength(50).IsRequired();
                b.Property(x => x.Descricao).HasMaxLength(200);
                b.HasIndex(x => new { x.LojaId, x.Nome }).IsUnique(false);

                // ✅ CORRIGIDO
                b.HasQueryFilter(x => _designTime || (!_hasLoja || x.LojaId == _lojaId));
            });

            // ===== Produto =====
            modelBuilder.Entity<ProdutoModel>(b =>
            {
                b.ToTable("Produtos");

                b.Property(x => x.Nome).HasMaxLength(100).IsRequired();
                b.Property(x => x.Marca).HasMaxLength(80);
                b.Property(x => x.SKU).HasMaxLength(40);
                b.Property(x => x.CodigoBarras).HasMaxLength(30);

                b.HasIndex(x => new { x.LojaId, x.SKU }).IsUnique(false);
                b.HasIndex(x => new { x.LojaId, x.CodigoBarras }).IsUnique(false);

                b.HasMany(x => x.Atributos)
                    .WithOne(a => a.Produto)
                    .HasForeignKey(a => a.ProdutoId)
                    .OnDelete(DeleteBehavior.Cascade);

                // ✅ CORRIGIDO
                b.HasQueryFilter(x => _designTime || (!_hasLoja || x.LojaId == _lojaId));
            });

            modelBuilder.Entity<ProdutoAtributoModel>(b =>
            {
                b.ToTable("ProdutoAtributos");
                b.Property(x => x.Chave).HasMaxLength(50).IsRequired();
                b.Property(x => x.Valor).HasMaxLength(120).IsRequired();
                b.HasIndex(x => new { x.ProdutoId, x.Chave });
            });

            // ===== Pedido / Payment =====
            modelBuilder.Entity<PedidoModel>()
                .HasOne(p => p.Endereco)
                .WithMany()
                .HasForeignKey(p => p.EnderecoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PaymentModel>()
                .HasOne(p => p.Pedido)
                .WithMany(o => o.Pagamentos)
                .HasForeignKey(p => p.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== Endereco =====
            modelBuilder.Entity<EnderecoModel>()
                .HasIndex(e => e.ClienteId)
                .HasDatabaseName("IX_Enderecos_ClienteId");

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

            // ===== Soft delete / ativos =====
            modelBuilder.Entity<EnderecoModel>().HasQueryFilter(e => e.Ativo);

            modelBuilder.Entity<PedidoModel>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (_designTime || (!_hasLoja || x.LojaId == _lojaId)));
        }
    }
}
