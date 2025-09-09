using Common.Services.Interfaces;
using System;
using System.Collections.Generic;
using Common.Models; // Added using directive for SavedQuery

namespace TugboatCaptainsPlayground.Services
{
    public class DatabaseAssertsState : IPaginates<object>, ITracksLoading
    {
        public string SelectedOracleEnvironment { get; set; }
        public string SelectedMongoEnvironment { get; set; }
        public string SelectedQueryType { get; set; } = "SQL"; // Default to SQL
        public string MongoDatabaseName { get; set; }
        public string MongoCollectionName { get; set; }
        public string DateFieldForFiltering { get; set; } // Added property for date filtering field
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<Dictionary<string, object>> QueryResults { get; set; } = new List<Dictionary<string, object>>();
        public List<SavedQuery> SavedQueries { get; set; } = new List<SavedQuery>();

        // Unified property for the query text area
        public string CurrentQueryString { get; set; }

        // Properties for pagination (from IPaginates<object>)
        public List<object> PageItems { get; set; } = new List<object>();
        public int TotalCount { get; set; }
        public int PageSize { get; set; } = 10;
        public int CurrentPage { get; set; } = 1;

        // Properties for loading indicator (from ITracksLoading)
        public bool IsLoading { get; set; }
        public string ProgressLabel { get; set; }
        public int? ProgressValue { get; set; }
    }

    // Placeholder for SavedQuery class - will be defined in Common.Models
    // The generator will not pick this up here, it needs to be in a separate file or ConfigModel
    // public class SavedQuery
    // {
    //     public string Name { get; set; }
    //     public string QueryString { get; set; }
    //     public string QueryType { get; set; } // "SQL" or "MongoDB"
    // }
}