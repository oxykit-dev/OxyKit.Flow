using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OxyKit.Flow.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddArtifactCacheAndWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArtifactCachedAt",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactContentType",
                table: "Jobs",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactFileName",
                table: "Jobs",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "Jobs",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtifactCachedAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ArtifactContentType",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ArtifactFileName",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "Jobs");
        }
    }
}
