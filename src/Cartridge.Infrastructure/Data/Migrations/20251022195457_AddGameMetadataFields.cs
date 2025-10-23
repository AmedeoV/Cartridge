using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cartridge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameMetadataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "UserGames",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Developer",
                table: "UserGames",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Genres",
                table: "UserGames",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Publisher",
                table: "UserGames",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseDate",
                table: "UserGames",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "UserGames");

            migrationBuilder.DropColumn(
                name: "Developer",
                table: "UserGames");

            migrationBuilder.DropColumn(
                name: "Genres",
                table: "UserGames");

            migrationBuilder.DropColumn(
                name: "Publisher",
                table: "UserGames");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "UserGames");
        }
    }
}
