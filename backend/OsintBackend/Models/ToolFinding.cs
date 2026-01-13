using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OsintBackend.Models
{
    /// <summary>
    /// Represents a single finding/result from an external tool execution
    /// </summary>
    public class ToolFinding
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Reference to the tool execution that produced this finding
        /// </summary>
        public int ToolExecutionId { get; set; }

        [ForeignKey("ToolExecutionId")]
        public virtual ToolExecution ToolExecution { get; set; } = null!;

        /// <summary>
        /// Type of finding (domain, email, username, ip, hash, breach, social_media, etc.)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FindingType { get; set; } = string.Empty;

        /// <summary>
        /// Primary value/data point (domain name, email, username, etc.)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Additional details specific to this finding
        /// </summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// Severity level (Info, Low, Medium, High, Critical)
        /// </summary>
        [MaxLength(50)]
        public string Severity { get; set; } = "Info";

        /// <summary>
        /// Confidence score (0-100)
        /// </summary>
        public int? ConfidenceScore { get; set; }

        /// <summary>
        /// Source of this finding within the tool (e.g., SpiderFoot module name)
        /// </summary>
        [MaxLength(200)]
        public string? Source { get; set; }

        /// <summary>
        /// Related entities (as JSON array)
        /// Example: ["example.com", "123.45.67.89"]
        /// </summary>
        [Column(TypeName = "json")]
        public string RelatedEntities { get; set; } = "[]";

        /// <summary>
        /// Raw/full finding data from the tool (JSON)
        /// </summary>
        [Column(TypeName = "json")]
        public string RawData { get; set; } = "{}";

        /// <summary>
        /// External reference/URL for this finding
        /// </summary>
        [MaxLength(500)]
        public string? ReferenceUrl { get; set; }

        /// <summary>
        /// When this finding was discovered
        /// </summary>
        public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this finding has been verified/analyzed
        /// </summary>
        public bool IsVerified { get; set; } = false;

        /// <summary>
        /// Notes from analyst review
        /// </summary>
        [MaxLength(1000)]
        public string? AnalystNotes { get; set; }
    }
}
