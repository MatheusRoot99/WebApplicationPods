using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class EnderecoSoftDelete_And_FilteredIndex_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Enderecos_PrincipalPorCliente",
                table: "Enderecos");

            migrationBuilder.AlterColumn<bool>(
                name: "Ativo",
                table: "Enderecos",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.CreateIndex(
                name: "UX_Enderecos_PrincipalPorCliente",
                table: "Enderecos",
                column: "ClienteId",
                unique: true,
                filter: "[Principal] = 1 AND [Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Enderecos_PrincipalPorCliente",
                table: "Enderecos");

            migrationBuilder.AlterColumn<bool>(
                name: "Ativo",
                table: "Enderecos",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.CreateIndex(
                name: "UX_Enderecos_PrincipalPorCliente",
                table: "Enderecos",
                column: "ClienteId",
                unique: true,
                filter: "[Principal] = 1");
        }
    }
}
