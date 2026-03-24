using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddEntregaModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entregas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    EntregadorId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DataAtribuicao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataAceite = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataColeta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataSaidaParaEntrega = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataConclusao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ComprovanteEntregaUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entregas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entregas_Entregadores_EntregadorId",
                        column: x => x.EntregadorId,
                        principalTable: "Entregadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Entregas_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_EntregadorId_Status",
                table: "Entregas",
                columns: new[] { "EntregadorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_PedidoId",
                table: "Entregas",
                column: "PedidoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Entregas");
        }
    }
}
