using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCamposFiltrosProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Avaliacao",
                table: "Produtos",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "CapacidadeBateria",
                table: "Produtos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Cor",
                table: "Produtos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EmPromocao",
                table: "Produtos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MaisVendido",
                table: "Produtos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoPromocional",
                table: "Produtos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Puffs",
                table: "Produtos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Sabor",
                table: "Produtos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Avaliacao",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "CapacidadeBateria",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Cor",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "EmPromocao",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "MaisVendido",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "PrecoPromocional",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Puffs",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Sabor",
                table: "Produtos");
        }
    }
}
