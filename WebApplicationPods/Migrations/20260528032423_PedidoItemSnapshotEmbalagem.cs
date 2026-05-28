using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class PedidoItemSnapshotEmbalagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbalagemNome",
                table: "PedidoItens",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProdutoNomeSnapshot",
                table: "PedidoItens",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoProdutoSnapshot",
                table: "PedidoItens",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadeVendaDescricao",
                table: "PedidoItens",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnidadesPorEmbalagem",
                table: "PedidoItens",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbalagemNome",
                table: "PedidoItens");

            migrationBuilder.DropColumn(
                name: "ProdutoNomeSnapshot",
                table: "PedidoItens");

            migrationBuilder.DropColumn(
                name: "TipoProdutoSnapshot",
                table: "PedidoItens");

            migrationBuilder.DropColumn(
                name: "UnidadeVendaDescricao",
                table: "PedidoItens");

            migrationBuilder.DropColumn(
                name: "UnidadesPorEmbalagem",
                table: "PedidoItens");
        }
    }
}
