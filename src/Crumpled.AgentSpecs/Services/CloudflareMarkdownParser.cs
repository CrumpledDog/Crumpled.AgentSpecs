using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crumpled.AgentSpecs.Models;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// Splits a Cloudflare "Markdown for Agents" response into YAML frontmatter, markdown body and the
    /// trailing fenced json block holding any JSON-LD.
    /// </summary>
    public static partial class CloudflareMarkdownParser
    {
        // What makes a json fence JSON-LD rather than an ordinary example: one of its reserved keywords.
        [GeneratedRegex("\"@(context|type|graph|id)\"", RegexOptions.CultureInvariant)]
        private static partial Regex JsonLdSignal();

        private const string FenceOpen = "```json\n";

        public record Result(FrontmatterModel Frontmatter, string Markdown, IReadOnlyList<JsonLdModel> JsonLd);

        public static Result Parse(string raw)
        {
            var text = raw.TrimStart('﻿').Replace("\r\n", "\n");

            var frontmatter = ExtractFrontmatter(ref text);
            var jsonLd = ExtractJsonLd(ref text);

            return new Result(frontmatter, TrimBlankLines(text), jsonLd);
        }

        // Drop the blank separator lines around the body, but keep the first line's own indentation: it can be an
        // indented code block, and Trim() would turn that into a paragraph.
        private static string TrimBlankLines(string text)
        {
            var start = 0;
            while (start < text.Length)
            {
                var lineEnd = text.IndexOf((char)10, start);
                if (lineEnd < 0 || !string.IsNullOrWhiteSpace(text[start..lineEnd]))
                {
                    break;
                }

                start = lineEnd + 1;
            }

            return text[start..].TrimEnd();
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

                fields[line[..colon].Trim()] = UnquoteScalar(line[(colon + 1)..].Trim());
            }

            text = text[Math.Min(end + 4, text.Length)..];
            return new FrontmatterModel { Raw = block, Fields = fields };
        }

        // A quoted YAML scalar is decoded, not just stripped of its quotes: a double-quoted one can hold escapes
        // (\" \\ \n \t \uXXXX), and a single-quoted one writes a literal quote as ''.
        private static string UnquoteScalar(string value)
        {
            if (value.Length < 2 || value[^1] != value[0])
            {
                return value;
            }

            if (value[0] == (char)39)
            {
                return value[1..^1].Replace("''", ((char)39).ToString());
            }

            if (value[0] != '"')
            {
                return value;
            }

            var inner = value[1..^1];
            var decoded = new StringBuilder(inner.Length);
            for (var i = 0; i < inner.Length; i++)
            {
                if (inner[i] != '\\' || i + 1 >= inner.Length)
                {
                    decoded.Append(inner[i]);
                    continue;
                }

                var next = inner[++i];
                switch (next)
                {
                    case 'n': decoded.Append('\n'); break;
                    case 't': decoded.Append('\t'); break;
                    case 'r': decoded.Append('\r'); break;
                    case '0': decoded.Append('\0'); break;
                    case 'u' when i + 4 < inner.Length && ushort.TryParse(inner.AsSpan(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out var code):
                        decoded.Append((char)code);
                        i += 4;
                        break;
                    default: decoded.Append(next); break;
                }
            }

            return decoded.ToString();
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
            if (!JsonLdSignal().IsMatch(json))
            {
                // A trailing json example in the page's own content, not structured data: leave it in the body.
                return [];
            }

            text = text[..open];
            return SplitJsonValues(json);
        }

        internal static IReadOnlyList<JsonLdModel> SplitJsonValues(string json)
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
