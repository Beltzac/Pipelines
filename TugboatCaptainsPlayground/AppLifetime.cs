namespace TugboatCaptainsPlayground
{
    public static class AppLifetime
    {
        public static Action ExitAction { get; set; }

        public static void Exit()
        {
            ExitAction?.Invoke();
        }
    }
}