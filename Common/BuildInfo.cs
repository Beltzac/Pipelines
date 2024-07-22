using LiteDB;
using System.Text.Json;

public class BuildInfo
{
    public int Id { get; set; }
    public BsonDocument Project { get; set; }
    public BsonDocument Pipeline { get; set; }
    public BsonDocument LatestBuildDetails { get; set; }
    public BsonDocument LatestBuildCommit { get; set; }
    public string ErrorLogs { get; set; }
    public bool Clonned { get; set; }
}