namespace OsintBackend.Services
{
    public class AiJobQueueOptions
    {
        public int MaxAttempts { get; set; } = 3;
        public double RetryBackoffSeconds { get; set; } = 5;
    }
}
