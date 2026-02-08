using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraFoto.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisplaySettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SlideDuration = table.Column<int>(type: "INTEGER", nullable: false),
                    Transition = table.Column<int>(type: "INTEGER", nullable: false),
                    TransitionDuration = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceId = table.Column<long>(type: "INTEGER", nullable: true),
                    Shuffle = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImageFit = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisplaySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageProviders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Photos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    OriginalFilename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<double>(type: "REAL", nullable: true),
                    DateTaken = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    ProviderId = table.Column<long>(type: "INTEGER", nullable: true),
                    ProviderFileId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Photos_StorageProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "StorageProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PickerSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderId = table.Column<long>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PickerUri = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    MediaItemsSet = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PickerSessions_StorageProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "StorageProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CoverPhotoId = table.Column<long>(type: "INTEGER", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Albums_Photos_CoverPhotoId",
                        column: x => x.CoverPhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PhotoTags",
                columns: table => new
                {
                    PhotoId = table.Column<long>(type: "INTEGER", nullable: false),
                    TagId = table.Column<long>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoTags", x => new { x.PhotoId, x.TagId });
                    table.ForeignKey(
                        name: "FK_PhotoTags_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestLinks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MaxUploads = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentUploads = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedById = table.Column<long>(type: "INTEGER", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TargetAlbumId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestLinks_Albums_TargetAlbumId",
                        column: x => x.TargetAlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GuestLinks_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoAlbums",
                columns: table => new
                {
                    PhotoId = table.Column<long>(type: "INTEGER", nullable: false),
                    AlbumId = table.Column<long>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoAlbums", x => new { x.PhotoId, x.AlbumId });
                    table.ForeignKey(
                        name: "FK_PhotoAlbums_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoAlbums_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albums_CoverPhotoId",
                table: "Albums",
                column: "CoverPhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_Name",
                table: "Albums",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_SortOrder",
                table: "Albums",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_DisplaySettings_IsActive",
                table: "DisplaySettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLinks_CreatedById",
                table: "GuestLinks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLinks_ExpiresAt",
                table: "GuestLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GuestLinks_TargetAlbumId",
                table: "GuestLinks",
                column: "TargetAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoAlbums_AlbumId",
                table: "PhotoAlbums",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoAlbums_SortOrder",
                table: "PhotoAlbums",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_DateAdded",
                table: "Photos",
                column: "DateAdded");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_DateTaken",
                table: "Photos",
                column: "DateTaken");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_Filename",
                table: "Photos",
                column: "Filename");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ProviderId",
                table: "Photos",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ProviderId_ProviderFileId",
                table: "Photos",
                columns: new[] { "ProviderId", "ProviderFileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoTags_TagId",
                table: "PhotoTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_PickerSessions_CreatedAt",
                table: "PickerSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PickerSessions_ExpiresAt",
                table: "PickerSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PickerSessions_ProviderId",
                table: "PickerSessions",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_PickerSessions_SessionId",
                table: "PickerSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviders_IsEnabled",
                table: "StorageProviders",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviders_Type",
                table: "StorageProviders",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisplaySettings");

            migrationBuilder.DropTable(
                name: "GuestLinks");

            migrationBuilder.DropTable(
                name: "PhotoAlbums");

            migrationBuilder.DropTable(
                name: "PhotoTags");

            migrationBuilder.DropTable(
                name: "PickerSessions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Photos");

            migrationBuilder.DropTable(
                name: "StorageProviders");
        }
    }
}
