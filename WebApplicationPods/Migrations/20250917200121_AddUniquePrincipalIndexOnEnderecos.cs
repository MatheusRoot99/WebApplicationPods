using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    public partial class AddUniquePrincipalIndexOnEnderecos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NÃO remova o índice do FK:
            // migrationBuilder.DropIndex(name: "IX_Enderecos_ClienteId", table: "Enderecos");

            // Cria o índice único filtrado para garantir apenas 1 principal por cliente
            migrationBuilder.CreateIndex(
                name: "UX_Enderecos_PrincipalPorCliente",
                table: "Enderecos",
                column: "ClienteId",
                unique: true,
                filter: "[Principal] = 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove apenas o índice filtrado
            migrationBuilder.DropIndex(
                name: "UX_Enderecos_PrincipalPorCliente",
                table: "Enderecos");

            // NÃO recrie o IX_Enderecos_ClienteId aqui, porque ele nunca foi removido.
        }
    }
}
