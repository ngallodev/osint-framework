using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OsintBackend.Models
{
    /// <summary>
    /// Represents execution of an external OSINT tool (SpiderFoot, Sherlock, etc.)
    /// </summary>
    public class ToolExecution
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Reference to the investigation this tool was run for
        /// </summary>
        public int OsintInvestigationId { get; set; }

        [ForeignKey("OsintInvestigationId")]
        public virtual OsintInvestigation Investigation { get; set; } = null!;

        /// <summary>
        /// Current execution status (Queued, Running, Completed, Failed)
        /// </summary>
        public ToolExecutionStatus Status { get; set; } = ToolExecutionStatus.Queued;

        /// <summary>
        /// Tool-specific configuration as JSON (API keys, scan parameters, etc.)
        /// </summary>
        [Column(TypeName = "json")]
        public string Configuration { get; set; } = "{}";

        /// <summary>
        /// When the execution started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the execution completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of findings discovered
        /// </summary>
        public int FindingCount { get; set; } = 0;

        /// <summary>
        /// Execution metadata and logs as JSON
        /// </summary>
        [Column(TypeName = "json")]
        public string ExecutionMetadata { get; set; } = "{}";

        /// <summary>
        /// When this record was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Related findings from this tool execution
        /// </summary>
        public virtual ICollection<ToolFinding> Findings { get; set; } = new List<ToolFinding>();
    }

    public enum ToolExecutionStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}
