using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurchProjection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_names",
                columns: table => new
                {
                    translation_id = table.Column<string>(type: "TEXT", nullable: false),
                    book_id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    abbrev = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_names", x => new { x.translation_id, x.book_id });
                });

            migrationBuilder.CreateTable(
                name: "live_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    service_id = table.Column<string>(type: "TEXT", nullable: true),
                    live_item_id = table.Column<string>(type: "TEXT", nullable: true),
                    live_page_index = table.Column<int>(type: "INTEGER", nullable: false),
                    live_media_available = table.Column<bool>(type: "INTEGER", nullable: false),
                    preview_item_id = table.Column<string>(type: "TEXT", nullable: true),
                    preview_page_index = table.Column<int>(type: "INTEGER", nullable: false),
                    preview_media_available = table.Column<bool>(type: "INTEGER", nullable: false),
                    blackout = table.Column<bool>(type: "INTEGER", nullable: false),
                    skipped_json = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    filename = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    duration_ms = table.Column<int>(type: "INTEGER", nullable: true),
                    width = table.Column<int>(type: "INTEGER", nullable: true),
                    height = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    service_date = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_services", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "songs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    author = table.Column<string>(type: "TEXT", nullable: true),
                    ccli = table.Column<string>(type: "TEXT", nullable: true),
                    language = table.Column<string>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_songs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "translations",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    abbrev = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    language = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "verses",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    translation_id = table.Column<string>(type: "TEXT", nullable: false),
                    book_id = table.Column<int>(type: "INTEGER", nullable: false),
                    chapter = table.Column<int>(type: "INTEGER", nullable: false),
                    verse = table.Column<int>(type: "INTEGER", nullable: false),
                    text = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_items",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    label = table.Column<string>(type: "TEXT", nullable: false),
                    ref_json = table.Column<string>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    service_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_items_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "song_pages",
                columns: table => new
                {
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    song_id = table.Column<string>(type: "TEXT", nullable: false),
                    section_label = table.Column<string>(type: "TEXT", nullable: true),
                    text = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_song_pages", x => new { x.song_id, x.position });
                    table.ForeignKey(
                        name: "FK_song_pages_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_items_service_id",
                table: "service_items",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_verses_translation_id_book_id_chapter_verse",
                table: "verses",
                columns: new[] { "translation_id", "book_id", "chapter", "verse" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_names");

            migrationBuilder.DropTable(
                name: "live_state");

            migrationBuilder.DropTable(
                name: "media");

            migrationBuilder.DropTable(
                name: "service_items");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "song_pages");

            migrationBuilder.DropTable(
                name: "translations");

            migrationBuilder.DropTable(
                name: "verses");

            migrationBuilder.DropTable(
                name: "services");

            migrationBuilder.DropTable(
                name: "songs");
        }
    }
}
