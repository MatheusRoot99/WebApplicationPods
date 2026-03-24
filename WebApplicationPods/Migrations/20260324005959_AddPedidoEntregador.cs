using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidoEntregador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataAtribuicaoEntregador",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataEntregue",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSaiuParaEntrega",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EntregadorId",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_EntregadorId",
                table: "Pedidos",
                column: "EntregadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Entregadores_EntregadorId",
                table: "Pedidos",
                column: "EntregadorId",
                principalTable: "Entregadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Entregadores_EntregadorId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_EntregadorId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataAtribuicaoEntregador",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataEntregue",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataSaiuParaEntrega",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "EntregadorId",
                table: "Pedidos");
        }
    }
}
