using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiLojaBase_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MerchantPaymentConfigs_AspNetUsers_UserId",
                table: "MerchantPaymentConfigs");

            migrationBuilder.DropIndex(
                name: "IX_MerchantPaymentConfigs_UserId_Provider",
                table: "MerchantPaymentConfigs");

            migrationBuilder.AlterColumn<string>(
                name: "SaboresQuantidades",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ImagemUrl",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "Produtos",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Cor",
                table: "Produtos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "CodigoBarras",
                table: "Produtos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LojaId",
                table: "Produtos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Marca",
                table: "Produtos",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SKU",
                table: "Produtos",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LojaId",
                table: "Pedidos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "MerchantPaymentConfigs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "LojistaUserId",
                table: "LojaConfigs",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LojaId",
                table: "LojaConfigs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LojaId",
                table: "Categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LojaId",
                table: "Carrinhos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LojaId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Lojas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Plano = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DonoUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lojas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lojas_AspNetUsers_DonoUserId",
                        column: x => x.DonoUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProdutoAtributos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutoAtributos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProdutoAtributos_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_LojaId_CodigoBarras",
                table: "Produtos",
                columns: new[] { "LojaId", "CodigoBarras" });

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_LojaId_SKU",
                table: "Produtos",
                columns: new[] { "LojaId", "SKU" });

            migrationBuilder.CreateIndex(
                name: "IX_LojaConfigs_LojaId",
                table: "LojaConfigs",
                column: "LojaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LojaConfigs_LojistaUserId",
                table: "LojaConfigs",
                column: "LojistaUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_LojaId_Nome",
                table: "Categorias",
                columns: new[] { "LojaId", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_Lojas_DonoUserId",
                table: "Lojas",
                column: "DonoUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutoAtributos_ProdutoId_Chave",
                table: "ProdutoAtributos",
                columns: new[] { "ProdutoId", "Chave" });

            migrationBuilder.AddForeignKey(
                name: "FK_LojaConfigs_AspNetUsers_LojistaUserId",
                table: "LojaConfigs",
                column: "LojistaUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LojaConfigs_Lojas_LojaId",
                table: "LojaConfigs",
                column: "LojaId",
                principalTable: "Lojas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LojaConfigs_AspNetUsers_LojistaUserId",
                table: "LojaConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_LojaConfigs_Lojas_LojaId",
                table: "LojaConfigs");

            migrationBuilder.DropTable(
                name: "Lojas");

            migrationBuilder.DropTable(
                name: "ProdutoAtributos");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_LojaId_CodigoBarras",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_LojaId_SKU",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_LojaConfigs_LojaId",
                table: "LojaConfigs");

            migrationBuilder.DropIndex(
                name: "IX_LojaConfigs_LojistaUserId",
                table: "LojaConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_LojaId_Nome",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "CodigoBarras",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "LojaId",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Marca",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "SKU",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "LojaId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "LojaId",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "LojaId",
                table: "Categorias");

            migrationBuilder.DropColumn(
                name: "LojaId",
                table: "Carrinhos");

            migrationBuilder.DropColumn(
                name: "LojaId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "SaboresQuantidades",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImagemUrl",
                table: "Produtos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Descricao",
                table: "Produtos",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Cor",
                table: "Produtos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "MerchantPaymentConfigs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "LojistaUserId",
                table: "LojaConfigs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantPaymentConfigs_UserId_Provider",
                table: "MerchantPaymentConfigs",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MerchantPaymentConfigs_AspNetUsers_UserId",
                table: "MerchantPaymentConfigs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
