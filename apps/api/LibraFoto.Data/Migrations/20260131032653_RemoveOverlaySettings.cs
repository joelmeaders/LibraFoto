using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraFoto.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOverlaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverlayPosition",
                table: "DisplaySettings");

            migrationBuilder.DropColumn(
                name: "ShowDate",
                table: "DisplaySettings");

            migrationBuilder.DropColumn(
                name: "ShowLocation",
                table: "DisplaySettings");

            migrationBuilder.DropColumn(
                name: "ShowOverlay",
                table: "DisplaySettings");

            migrationBuilder.DropColumn(
                name: "ShowTime",
                table: "DisplaySettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OverlayPosition",
                table: "DisplaySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowDate",
                table: "DisplaySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowLocation",
                table: "DisplaySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOverlay",
                table: "DisplaySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowTime",
                table: "DisplaySettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
