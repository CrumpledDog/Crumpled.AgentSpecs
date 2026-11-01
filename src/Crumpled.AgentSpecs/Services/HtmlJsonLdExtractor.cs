using Crumpled.AgentSpecs.Models;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// Finds the JSON-LD blocks (script elements whose type is exactly application/ld+json) in an HTML response, for
    /// pages that did not answer the markdown request. Agents reading the raw HTML still see this structured data.
    /// </summary>
    /// <remarks>
    /// A small tokenizer rather than a pattern: it skips comments, reads quoted attribute values (which may contain
    /// <c>&gt;</c>), compares the type attribute exactly, and takes a script's content as raw text up to its closing tag.
    /// </remarks>
    public static class HtmlJsonLdExtractor
    {
        private const string JsonLdType = "application/ld+json";

        public static IReadOnlyList<JsonLdModel> Extract(string html)
        {
            var items = new List<JsonLdModel>();
            var position = 0;

            while (position < html.Length)
            {
                var open = html.IndexOf('<', position);
                if (open < 0)
                {
                    break;
                }

                if (string.CompareOrdinal(html, open, "<!--", 0, 4) == 0)
                {
                    var commentEnd = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
                    position = commentEnd < 0 ? html.Length : commentEnd + 3;
                    continue;
                }

                if (!IsScriptStart(html, open))
                {
                    position = open + 1;
                    continue;
                }

                var type = ReadTag(html, open + "<script".Length, out var contentStart);
                var close = html.IndexOf("</script", contentStart, StringComparison.OrdinalIgnoreCase);
                var contentEnd = close < 0 ? html.Length : close;

                if (string.Equals(type?.Trim(), JsonLdType, StringComparison.OrdinalIgnoreCase))
                {
                    var json = html[contentStart..contentEnd].Trim();
                    if (json.Length > 0)
                    {
                        items.AddRange(CloudflareMarkdownParser.SplitJsonValues(json));
                    }
                }

                // Whatever is inside a script is not markup, so carry on after its closing tag.
                var closeEnd = close < 0 ? -1 : html.IndexOf('>', close);
                position = closeEnd < 0 ? html.Length : closeEnd + 1;
            }

            return items;
        }

        private static bool IsScriptStart(string html, int open)
        {
            const string tag = "<script";
            if (string.Compare(html, open, tag, 0, tag.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            var next = open + tag.Length;
            return next < html.Length && (char.IsWhiteSpace(html[next]) || html[next] == '>' || html[next] == '/');
        }

        // Reads the attributes of a start tag and returns the value of its type attribute (null if it has none).
        // contentStart is the index just after the tag's closing '>'.
        private static string? ReadTag(string html, int index, out int contentStart)
        {
            string? type = null;
            while (index < html.Length && html[index] != '>')
            {
                if (char.IsWhiteSpace(html[index]) || html[index] == '/')
                {
                    index++;
                    continue;
                }

                var nameStart = index;
                while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] is not ('=' or '>' or '/'))
                {
                    index++;
                }

                var name = html[nameStart..index];
                while (index < html.Length && char.IsWhiteSpace(html[index]))
                {
                    index++;
                }

                string? value = null;
                if (index < html.Length && html[index] == '=')
                {
                    index++;
                    while (index < html.Length && char.IsWhiteSpace(html[index]))
                    {
                        index++;
                    }

                    if (index < html.Length && html[index] is '"' or (char)39)
                    {
                        var quote = html[index];
                        var end = html.IndexOf(quote, index + 1);
                        end = end < 0 ? html.Length : end;
                        value = html[(index + 1)..end];
                        index = Math.Min(end + 1, html.Length);
                    }
                    else
                    {
                        var valueStart = index;
                        while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] != '>')
                        {
                            index++;
                        }

                        value = html[valueStart..index];
                    }
                }

                if (type is null && name.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    type = value ?? string.Empty;
                }
            }

            contentStart = Math.Min(index + 1, html.Length);
            return type;
        }
    }
}
