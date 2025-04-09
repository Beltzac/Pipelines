using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class FileChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileChunkRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    ChunkStart = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunkEnd = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunkText = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Embedding = table.Column<byte[]>(type: "BLOB", nullable: true),
                    FileHash = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileChunkRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileChunkRecords");
        }
    }
}
