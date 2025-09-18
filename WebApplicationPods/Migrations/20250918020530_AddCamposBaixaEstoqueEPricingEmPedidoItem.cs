using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposBaixaEstoqueEPricingEmPedidoItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstoqueBaixado",
                table: "PedidoItens",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstoqueBaixadoEm",
                table: "PedidoItens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sabor",
                table: "PedidoItens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstoqueBaixado",
                table: "PedidoItens");

            migrationBuilder.DropColumn(
                name: "EstoqueBaixadoEm",
                table: "PedidoItens");

            migrationBuilder.DropColumn(
                name: "Sabor",
                table: "PedidoItens");
        }
    }
}
