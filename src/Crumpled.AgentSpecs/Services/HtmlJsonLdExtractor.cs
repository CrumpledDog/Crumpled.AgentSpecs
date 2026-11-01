using System.Text.RegularExpressions;
using Crumpled.AgentSpecs.Models;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// Finds the JSON-LD blocks (script type="application/ld+json") in an HTML response, for pages that did not
    /// answer the markdown request. Agents reading the raw HTML still see this structured data.
    /// </summary>
    public static partial class HtmlJsonLdExtractor
    {
        [GeneratedRegex(
            @"<script\b[^>]*\btype\s*=\s*[""']?application/ld\+json[""']?[^>]*>(?<json>.*?)</script\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
            matchTimeoutMilliseconds: 2000)]
        private static partial Regex ScriptPattern();

        public static IReadOnlyList<JsonLdModel> Extract(string html)
        {
            var items = new List<JsonLdModel>();
            try
            {
                foreach (Match match in ScriptPattern().Matches(html))
                {
                    var json = match.Groups["json"].Value.Trim();
                    if (json.Length > 0)
                    {
                        items.AddRange(CloudflareMarkdownParser.SplitJsonValues(json));
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological page: report what was found so far rather than failing the whole request.
            }

            return items;
        }
    }
}
