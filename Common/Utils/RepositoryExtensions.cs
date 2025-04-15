namespace Common.Utils
{
    /// <summary>
    /// Extension methods for Repository operations
    /// </summary>
    public static class RepositoryExtensions
    {
        public static int MinUpdateTime { get; set; } = 60 * 3; // default 3 minutes in seconds
        public static int MaxUpdateTime { get; set; } = 60 * 60 * 4; // default 4 hours in seconds
        public const int MIN_TIME_SINCE_UPDATE = 6 * 60 * 60; // 6 hours in seconds
        public const int MAX_TIME_SINCE_UPDATE = 60 * 60 * 24 * 30; // 30 days in seconds
        public const int MAX_RANDOM_OFFSET = 30; // Maximum random offset in seconds

        private static readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random());

        /// <summary>
        /// Calculates the number of seconds until the next update should occur
        /// </summary>
        public static int SecondsToNextUpdate(this Repository repo)
        {
            if (repo == null)
                return MaxUpdateTime;

            var lastUpdated = repo.Pipeline?.Last?.Changed;
            var timeSinceUpdate = lastUpdated.HasValue
                ? (int)(DateTime.UtcNow - lastUpdated.Value).TotalSeconds
                : MAX_TIME_SINCE_UPDATE;

            // Linear scaling between MinUpdateTime and MaxUpdateTime
            var scaledValue = ScaleValue(timeSinceUpdate, MinUpdateTime, MaxUpdateTime,
                                       MIN_TIME_SINCE_UPDATE, MAX_TIME_SINCE_UPDATE);

            // Add random offset to stagger jobs
            var randomOffset = _random.Value.Next(0, MAX_RANDOM_OFFSET);

            return Math.Min(scaledValue + randomOffset, MaxUpdateTime);
        }

        /// <summary>
        /// Linearly scales a value from one range to another
        /// </summary>
        public static int ScaleValue(int value, int minValue, int maxValue,
                                   int minTimeSinceUpdate, int maxTimeSinceUpdate)
        {
            // Parameter validation
            minTimeSinceUpdate = Math.Max(minTimeSinceUpdate, 1);
            maxTimeSinceUpdate = Math.Max(maxTimeSinceUpdate, minTimeSinceUpdate + 1);
            value = Math.Clamp(value, minTimeSinceUpdate, maxTimeSinceUpdate);

            if (minTimeSinceUpdate == maxTimeSinceUpdate)
                return minValue;

            // Linear scaling calculation
            double normalized = (double)(value - minTimeSinceUpdate) / (maxTimeSinceUpdate - minTimeSinceUpdate);
            double scaledValue = minValue + (maxValue - minValue) * normalized;

            return (int)Math.Clamp(scaledValue, minValue, maxValue);
        }
    }
}
