using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class IndexesPesquisa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Builds_LastBuildId",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Builds_LastSuccessfulBuildId",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Repositories_RepositoryId",
                table: "Pipelines");

            migrationBuilder.DropIndex(
                name: "IX_Pipelines_RepositoryId",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "RepositoryId",
                table: "Pipelines");

            migrationBuilder.AddColumn<int>(
                name: "PipelineId",
                table: "Repositories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Name",
                table: "Repositories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_PipelineId",
                table: "Repositories",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Project",
                table: "Repositories",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_Commits_AuthorName",
                table: "Commits",
                column: "AuthorName");

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Builds_LastBuildId",
                table: "Pipelines",
                column: "LastBuildId",
                principalTable: "Builds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Builds_LastSuccessfulBuildId",
                table: "Pipelines",
                column: "LastSuccessfulBuildId",
                principalTable: "Builds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Pipelines_PipelineId",
                table: "Repositories",
                column: "PipelineId",
                principalTable: "Pipelines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Builds_LastBuildId",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Builds_LastSuccessfulBuildId",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Pipelines_PipelineId",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_Name",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_PipelineId",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_Project",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Commits_AuthorName",
                table: "Commits");

            migrationBuilder.DropColumn(
                name: "PipelineId",
                table: "Repositories");

            migrationBuilder.AddColumn<Guid>(
                name: "RepositoryId",
                table: "Pipelines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_RepositoryId",
                table: "Pipelines",
                column: "RepositoryId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Builds_LastBuildId",
                table: "Pipelines",
                column: "LastBuildId",
                principalTable: "Builds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Builds_LastSuccessfulBuildId",
                table: "Pipelines",
                column: "LastSuccessfulBuildId",
                principalTable: "Builds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Repositories_RepositoryId",
                table: "Pipelines",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
