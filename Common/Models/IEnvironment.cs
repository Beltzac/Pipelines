namespace Common.Models
{
    public interface IEnvironment
    {
        string Name { get; set; }
        bool IsProduction { get; set; }
        string ServiceType { get; }
    }
}