using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;
using WebApplicationPods.Services.Interface;

namespace WebApplicationPods.Data
{
    public class BancoContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        private readonly int _lojaId;
        private readonly bool _hasLoja;
        private readonly bool _designTime;

        public BancoContext(DbContextOptions<BancoContext> options, ICurrentLojaService currentLoja)
            : base(options)
        {
            _designTime = false;
            var loja = currentLoja?.LojaId;
            _hasLoja = loja.HasValue;
            _lojaId = loja ?? 0;
        }

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
        public DbSet<PedidoHistoricoModel> PedidoHistoricos => Set<PedidoHistoricoModel>();
        public DbSet<EntregadorModel> Entregadores => Set<EntregadorModel>();
        public DbSet<EntregaModel> Entregas => Set<EntregaModel>();
        public DbSet<ProdutoVariacaoModel> ProdutoVariacoes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(b =>
            {
                b.HasIndex(u => u.CPF)
                    .IsUnique()
                    .HasFilter("[CPF] IS NOT NULL");

                b.HasIndex(u => u.PhoneNumber)
                    .IsUnique()
                    .HasFilter("[PhoneNumber] IS NOT NULL");

                b.HasOne(u => u.Loja)
                    .WithMany()
                    .HasForeignKey(u => u.LojaId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

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

                b.HasQueryFilter(x => _designTime || (!_hasLoja || x.LojaId == _lojaId));
            });

            modelBuilder.Entity<CategoriaModel>(b =>
            {
                b.ToTable("Categorias");
                b.Property(x => x.Nome).HasMaxLength(50).IsRequired();
                b.Property(x => x.Descricao).HasMaxLength(200);
                b.HasIndex(x => new { x.LojaId, x.Nome }).IsUnique(false);
                b.HasQueryFilter(x => _designTime || (!_hasLoja || x.LojaId == _lojaId));
            });

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

                b.HasQueryFilter(x => _designTime || (!_hasLoja || x.LojaId == _lojaId));
            });

            modelBuilder.Entity<ProdutoAtributoModel>(b =>
            {
                b.ToTable("ProdutoAtributos");
                b.Property(x => x.Chave).HasMaxLength(50).IsRequired();
                b.Property(x => x.Valor).HasMaxLength(120).IsRequired();
                b.HasIndex(x => new { x.ProdutoId, x.Chave });
            });

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

            modelBuilder.Entity<EnderecoModel>().HasQueryFilter(e => e.Ativo);

            modelBuilder.Entity<PedidoModel>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (_designTime || (!_hasLoja || x.LojaId == _lojaId)));

            modelBuilder.Entity<ProdutoVariacaoModel>()
                .HasOne(v => v.Produto)
                .WithMany(p => p.Variacoes)
                .HasForeignKey(v => v.ProdutoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PedidoHistoricoModel>(b =>
            {
                b.ToTable("PedidoHistoricos");

                b.Property(x => x.NovoStatus).HasMaxLength(64).IsRequired();
                b.Property(x => x.StatusAnterior).HasMaxLength(64);
                b.Property(x => x.Observacao).HasMaxLength(500);
                b.Property(x => x.UsuarioResponsavelId).HasMaxLength(100);
                b.Property(x => x.NomeResponsavel).HasMaxLength(120);
                b.Property(x => x.Origem).HasMaxLength(60);

                b.HasOne(x => x.Pedido)
                    .WithMany(p => p.Historico)
                    .HasForeignKey(x => x.PedidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => new { x.PedidoId, x.DataCadastro });
            });

            modelBuilder.Entity<PedidoModel>(b =>
            {
                b.HasOne(x => x.Entregador)
                    .WithMany()
                    .HasForeignKey(x => x.EntregadorId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(x => x.Entrega)
                    .WithOne(x => x.Pedido)
                    .HasForeignKey<EntregaModel>(x => x.PedidoId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EntregadorModel>(b =>
            {
                b.ToTable("Entregadores");

                b.Property(x => x.Nome).HasMaxLength(150).IsRequired();
                b.Property(x => x.Telefone).HasMaxLength(20).IsRequired();
                b.Property(x => x.Veiculo).HasMaxLength(80);
                b.Property(x => x.PlacaVeiculo).HasMaxLength(20);
                b.Property(x => x.Observacoes).HasMaxLength(500);

                b.HasOne(x => x.Loja)
                    .WithMany()
                    .HasForeignKey(x => x.LojaId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Usuario)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(x => new { x.LojaId, x.Nome });
                b.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");

                b.HasQueryFilter(x => _designTime || (!_hasLoja || x.LojaId == _lojaId));
            });

            modelBuilder.Entity<EntregaModel>(b =>
            {
                b.ToTable("Entregas");

                b.Property(x => x.Status).HasMaxLength(40).IsRequired();
                b.Property(x => x.Observacao).HasMaxLength(500);
                b.Property(x => x.ComprovanteEntregaUrl).HasMaxLength(500);

                b.HasOne(x => x.Pedido)
                    .WithOne(x => x.Entrega)
                    .HasForeignKey<EntregaModel>(x => x.PedidoId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Entregador)
                    .WithMany()
                    .HasForeignKey(x => x.EntregadorId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(x => x.PedidoId).IsUnique();
                b.HasIndex(x => new { x.EntregadorId, x.Status });
            });
        }
    }
}