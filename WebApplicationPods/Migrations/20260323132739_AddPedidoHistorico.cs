using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidoHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PedidoHistoricos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    StatusAnterior = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NovoStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsuarioResponsavelId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NomeResponsavel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoHistoricos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidoHistoricos_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidoHistoricos_PedidoId_DataCadastro",
                table: "PedidoHistoricos",
                columns: new[] { "PedidoId", "DataCadastro" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidoHistoricos");
        }
    }
}
