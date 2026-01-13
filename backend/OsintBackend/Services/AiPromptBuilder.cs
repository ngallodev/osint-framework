using System.Collections.Generic;
using System.Linq;
using System.Text;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    public static class AiPromptBuilder
    {
        private const int MaxFindingsIncluded = 60;

        private static readonly string[] AnalysisSections =
        {
            "Executive Summary",
            "Key Findings",
            "Risks & Red Flags",
            "Recommended Next Steps",
            "Confidence & Caveats"
        };

        private static readonly string[] InferenceSections =
        {
            "Working Hypotheses",
            "Supporting Evidence",
            "Observed Gaps or Contradictions",
            "Suggested Follow-up Actions",
            "Confidence & Assumptions"
        };

        public static IReadOnlyList<string> AnalysisSectionHeadings => AnalysisSections;
        public static IReadOnlyList<string> InferenceSectionHeadings => InferenceSections;

        public static string BuildAnalysisPrompt(IEnumerable<OsintResult> results, string analysisType)
        {
            var resultList = (results ?? Enumerable.Empty<OsintResult>()).ToList();
            var includedResults = resultList.Take(MaxFindingsIncluded).ToList();
            var analysisDescriptor = string.IsNullOrWhiteSpace(analysisType) ? "comprehensive analysis" : analysisType;

            var builder = new StringBuilder();
            builder.AppendLine("You are an experienced OSINT analyst supporting an active investigation.");
            builder.AppendLine($"Perform a {analysisDescriptor} of the supplied findings and highlight what the team should focus on next.");
            builder.AppendLine();
            AppendSectionGuidance(builder, AnalysisSections);
            AppendFormattingGuidance(builder);
            AppendDatasetContext(builder, resultList.Count, includedResults);
            AppendGroupedFindings(builder, includedResults);

            return builder.ToString();
        }

        public static string BuildInferencePrompt(IEnumerable<OsintResult> results)
        {
            var resultList = (results ?? Enumerable.Empty<OsintResult>()).ToList();
            var includedResults = resultList.Take(MaxFindingsIncluded).ToList();

            var builder = new StringBuilder();
            builder.AppendLine("You are an OSINT inference engine tasked with synthesizing investigative hypotheses.");
            builder.AppendLine("Draw meaningful connections between the findings, call out evidence that supports each hypothesis, and note any gaps that limit confidence.");
            builder.AppendLine();
            AppendSectionGuidance(builder, InferenceSections);

            builder.AppendLine("Additional requirements:");
            builder.AppendLine("1. Highlight non-obvious relationships (shared entities, infrastructure, timelines).");
            builder.AppendLine("2. Differentiate between strongly supported inferences and speculative ideas.");
            builder.AppendLine("3. Propose pointed follow-up collection or verification tasks.");
            builder.AppendLine();

            AppendDatasetContext(builder, resultList.Count, includedResults);
            AppendGroupedFindings(builder, includedResults);

            return builder.ToString();
        }

        private static void AppendSectionGuidance(StringBuilder builder, IEnumerable<string> headings)
        {
            builder.AppendLine("Respond in Markdown using the following section headings exactly:");
            foreach (var heading in headings)
            {
                builder.AppendLine($"- ## {heading}");
            }
            builder.AppendLine();
        }

        private static void AppendFormattingGuidance(StringBuilder builder)
        {
            builder.AppendLine("Formatting guidance:");
            builder.AppendLine("1. Keep the Executive Summary to a concise 3-5 sentences.");
            builder.AppendLine("2. Use bullet lists for Key Findings, Risks, and Recommended Next Steps.");
            builder.AppendLine("3. Always note confidence levels using qualitative language (e.g., high/medium/low).");
            builder.AppendLine("4. Call out explicit data gaps or assumptions in the \"Confidence & Caveats\" section.");
            builder.AppendLine();
        }

        private static void AppendDatasetContext(StringBuilder builder, int totalFindings, IReadOnlyCollection<OsintResult> includedResults)
        {
            builder.AppendLine("### Dataset Context");
            if (totalFindings == 0)
            {
                builder.AppendLine("No findings were provided. Outline what information would be required to proceed.");
                builder.AppendLine();
                return;
            }

            var toolNames = includedResults
                .Select(r => r.ToolName)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var dataTypes = includedResults
                .Select(r => string.IsNullOrWhiteSpace(r.DataType) ? "Unspecified" : r.DataType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            builder.AppendLine($"- Total findings available: {totalFindings}");
            builder.AppendLine($"- Findings included in prompt: {includedResults.Count} (cap: {MaxFindingsIncluded})");
            builder.AppendLine($"- Data types represented: {string.Join(", ", dataTypes)}");
            builder.AppendLine($"- Source tools: {string.Join(", ", toolNames)}");
            builder.AppendLine();
            builder.AppendLine("Focus on the substance of the findings rather than reiterating this metadata.");
            builder.AppendLine();
        }

        private static void AppendGroupedFindings(StringBuilder builder, IEnumerable<OsintResult> results)
        {
            builder.AppendLine("### Normalised Findings");
            var grouped = GroupResults(results).ToList();

            if (grouped.Count == 0)
            {
                builder.AppendLine("- No findings supplied. Provide guidance on next steps to collect baseline intelligence.");
                return;
            }

            foreach (var block in grouped)
            {
                builder.AppendLine(block);
                builder.AppendLine();
            }
        }

        private static IEnumerable<string> GroupResults(IEnumerable<OsintResult> results)
        {
            return results
                .GroupBy(r => string.IsNullOrWhiteSpace(r.DataType) ? "Uncategorised" : r.DataType)
                .OrderBy(g => g.Key)
                .Select(group =>
                {
                    var lines = group
                        .Select(result =>
                        {
                            var summary = !string.IsNullOrWhiteSpace(result.Summary)
                                ? result.Summary
                                : !string.IsNullOrWhiteSpace(result.RawData)
                                    ? Truncate(result.RawData, 240)
                                    : "(no summary provided)";

                            return $"- **{result.ToolName}** ({result.CollectedAt:yyyy-MM-dd HH:mm} UTC): {summary}";
                        });

                    return $"#### {group.Key}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
                });
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength
                ? value
                : $"{value[..maxLength]}…";
        }
    }
}
