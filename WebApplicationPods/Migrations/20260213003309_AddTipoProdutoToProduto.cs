using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoProdutoToProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoProduto",
                table: "Produtos",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoProduto",
                table: "Produtos");
        }
    }
}
