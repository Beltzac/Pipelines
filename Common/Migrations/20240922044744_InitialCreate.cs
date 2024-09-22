using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Commit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorEmail = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Build",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorLogs = table.Column<string>(type: "TEXT", nullable: false),
                    Queued = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Changed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CommitId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Build", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Build_Commit_CommitId",
                        column: x => x.CommitId,
                        principalTable: "Commit",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Pipeline",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LastId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSuccessfulId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipeline", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pipeline_Build_LastId",
                        column: x => x.LastId,
                        principalTable: "Build",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pipeline_Build_LastSuccessfulId",
                        column: x => x.LastSuccessfulId,
                        principalTable: "Build",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Project = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    CloneUrl = table.Column<string>(type: "TEXT", nullable: false),
                    MasterClonned = table.Column<bool>(type: "INTEGER", nullable: false),
                    PipelineId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_Pipeline_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "Pipeline",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Build_CommitId",
                table: "Build",
                column: "CommitId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipeline_LastId",
                table: "Pipeline",
                column: "LastId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipeline_LastSuccessfulId",
                table: "Pipeline",
                column: "LastSuccessfulId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_PipelineId",
                table: "Repositories",
                column: "PipelineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Repositories");

            migrationBuilder.DropTable(
                name: "Pipeline");

            migrationBuilder.DropTable(
                name: "Build");

            migrationBuilder.DropTable(
                name: "Commit");
        }
    }
}
