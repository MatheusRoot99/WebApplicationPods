using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposPedidoModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataAguardandoPagamento",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCancelado",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataConcluido",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInicioPreparo",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataPagamentoAprovado",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSaiuParaEntregaOuRetirada",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataAguardandoPagamento",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataCancelado",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataConcluido",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataInicioPreparo",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataPagamentoAprovado",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DataSaiuParaEntregaOuRetirada",
                table: "Pedidos");
        }
    }
}
