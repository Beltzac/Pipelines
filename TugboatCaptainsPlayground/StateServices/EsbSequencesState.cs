using System.Collections.Generic;
using Common.Models;

namespace TugboatCaptainsPlayground.Services
{
    public class EsbSequencesState
    {
        public List<SequenceInfo> Sequences { get; set; } = new List<SequenceInfo>();
        public bool IsLoading { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<EsbServerConfig> EsbServers { get; set; } = new List<EsbServerConfig>();
        public string SelectedEsbServerName { get; set; } = string.Empty;
        public string SearchTerm { get; set; } = string.Empty;
        public string FilterEnableStatistics { get; set; } = string.Empty;
        public string FilterEnableTracing { get; set; } = string.Empty;
        public string FilterIsEdited { get; set; } = string.Empty;
    }
}