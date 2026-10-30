# Crumpled.AgentSpecs

See your Umbraco pages the way AI agents see them. Crumpled.AgentSpecs adds an **Agent Specs** view to every
content item in the backoffice. Pick one of the item's front-end URLs, click **Fetch as agent**, and it requests
the live page with `Accept: text/markdown` and shows you the markdown and JSON-LD that
[Cloudflare Markdown for Agents](https://developers.cloudflare.com/fundamentals/reference/markdown-for-agents/)
serves to agents.

<img src="https://raw.githubusercontent.com/CrumpledDog/Crumpled.AgentSpecs/develop/v1/icons/icon.png" width="120" height="120" alt="">

## Installation

```bash
dotnet add package Crumpled.AgentSpecs
```

No configuration is required. Optional settings:

```json
{
  "Crumpled": {
    "AgentSpecs": {
      "UserAgent": "MyCompany-AgentSpecs/1.0",
      "TimeoutSeconds": 30,
      "MaxBodyBytes": 8388608
    }
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `UserAgent` | `Crumpled.AgentSpecs/1.0` | The `User-Agent` sent with every request. Set this if your WAF or bot rules need to recognise (or allow-list) the request. |
| `TimeoutSeconds` | `30` | How long to wait for the page. |
| `MaxBodyBytes` | `8388608` | The response is cut off (and flagged as truncated) beyond this size. |

## How it works

1. The **Agent Specs** view lists the content item's URLs, the same ones shown in the Links panel, defaulting to
   the culture you are editing.
2. Nothing is requested until you click **Fetch as agent**.
3. The backoffice asks a small authenticated Management API endpoint to make the request, so it works whatever
   domain the site is served from (no CORS problems). The endpoint only accepts URLs that belong to the content
   item, and only follows redirects to the same host.
4. The response is split into its parts and shown in three tabs:
   - **Preview** - the front matter and the rendered markdown.
   - **Markdown** - the raw response in a read-only code editor, with a copy button.
   - **JSON-LD** - each JSON-LD block in a collapsible JSON viewer.

If the site does not answer with `text/markdown`, you'll see the raw response and a warning that Markdown for
Agents doesn't appear to be enabled for that URL. Only published pages can be requested.

## Requirements

- The site must answer `Accept: text/markdown` requests with markdown. The easiest way is
  **Cloudflare Markdown for Agents**, enabled for the site's zone (or the relevant paths).

### Sites without Cloudflare

Agent Specs only needs a `text/markdown` response, so it works with any site that does content negotiation, not
just Cloudflare. Two options:

- Install [AI Visibility for Umbraco](https://marketplace.umbraco.com/package/umbraco.community.aivisibility),
  which serves markdown for `Accept: text/markdown` requests (and `.md` URLs).
- Or add a small middleware of your own. This sketch uses [ReverseMarkdown](https://www.nuget.org/packages/ReverseMarkdown)
  to convert the HTML response when the client asks for markdown:

```csharp
// Program.cs - before app.UseUmbraco()
app.Use(async (context, next) =>
{
    var wantsMarkdown = HttpMethods.IsGet(context.Request.Method)
        && !context.Request.Path.StartsWithSegments("/umbraco")
        && context.Request.GetTypedHeaders().Accept.Any(a => a.MediaType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase));
    if (!wantsMarkdown) { await next(); return; }

    var original = context.Response.Body;
    await using var buffer = new MemoryStream();
    context.Response.Body = buffer;
    try { await next(); } finally { context.Response.Body = original; }

    buffer.Position = 0;
    if (context.Response.StatusCode != 200 || context.Response.ContentType?.StartsWith("text/html") != true)
    {
        await buffer.CopyToAsync(original);
        return;
    }

    var markdown = new ReverseMarkdown.Converter().Convert(Encoding.UTF8.GetString(buffer.ToArray()));
    context.Response.ContentType = "text/markdown; charset=utf-8";
    context.Response.ContentLength = null;
    context.Response.Headers.Append("Vary", "Accept");
    await original.WriteAsync(Encoding.UTF8.GetBytes(markdown));
});
```

The DemoSite in this repo has a fuller version, [`MarkdownForAgentsMiddleware`](src/Crumpled.AgentSpecs.DemoSite/MarkdownForAgents/MarkdownForAgentsMiddleware.cs),
which also strips headers, navigation and footers, and adds front matter and a trailing JSON-LD block the way
Cloudflare does. It exists for testing only and is not part of the package.

The **JSON-LD** tab appears when the response ends with a fenced `json` block, which is the format Cloudflare
uses. If your middleware doesn't append one, you'll still get the Preview and Markdown tabs.

Each Umbraco major has its own package version line, so pick the one that matches your site:

| Package version | Umbraco CMS | Branch |
|---|---|---|
| `1.x` | 17 | `release/v1` |

```bash
# Umbraco 17
dotnet add package Crumpled.AgentSpecs --version "1.*"
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the branching strategy and formatting requirements.

## License

[MIT](LICENSE)
