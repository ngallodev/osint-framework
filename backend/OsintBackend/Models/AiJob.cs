using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OsintBackend.Models
{
    public class AiJob
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OsintInvestigationId { get; set; }

        [ForeignKey(nameof(OsintInvestigationId))]
        public OsintInvestigation? Investigation { get; set; }

        [Required]
        [MaxLength(32)]
        public string JobType { get; set; } = AiJobTypes.Analysis;

        [Required]
        public AiJobStatus Status { get; set; } = AiJobStatus.Queued;

        [MaxLength(100)]
        public string? Model { get; set; }

        [Column(TypeName = "longtext")]
        public string? Prompt { get; set; }

        [Column(TypeName = "longtext")]
        public string? Result { get; set; }

        [MaxLength(64)]
        public string ResultFormat { get; set; } = AiJobResultFormats.MarkdownSectionsV1;

        [Column(TypeName = "json")]
        public AiJobStructuredResult? StructuredResult { get; set; }

        [Column(TypeName = "longtext")]
        public string? Error { get; set; }

        [Column(TypeName = "json")]
        public AiJobErrorInfo? ErrorInfo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int AttemptCount { get; set; } = 0;

        public string? WorkerHost { get; set; }

        public DateTime? LastAttemptStartedAt { get; set; }

        public DateTime? LastAttemptCompletedAt { get; set; }

        public double? LastDurationMilliseconds { get; set; }

        public string? LastError { get; set; }

        /// <summary>
        /// Enable debug mode to capture detailed Ollama metrics
        /// </summary>
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Debug metadata including timing, token counts, and full prompt
        /// </summary>
        [Column(TypeName = "json")]
        public AiJobDebugInfo? DebugInfo { get; set; }
    }

    public static class AiJobTypes
    {
        public const string Analysis = "analysis";
        public const string Inference = "inference";
    }

    public enum AiJobStatus
    {
        Queued = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4
    }
}
