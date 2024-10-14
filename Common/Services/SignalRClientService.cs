using Microsoft.AspNetCore.SignalR.Client;

namespace Common.Services
{
    public class SignalRClientService : ISignalRClientService
    {
        private readonly HubConnection _hubConnection;

        public SignalRClientService()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"http://localhost:{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 8001 : 8002)}/buildInfoHub")
                .Build();
        }

        public async Task StartAsync(Action<Guid> action)
        {

            if (_hubConnection.State != HubConnectionState.Disconnected)
            {
                return;
            }

            _hubConnection.On("Update", action);

            await _hubConnection.StartAsync();
        }

        public async Task Disconect()
        {
            await _hubConnection.StopAsync();
        }

        public async Task SendMessageAsync(string message)
        {
            await _hubConnection.InvokeAsync("SendMessage", message);
        }
    }
}
