using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiquidacionSueldos.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConceptos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConceptosAgregadosJson",
                table: "LiquidacionLineas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ObraSocialPorcentaje",
                table: "Empleados",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObraSocialSobreNoRem",
                table: "Empleados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Manual",
                table: "Conceptos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConceptosAgregadosJson",
                table: "LiquidacionLineas");

            migrationBuilder.DropColumn(
                name: "ObraSocialPorcentaje",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "ObraSocialSobreNoRem",
                table: "Empleados");

            migrationBuilder.DropColumn(
                name: "Manual",
                table: "Conceptos");
        }
    }
}
