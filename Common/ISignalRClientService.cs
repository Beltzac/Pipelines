
namespace Common
{
    public interface ISignalRClientService
    {
        Task Disconect();
        Task SendMessageAsync(string message);
        Task StartAsync(Action<Guid> action);
    }
}