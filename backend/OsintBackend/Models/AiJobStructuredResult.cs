using System.Collections.Generic;

namespace OsintBackend.Models
{
    public static class AiJobResultFormats
    {
        public const string MarkdownSectionsV1 = "markdown_sections_v1";
    }

    public class AiJobStructuredResult
    {
        public string FormatVersion { get; set; } = AiJobResultFormats.MarkdownSectionsV1;
        public List<AiJobStructuredResultSection> Sections { get; set; } = new();
        public Dictionary<string, string>? Metadata { get; set; } = new();
    }

    public class AiJobStructuredResultSection
    {
        public string Key { get; set; } = string.Empty;
        public string Heading { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
