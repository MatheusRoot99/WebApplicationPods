using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposBebidaProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BebidaEmbalagem",
                table: "Produtos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BebidaQtdPorEmbalagem",
                table: "Produtos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BebidaTeorAlcoolico",
                table: "Produtos",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BebidaTipo",
                table: "Produtos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BebidaVolumeMl",
                table: "Produtos",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BebidaEmbalagem",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "BebidaQtdPorEmbalagem",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "BebidaTeorAlcoolico",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "BebidaTipo",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "BebidaVolumeMl",
                table: "Produtos");
        }
    }
}
