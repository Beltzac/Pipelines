using Common.Services;

namespace Tests.Common.Services
{
    public class SignalRClientServiceTests
    {
        private readonly SignalRClientService _signalRClientService;

        public SignalRClientServiceTests()
        {
            _signalRClientService = new SignalRClientService();
        }

        //[Test]
        //public async Task StartAsync_ExecutesAction()
        //{
        //    bool actionExecuted = false;
        //    Action<Guid> action = (id) => actionExecuted = true;

        //    await _signalRClientService.StartAsync(action);

        //    Assert.True(actionExecuted, "The action was not executed as expected.");
        //}
    }
}
