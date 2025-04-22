using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class FixPullRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryPullRequests");

            migrationBuilder.CreateTable(
                name: "PullRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Url = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    SourceBranch = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    TargetBranch = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    ChangedFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PullRequest_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequest_RepositoryId",
                table: "PullRequest",
                column: "RepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PullRequest");

            migrationBuilder.CreateTable(
                name: "RepositoryPullRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceBranch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    TargetBranch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryPullRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryPullRequests_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryPullRequests_RepositoryId",
                table: "RepositoryPullRequests",
                column: "RepositoryId");
        }
    }
}
