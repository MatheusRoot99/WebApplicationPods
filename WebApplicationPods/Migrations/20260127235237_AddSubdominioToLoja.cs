using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddSubdominioToLoja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subdominio",
                table: "Lojas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            // Evita quebrar o unique index se já existirem lojas
            migrationBuilder.Sql(@"
UPDATE Lojas
SET Subdominio = CONCAT('loja-', Id)
WHERE (Subdominio IS NULL OR LTRIM(RTRIM(Subdominio)) = '');
");

            migrationBuilder.CreateIndex(
                name: "IX_Lojas_Subdominio",
                table: "Lojas",
                column: "Subdominio",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lojas_Subdominio",
                table: "Lojas");

            migrationBuilder.DropColumn(
                name: "Subdominio",
                table: "Lojas");
        }

    }
}
