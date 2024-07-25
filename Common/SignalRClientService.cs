using Microsoft.AspNetCore.SignalR.Client;

namespace Common
{
    public class SignalRClientService
    {
        private readonly HubConnection _hubConnection;

        public SignalRClientService()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:8001/buildInfoHub")
                .Build();
        }

        public async Task StartAsync(Action<int> action)
        {

            if (_hubConnection.State != HubConnectionState.Disconnected) {
                return;
            }

            _hubConnection.On<int>("Update", action);

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