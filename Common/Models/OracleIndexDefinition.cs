using Microsoft.EntityFrameworkCore;

namespace Common.Models
{
    [PrimaryKey(nameof(Owner), nameof(IndexName))]
    public class OracleIndexDefinition
    {
        public string Owner { get; set; }
        public string IndexName { get; set; }
        public string TableName { get; set; }
        public string IndexType { get; set; }
        public string Uniqueness { get; set; }
        public string Columns { get; set; }

        public OracleIndexDefinition()
        {
        }

        public OracleIndexDefinition(string owner, string indexName, string tableName, string indexType, string uniqueness, string columns)
        {
            Owner = owner;
            IndexName = indexName;
            TableName = tableName;
            IndexType = indexType;
            Uniqueness = uniqueness;
            Columns = columns;
        }
    }
}
