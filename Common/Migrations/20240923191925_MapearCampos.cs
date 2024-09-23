using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class MapearCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Build_Commit_CommitId",
                table: "Build");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipeline_Build_LastId",
                table: "Pipeline");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipeline_Build_LastSuccessfulId",
                table: "Pipeline");

            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Pipeline_PipelineId",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_PipelineId",
                table: "Repositories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Pipeline",
                table: "Pipeline");

            migrationBuilder.DropIndex(
                name: "IX_Pipeline_LastId",
                table: "Pipeline");

            migrationBuilder.DropIndex(
                name: "IX_Pipeline_LastSuccessfulId",
                table: "Pipeline");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Commit",
                table: "Commit");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Build",
                table: "Build");

            migrationBuilder.DropColumn(
                name: "PipelineId",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "LastId",
                table: "Pipeline");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulId",
                table: "Pipeline");

            migrationBuilder.RenameTable(
                name: "Pipeline",
                newName: "Pipelines");

            migrationBuilder.RenameTable(
                name: "Commit",
                newName: "Commits");

            migrationBuilder.RenameTable(
                name: "Build",
                newName: "Builds");

            migrationBuilder.RenameIndex(
                name: "IX_Build_CommitId",
                table: "Builds",
                newName: "IX_Builds_CommitId");

            migrationBuilder.AddColumn<int>(
                name: "LastBuildId",
                table: "Pipelines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSuccessfulBuildId",
                table: "Pipelines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepositoryId",
                table: "Pipelines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Pipelines",
                table: "Pipelines",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Commits",
                table: "Commits",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Builds",
                table: "Builds",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_LastBuildId",
                table: "Pipelines",
                column: "LastBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_LastSuccessfulBuildId",
                table: "Pipelines",
                column: "LastSuccessfulBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_RepositoryId",
                table: "Pipelines",
                column: "RepositoryId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Commits_CommitId",
                table: "Builds",
                column: "CommitId",
                principalTable: "Commits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Commits_CommitId",
                table: "Builds");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Builds_LastBuildId",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Builds_LastSuccessfulBuildId",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Repositories_RepositoryId",
                table: "Pipelines");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Pipelines",
                table: "Pipelines");

            migrationBuilder.DropIndex(
                name: "IX_Pipelines_LastBuildId",
                table: "Pipelines");

            migrationBuilder.DropIndex(
                name: "IX_Pipelines_LastSuccessfulBuildId",
                table: "Pipelines");

            migrationBuilder.DropIndex(
                name: "IX_Pipelines_RepositoryId",
                table: "Pipelines");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Commits",
                table: "Commits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Builds",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "LastBuildId",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulBuildId",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "RepositoryId",
                table: "Pipelines");

            migrationBuilder.RenameTable(
                name: "Pipelines",
                newName: "Pipeline");

            migrationBuilder.RenameTable(
                name: "Commits",
                newName: "Commit");

            migrationBuilder.RenameTable(
                name: "Builds",
                newName: "Build");

            migrationBuilder.RenameIndex(
                name: "IX_Builds_CommitId",
                table: "Build",
                newName: "IX_Build_CommitId");

            migrationBuilder.AddColumn<int>(
                name: "PipelineId",
                table: "Repositories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastId",
                table: "Pipeline",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastSuccessfulId",
                table: "Pipeline",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Pipeline",
                table: "Pipeline",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Commit",
                table: "Commit",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Build",
                table: "Build",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_PipelineId",
                table: "Repositories",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipeline_LastId",
                table: "Pipeline",
                column: "LastId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipeline_LastSuccessfulId",
                table: "Pipeline",
                column: "LastSuccessfulId");

            migrationBuilder.AddForeignKey(
                name: "FK_Build_Commit_CommitId",
                table: "Build",
                column: "CommitId",
                principalTable: "Commit",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Pipeline_Build_LastId",
                table: "Pipeline",
                column: "LastId",
                principalTable: "Build",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pipeline_Build_LastSuccessfulId",
                table: "Pipeline",
                column: "LastSuccessfulId",
                principalTable: "Build",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Pipeline_PipelineId",
                table: "Repositories",
                column: "PipelineId",
                principalTable: "Pipeline",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
