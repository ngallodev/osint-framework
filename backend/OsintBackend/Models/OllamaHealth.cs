namespace OsintBackend.Models
{
    public class OllamaHealth
    {
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string? StatusMessage { get; set; }
        public double LatencyMilliseconds { get; set; }
        public List<string> Models { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
