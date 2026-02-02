using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraFoto.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImageFitToDisplaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImageFit",
                table: "DisplaySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageFit",
                table: "DisplaySettings");
        }
    }
}
