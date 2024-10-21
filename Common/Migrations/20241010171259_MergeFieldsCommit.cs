using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class MergeFieldsCommit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Commits_CommitId",
                table: "Builds");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "Commits",
                newName: "CommitMessage");

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                table: "Commits",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<DateTime>(
                name: "CommitDate",
                table: "Commits",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "JiraCardID",
                table: "Commits",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "Commits",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "RepoName",
                table: "Commits",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Commits_CommitId",
                table: "Builds",
                column: "CommitId",
                principalTable: "Commits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Builds_Commits_CommitId",
                table: "Builds");

            migrationBuilder.DropColumn(
                name: "BranchName",
                table: "Commits");

            migrationBuilder.DropColumn(
                name: "CommitDate",
                table: "Commits");

            migrationBuilder.DropColumn(
                name: "JiraCardID",
                table: "Commits");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "Commits");

            migrationBuilder.DropColumn(
                name: "RepoName",
                table: "Commits");

            migrationBuilder.RenameColumn(
                name: "CommitMessage",
                table: "Commits",
                newName: "Message");

            migrationBuilder.AddForeignKey(
                name: "FK_Builds_Commits_CommitId",
                table: "Builds",
                column: "CommitId",
                principalTable: "Commits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
