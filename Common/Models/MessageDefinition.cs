namespace Common.Models
{
    public class MessageDefinition
    {
        public long IdMensagem { get; set; }
        public long IdSistemaMensagem { get; set; }
        public long IdDestinoMensagem { get; set; }
        public long IdGrupoMensagem { get; set; }
        public bool Verificado { get; set; }
        public string Modulo { get; set; }
        public string Codigo { get; set; }
        public string Prefixo { get; set; }
        public string Elemento { get; set; }
        public string Observacao { get; set; }
        public Dictionary<int, MessageLanguageDefinition> Languages { get; set; } = new();
        public string Key { get; set; }
    }

    public class MessageLanguageDefinition
    {
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public string Ajuda { get; set; }
        public int Idioma { get; set; }
    }
}