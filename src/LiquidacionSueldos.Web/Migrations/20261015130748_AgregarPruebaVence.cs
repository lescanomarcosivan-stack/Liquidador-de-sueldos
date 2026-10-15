using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiquidacionSueldos.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPruebaVence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PruebaVence",
                table: "Estudios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PruebaVence",
                table: "Estudios");
        }
    }
}
