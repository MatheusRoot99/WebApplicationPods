using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrantionEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Custo",
                table: "Produtos",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Custo",
                table: "Produtos");
        }
    }
}
