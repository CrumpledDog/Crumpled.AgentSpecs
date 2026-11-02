using System.Text;
using HtmlAgilityPack;
using Microsoft.Net.Http.Headers;
using ReverseMarkdown;

namespace Crumpled.AgentSpecs.DemoSite.MarkdownForAgents;

/// <summary>
/// TEST-ONLY stand-in for Cloudflare "Markdown for Agents", so the DemoSite can answer
/// <c>Accept: text/markdown</c> the way a Cloudflare-fronted site does: optional YAML front matter
/// (title, description, image), the page body as markdown, then any JSON-LD as one trailing fenced
/// json block. Not shipped in the package.
/// </summary>
public class MarkdownForAgentsMiddleware(RequestDelegate next)
{
    private static readonly Converter Converter = new(new Config
    {
        Tags = { Unknown = Config.UnknownTagsOption.Bypass },
        GithubFlavored = true,
        Formatting = { RemoveComments = true },
        Links = { SmartHref = true },
    });

    private static readonly string[] StrippedElements =
        ["//script", "//style", "//noscript", "//template", "//head", "//nav", "//header", "//footer"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!WantsMarkdown(context.Request))
        {
            await next(context);
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        var isHtml = context.Response.StatusCode == StatusCodes.Status200OK
            && context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
        if (!isHtml)
        {
            await buffer.CopyToAsync(original);
            return;
        }

        var markdown = ToMarkdown(Encoding.UTF8.GetString(buffer.ToArray()));
        context.Response.ContentType = "text/markdown; charset=utf-8";
        context.Response.ContentLength = null; // like Cloudflare, the converted body is streamed
        context.Response.Headers.Append(HeaderNames.Vary, "Accept");
        await original.WriteAsync(Encoding.UTF8.GetBytes(markdown));
    }

    private static bool WantsMarkdown(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method)
            || request.Path.StartsWithSegments("/umbraco")
            || request.Path.StartsWithSegments("/App_Plugins"))
        {
            return false;
        }

        return request.GetTypedHeaders().Accept
            .Any(accept => accept.MediaType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase));
    }

    public static string ToMarkdown(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        string? Meta(string name) => doc.DocumentNode
            .SelectSingleNode($"//meta[@name='{name}' or @property='{name}']")
            ?.GetAttributeValue("content", string.Empty) is { Length: > 0 } content ? content : null;

        var title = Meta("title") ?? Meta("og:title");
        var description = Meta("description") ?? Meta("og:description");
        var image = Meta("og:image");

        var jsonLd = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']")
            ?.Select(node => node.InnerText.Trim())
            .Where(text => text.Length > 0)
            .ToList() ?? [];

        foreach (var xpath in StrippedElements)
        {
            foreach (var node in doc.DocumentNode.SelectNodes(xpath)?.ToList() ?? [])
            {
                node.Remove();
            }
        }

        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var result = new StringBuilder();

        var frontMatter = new (string Key, string? Value)[] { ("description", description), ("image", image), ("title", title) }
            .Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .ToList();
        if (frontMatter.Count > 0)
        {
            result.Append("---\n");
            foreach (var (key, value) in frontMatter)
            {
                result.Append(key).Append(": ").Append(value!.ReplaceLineEndings(" ").Trim()).Append('\n');
            }

            result.Append("---\n\n");
        }

        result.Append(Converter.Convert(body.InnerHtml).Trim());

        if (jsonLd.Count > 0)
        {
            result.Append("\n\n```json\n").Append(string.Join("\n", jsonLd)).Append("\n```");
        }

        result.Append('\n');
        return result.ToString();
    }
}
