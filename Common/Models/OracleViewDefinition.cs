using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Common.Models
{
    [PrimaryKey(nameof(Owner), nameof(Name))]
    public class OracleViewDefinition
    {
        public string Owner { get; set; }

        public string Name { get; set; }

        public string Definition { get; set; }

        public OracleViewDefinition(string name, string definition)
        {
            Name = name;
            Definition = definition;
        }
    }
}