using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OsintBackend.Models
{
    public class OsintInvestigation
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string Target { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string InvestigationType { get; set; } = string.Empty;
        
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(100)]
        public string? RequestedBy { get; set; }
        
        public InvestigationStatus Status { get; set; } = InvestigationStatus.Pending;
        
        public virtual ICollection<OsintResult> Results { get; set; } = new List<OsintResult>();

        public virtual ICollection<ToolExecution> ToolExecutions { get; set; } = new List<ToolExecution>();

        public virtual ICollection<AiJob> AiJobs { get; set; } = new List<AiJob>();
    }

    public enum InvestigationStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }
}
