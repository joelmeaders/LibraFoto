using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraFoto.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCachedFilesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedFiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccessCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CachedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    LastAccessedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PickerSessionId = table.Column<string>(type: "TEXT", nullable: true),
                    ProviderFileId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CachedFiles_StorageProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "StorageProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedFiles_FileHash",
                table: "CachedFiles",
                column: "FileHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CachedFiles_LastAccessedDate",
                table: "CachedFiles",
                column: "LastAccessedDate");

            migrationBuilder.CreateIndex(
                name: "IX_CachedFiles_ProviderFileId",
                table: "CachedFiles",
                column: "ProviderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CachedFiles_ProviderId",
                table: "CachedFiles",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_CachedFiles_ProviderId_ProviderFileId",
                table: "CachedFiles",
                columns: new[] { "ProviderId", "ProviderFileId" });
        }
    }
}
