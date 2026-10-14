using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiquidacionSueldos.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPlanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Estudios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Ilimitado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Estudios");
        }
    }
}
