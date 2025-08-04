using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class AddPullRequestProjectAndRepoIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PullRequest_Repositories_RepositoryId",
                table: "PullRequest");

            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "PullRequest",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "PullRequest",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddForeignKey(
                name: "FK_PullRequest_Repositories_RepositoryId",
                table: "PullRequest",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PullRequest_Repositories_RepositoryId",
                table: "PullRequest");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PullRequest");

            migrationBuilder.AlterColumn<Guid>(
                name: "RepositoryId",
                table: "PullRequest",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_PullRequest_Repositories_RepositoryId",
                table: "PullRequest",
                column: "RepositoryId",
                principalTable: "Repositories",
                principalColumn: "Id");
        }
    }
}
