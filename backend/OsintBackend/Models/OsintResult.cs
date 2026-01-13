using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OsintBackend.Models
{
    public class OsintResult
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ToolName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string DataType { get; set; } = string.Empty;
        
        [Column(TypeName = "json")]
        public string RawData { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Summary { get; set; }
        
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(10)]
        public string? ConfidenceScore { get; set; }
        
        public int OsintInvestigationId { get; set; }
        
        [ForeignKey("OsintInvestigationId")]
        public virtual OsintInvestigation Investigation { get; set; } = null!;
    }
}
