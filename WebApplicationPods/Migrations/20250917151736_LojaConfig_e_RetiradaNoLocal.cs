using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplicationPods.Migrations
{
    /// <inheritdoc />
    public partial class LojaConfig_e_RetiradaNoLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LojistaUserId",
                table: "LojaConfigs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "LojaConfigs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "LojaConfigs",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "LojaConfigs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Complemento",
                table: "LojaConfigs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "LojaConfigs",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "LojaConfigs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logradouro",
                table: "LojaConfigs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "LojaConfigs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapsPlaceUrl",
                table: "LojaConfigs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Numero",
                table: "LojaConfigs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Complemento",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Logradouro",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "MapsPlaceUrl",
                table: "LojaConfigs");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "LojaConfigs");

            migrationBuilder.AlterColumn<string>(
                name: "LojistaUserId",
                table: "LojaConfigs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }
    }
}
