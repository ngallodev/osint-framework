using System;
using System.Collections.Generic;

namespace OsintBackend.Models
{
    public class AiJobErrorInfo
    {
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? Code { get; set; }
        public bool IsRetryable { get; set; } = true;
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string>? Metadata { get; set; } = new();
    }

    public class AiJobCompletionPayload
    {
        public string RawResult { get; set; } = string.Empty;
        public AiJobStructuredResult? StructuredResult { get; set; }
        public string ResultFormat { get; set; } = AiJobResultFormats.MarkdownSectionsV1;
        public string? PromptUsed { get; set; }
        public AiJobDebugInfo? DebugInfo { get; set; }
    }

    public class AiJobFailurePayload
    {
        public AiJobErrorInfo Error { get; set; } = new();
        public string? PromptUsed { get; set; }
    }
}
