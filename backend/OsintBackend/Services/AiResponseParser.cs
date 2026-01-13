using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OsintBackend.Models;

namespace OsintBackend.Services
{
    public static class AiResponseParser
    {
        private static readonly Regex HeadingRegex = new(@"^##\s+(.*)$", RegexOptions.Compiled);
        private const string ParserVersion = "1.0.0";

        public static AiJobStructuredResult Parse(string jobType, string rawResponse)
        {
            var result = new AiJobStructuredResult
            {
                FormatVersion = AiJobResultFormats.MarkdownSectionsV1,
                Metadata = new Dictionary<string, string>
                {
                    ["jobType"] = jobType,
                    ["parserVersion"] = ParserVersion,
                    ["template"] = jobType == AiJobTypes.Inference ? "inference_v1" : "analysis_v1"
                }
            };

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                result.Sections.Add(new AiJobStructuredResultSection
                {
                    Heading = "Empty Response",
                    Key = "empty_response",
                    Content = string.Empty
                });
                result.Metadata["missingSections"] = string.Join(",", ExpectedSectionKeys(jobType));
                return result;
            }

            var sections = new List<AiJobStructuredResultSection>();
            var normalised = rawResponse.Replace("\r\n", "\n");
            var lines = normalised.Split('\n');
            string? currentHeading = null;
            string currentKey = string.Empty;
            var buffer = new StringBuilder();

            void CommitSection()
            {
                if (currentHeading is null)
                {
                    return;
                }

                var content = buffer.ToString().Trim();
                sections.Add(new AiJobStructuredResultSection
                {
                    Heading = currentHeading,
                    Key = currentKey,
                    Content = content
                });
                buffer.Clear();
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                var match = HeadingRegex.Match(line);
                if (match.Success)
                {
                    CommitSection();
                    currentHeading = match.Groups[1].Value.Trim();
                    currentKey = NormalizeHeading(jobType, currentHeading);
                    continue;
                }

                buffer.AppendLine(line);
            }

            CommitSection();

            if (sections.Count == 0)
            {
                sections.Add(new AiJobStructuredResultSection
                {
                    Heading = "Full Response",
                    Key = "full_response",
                    Content = normalised.Trim()
                });
            }

            result.Sections = sections;

            var missingKeys = ExpectedSectionKeys(jobType)
                .Except(sections.Select(s => s.Key))
                .ToList();

            if (missingKeys.Count > 0)
            {
                result.Metadata["missingSections"] = string.Join(",", missingKeys);
            }

            return result;
        }

        private static IEnumerable<string> ExpectedSectionKeys(string jobType)
        {
            var headings = jobType == AiJobTypes.Inference
                ? AiPromptBuilder.InferenceSectionHeadings
                : AiPromptBuilder.AnalysisSectionHeadings;

            return headings.Select(h => NormalizeHeading(jobType, h));
        }

        private static string NormalizeHeading(string jobType, string heading)
        {
            if (string.IsNullOrWhiteSpace(heading))
            {
                return jobType == AiJobTypes.Inference ? "inference_section" : "analysis_section";
            }

            var cleaned = heading
                .Trim()
                .ToLowerInvariant()
                .Replace("&", "and")
                .Replace("/", " ")
                .Replace(":", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("[", "")
                .Replace("]", "")
                .Replace(".", "")
                .Replace(",", " ")
                .Replace("  ", " ");

            var tokens = cleaned
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
            {
                return jobType == AiJobTypes.Inference ? "inference_section" : "analysis_section";
            }

            return string.Join("_", tokens);
        }
    }
}
