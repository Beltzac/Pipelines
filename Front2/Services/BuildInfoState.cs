using System.Collections.Generic;
using Common.Models;

namespace Front2.Services
{
    public class BuildInfoState
    {
        public List<Repository> BuildInfos { get; set; } = new();
        public string Filter { get; set; } = string.Empty;
    }
}
