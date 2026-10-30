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
| `UserAgent` | `Crumpled.AgentSpecs/<version> (+https://github.com/CrumpledDog/Crumpled.AgentSpecs)` | The `User-Agent` sent with every request. Set this if your WAF or bot rules need to recognise (or allow-list) the request. |
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

- **Cloudflare Markdown for Agents** must be enabled for the site's zone (or the relevant paths).
- If your site compresses responses, the companion package
  [Crumpled.Cloudflare.NoCompressForAgents](https://github.com/CrumpledDog/Crumpled.Cloudflare.NoCompressForAgents)
  keeps markdown responses uncompressed.

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
