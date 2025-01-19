namespace Common.Utils
{
    public static class RepositoryExtensions
    {
        private const int MIN_UPDATE_TIME = 60 * 3; // 3 minute in seconds
        private const int MAX_UPDATE_TIME = 60 * 60 * 4; // 4 hours in seconds
        private const int MIN_TIME_SINCE_UPDATE = 6 * 60 * 60; // 1/4 day in seconds
        private const int MAX_TIME_SINCE_UPDATE = 60 * 60 * 24 * 30; // 30 days in seconds

        private static readonly Random _random = new Random();

        public static int SecondsToNextUpdate(this Repository repo)
        {
            var lastUpdated = repo.Pipeline?.Last?.Changed;
            var timeSinceUpdate = lastUpdated.HasValue
                ? (int)(DateTime.UtcNow - lastUpdated.Value).TotalSeconds
                : MAX_TIME_SINCE_UPDATE;

            // Scale the value between MIN_UPDATE_TIME and MAX_UPDATE_TIME
            var scaledValue = ScaleValue(timeSinceUpdate, MIN_UPDATE_TIME, MAX_UPDATE_TIME, MIN_TIME_SINCE_UPDATE, MAX_TIME_SINCE_UPDATE);

            // Add a random offset to stagger the jobs
            var randomOffset = _random.Next(0, 30);

            return scaledValue + randomOffset;
        }

        private static int ScaleValue(int value, int minValue, int maxValue, int minTimeSinceUpdate, int maxTimeSinceUpdate)
        {
            // Ensure minTimeSinceUpdate is at least 1 to avoid logarithm of zero
            minTimeSinceUpdate = Math.Max(minTimeSinceUpdate, 1);

            // Ensure maxTimeSinceUpdate is greater than minTimeSinceUpdate
            maxTimeSinceUpdate = Math.Max(maxTimeSinceUpdate, minTimeSinceUpdate + 1);

            // Clamp value between minTimeSinceUpdate and maxTimeSinceUpdate
            value = Math.Clamp(value, minTimeSinceUpdate, maxTimeSinceUpdate);

            // Avoid division by zero in the logarithmic calculation
            if (minTimeSinceUpdate == maxTimeSinceUpdate)
                return minValue;

            // Compute logarithms
            double logValue = Math.Log(value);
            double logMin = Math.Log(minTimeSinceUpdate);
            double logMax = Math.Log(maxTimeSinceUpdate);

            // Scale the value between minValue and maxValue logarithmically
            double scaledValue = minValue + (maxValue - minValue) * (logValue - logMin) / (logMax - logMin);

            // Ensure scaledValue is within minValue and maxValue
            return (int)Math.Clamp(scaledValue, minValue, maxValue);
        }
    }
}
