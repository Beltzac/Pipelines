namespace Common.Models
{
    public class MongoMessage
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string Key { get; set; }
        public string Idioma { get; set; }
        public int? Nivel { get; set; }
        public string Titulo { get; set; }
        public string Texto { get; set; }
        public List<string> Tags { get; set; }
        public bool RevisaoPendente { get; set; }

        public Metadata Inclusao { get; set; }
        public Metadata Alteracao { get; set; }
        public Metadata UltimaInteracao { get; set; }
    }

    public class Metadata
    {
        public DateTime Data { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserLogin { get; set; }
        public string ProcurationId { get; set; }
        public string CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string AggregationId { get; set; }
    }
}