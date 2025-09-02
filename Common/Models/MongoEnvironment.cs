namespace Common.Models
{
    public class MongoEnvironment : IEnvironment
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
        public bool IsProduction { get; set; }
        public string ServiceType => "Mongo";
    }
}