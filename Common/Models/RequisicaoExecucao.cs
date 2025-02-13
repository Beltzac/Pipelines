namespace Common.Models
{
    public class RequisicaoExecucao
    {
        public string Source { get; set; }
        public int IdExecucao { get; set; }
        public string? HttpMethod { get; set; }
        public string? HttpStatusCode { get; set; }
        public string? Requisicao { get; set; }
        public string? Resposta { get; set; }
        public string? Erro { get; set; }
        public string NomeFluxo { get; set; }
        public string? EndPoint { get; set; }
        public string? Url { get; set; }
        public TimeSpan? Duration { get; set; }
        public DateTime DataInicio { get; set; }
        public int IdUsuarioInclusao { get; set; }
        public string? UserLogin { get; set; }
        public int TotalCount { get; set; }
    }
}
