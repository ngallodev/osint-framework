using OsintBackend.Models;

namespace OsintBackend.GraphQL.InputTypes
{
    /// <summary>
    /// Input type for creating a new investigation
    /// </summary>
    public class CreateInvestigationInput
    {
        public string Target { get; set; } = string.Empty;
        public string InvestigationType { get; set; } = string.Empty;
        public string? RequestedBy { get; set; }
    }

    /// <summary>
    /// Input type for updating an investigation
    /// </summary>
    public class UpdateInvestigationInput
    {
        public int Id { get; set; }
        public string? Target { get; set; }
        public string? InvestigationType { get; set; }
        public InvestigationStatus? Status { get; set; }
        public string? RequestedBy { get; set; }
    }

    /// <summary>
    /// Input type for creating a result/ingesting data
    /// </summary>
    public class IngestResultInput
    {
        public int InvestigationId { get; set; }
        public string ToolName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string RawData { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? ConfidenceScore { get; set; }
    }

    /// <summary>
    /// Input type for bulk ingesting results
    /// </summary>
    public class BulkIngestResultInput
    {
        public int InvestigationId { get; set; }
        public List<IngestResultInput> Results { get; set; } = new List<IngestResultInput>();
    }
}
