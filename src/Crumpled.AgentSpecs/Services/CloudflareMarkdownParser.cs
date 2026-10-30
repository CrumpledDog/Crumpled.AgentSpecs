using System.Text;
using System.Text.Json;
using Crumpled.AgentSpecs.Models;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// Splits a Cloudflare "Markdown for Agents" response into YAML frontmatter, markdown body and the
    /// trailing fenced json block holding any JSON-LD.
    /// </summary>
    public static class CloudflareMarkdownParser
    {
        private const string FenceOpen = "```json\n";

        public record Result(FrontmatterModel Frontmatter, string Markdown, IReadOnlyList<JsonLdModel> JsonLd);

        public static Result Parse(string raw)
        {
            var text = raw.TrimStart('﻿').Replace("\r\n", "\n");

            var frontmatter = ExtractFrontmatter(ref text);
            var jsonLd = ExtractJsonLd(ref text);

            return new Result(frontmatter, text.Trim(), jsonLd);
        }

        private static FrontmatterModel ExtractFrontmatter(ref string text)
        {
            var fields = new Dictionary<string, string>();
            if (!text.StartsWith("---\n", StringComparison.Ordinal))
            {
                return new FrontmatterModel { Fields = fields };
            }

            // The closing delimiter is a line consisting solely of "---".
            var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
            while (end >= 0 && end + 4 < text.Length && text[end + 4] != '\n')
            {
                end = text.IndexOf("\n---", end + 1, StringComparison.Ordinal);
            }

            if (end < 0)
            {
                return new FrontmatterModel { Fields = fields };
            }

            // An empty block ("---\n---") closes immediately, leaving nothing between the delimiters.
            var block = end > 4 ? text[4..end] : string.Empty;
            foreach (var line in block.Split('\n'))
            {
                var colon = line.IndexOf(':');
                if (colon <= 0 || char.IsWhiteSpace(line[0]))
                {
                    continue;
                }

                var value = line[(colon + 1)..].Trim();
                if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                {
                    value = value[1..^1];
                }

                fields[line[..colon].Trim()] = value;
            }

            text = text[Math.Min(end + 4, text.Length)..];
            return new FrontmatterModel { Raw = block, Fields = fields };
        }

        private static IReadOnlyList<JsonLdModel> ExtractJsonLd(ref string text)
        {
            var open = text.LastIndexOf(FenceOpen, StringComparison.Ordinal);
            if (open < 0 || (open > 0 && text[open - 1] != '\n'))
            {
                return [];
            }

            var contentStart = open + FenceOpen.Length;
            var close = text.IndexOf("\n```", contentStart - 1, StringComparison.Ordinal);
            if (close < 0 || !string.IsNullOrWhiteSpace(text[(close + 4)..]))
            {
                // Not a trailing block, so it's part of the page's own markdown.
                return [];
            }

            var json = text[contentStart..Math.Max(contentStart, close)];
            text = text[..open];
            return SplitJsonValues(json);
        }

        private static IReadOnlyList<JsonLdModel> SplitJsonValues(string json)
        {
            var items = new List<JsonLdModel>();
            var reader = new Utf8JsonReader(
                Encoding.UTF8.GetBytes(json),
                new JsonReaderOptions { AllowMultipleValues = true });

            try
            {
                while (reader.Read())
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    items.Add(new JsonLdModel { Raw = doc.RootElement.GetRawText() });
                }
            }
            catch (JsonException ex)
            {
                items.Clear();
                items.Add(new JsonLdModel { Raw = json, Error = ex.Message });
            }

            return items;
        }
    }
}
