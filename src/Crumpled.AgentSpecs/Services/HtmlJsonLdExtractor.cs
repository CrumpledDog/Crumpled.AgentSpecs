using System.Net;
using Crumpled.AgentSpecs.Models;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// Finds the JSON-LD blocks (script elements whose type is exactly application/ld+json) in an HTML response, for
    /// pages that did not answer the markdown request. Agents reading the raw HTML still see this structured data.
    /// </summary>
    /// <remarks>
    /// A small tokenizer rather than a pattern: it skips comments, reads quoted attribute values (which may contain
    /// <c>&gt;</c>), compares the (decoded) type attribute exactly, and reads script, style and similar content as raw text up
    /// to the matching closing tag.
    /// </remarks>
    public static class HtmlJsonLdExtractor
    {
        private const string JsonLdType = "application/ld+json";

        // Elements whose content is text, not markup: anything that looks like a tag inside them is not one.
        private static readonly string[] RawTextElements = ["script", "style", "textarea", "title", "xmp", "iframe", "noembed", "noframes"];

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

                var nameStart = open + 1;
                if (nameStart >= html.Length || !char.IsAsciiLetter(html[nameStart]))
                {
                    position = nameStart;
                    continue;
                }

                var nameEnd = nameStart;
                while (nameEnd < html.Length && !char.IsWhiteSpace(html[nameEnd]) && html[nameEnd] is not ('>' or '/'))
                {
                    nameEnd++;
                }

                // Consume the whole start tag, so quoted attribute values are never scanned as markup.
                var name = html[nameStart..nameEnd].ToLowerInvariant();
                var type = ReadTag(html, nameEnd, out var contentStart);

                if (!RawTextElements.Contains(name))
                {
                    position = contentStart;
                    continue;
                }

                var close = FindClosingTag(html, name, contentStart);
                if (name == "script" && string.Equals(WebUtility.HtmlDecode(type ?? string.Empty).Trim(), JsonLdType, StringComparison.OrdinalIgnoreCase))
                {
                    var json = html[contentStart..close].Trim();
                    if (json.Length > 0)
                    {
                        items.AddRange(CloudflareMarkdownParser.SplitJsonValues(json));
                    }
                }

                // The content is not markup, so carry on after the closing tag.
                var closeEnd = close >= html.Length ? -1 : html.IndexOf('>', close);
                position = closeEnd < 0 ? html.Length : closeEnd + 1;
            }

            return items;
        }

        // The index of the end tag for name (the whole name, not a longer one such as </scripture>), or the end of the html.
        private static int FindClosingTag(string html, string name, int from)
        {
            var search = from;
            while (search < html.Length)
            {
                var close = html.IndexOf("</" + name, search, StringComparison.OrdinalIgnoreCase);
                if (close < 0)
                {
                    return html.Length;
                }

                var after = close + 2 + name.Length;
                if (after >= html.Length || char.IsWhiteSpace(html[after]) || html[after] is '>' or '/')
                {
                    return close;
                }

                search = after;
            }

            return html.Length;
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
