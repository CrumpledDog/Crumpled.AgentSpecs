import { css, customElement, html, nothing, property } from "@umbraco-cms/backoffice/external/lit";
import type { TemplateResult } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { AgentSpecsResponseModel } from "../api/index.js";
import { GUIDE_LAST_REVIEWED, GUIDE_LINKS } from "./agent-specs-guide.js";

type GuideSection = "markdown" | "signals" | "jsonld";

// The explanatory column beside the Agent Specs results. It explains the result on screen, in plain language,
// and highlights the section that matches the tab the editor is looking at.
@customElement("crumpled-agent-specs-guide")
export class CrumpledAgentSpecsGuideElement extends UmbLitElement {
  @property({ attribute: false }) result?: AgentSpecsResponseModel;
  @property() tab: "preview" | "markdown" | "jsonld" = "preview";

  #link(href: string, label: string) {
    return html`<uui-button
      compact
      look="secondary"
      label=${label}
      href=${href}
      target="_blank"
      rel="noopener noreferrer"
    >
      <uui-icon name="icon-out"></uui-icon> ${label}
    </uui-button>`;
  }

  #box(id: GuideSection, headline: string, summary: TemplateResult, more: TemplateResult, links: TemplateResult) {
    const active =
      this.result !== undefined &&
      ((id === "jsonld" && this.tab === "jsonld") || (id === "markdown" && this.tab !== "jsonld"));
    // Open the explanation for a problem the result shows, so it isn't hidden behind "Read more". Markdown and
    // structured data are independent of each other, so each opens on its own. Content signals are optional and
    // stay closed. The binding only changes with the result, so an editor opening or closing a section themselves
    // is left alone.
    const missing =
      this.result !== undefined &&
      ((id === "markdown" && !this.result.isMarkdown) || (id === "jsonld" && this.result.jsonLd.length === 0));
    return html`<uui-box headline=${headline} class=${active ? "active" : ""}>
      <p class="lead">${summary}</p>
      <details ?open=${missing}>
        <summary>Read more</summary>
        ${more}
      </details>
      <div class="links">${links}</div>
    </uui-box>`;
  }

  override render() {
    const result = this.result;
    return html`
      ${!result
        ? html`<uui-box headline="What is this?">
            <p class="lead">
              AI assistants, search bots and agents don't see your page the way a visitor does. They read its
              underlying content. This view shows you exactly what they receive.
            </p>
            <p>Press <strong>Fetch as agent</strong>. The results, and an explanation of each, appear here.</p>
          </uui-box>`
        : nothing}
      ${this.#box(
        "markdown",
        "Markdown for Agents",
        html`Agents work best with a clean, text-only version of a page: no menus, scripts or layout, just the
        content.`,
        html`<p>
            A page can answer an agent with <strong>markdown</strong> instead of HTML. It is smaller, cheaper for
            the agent to read, and less likely to be misread. This view asks for it the way an agent would.
          </p>
          ${result && !result.isMarkdown
            ? html`<p>
                <strong>This page answered with HTML.</strong> Agents can still read it, but with more noise. See
                <em>What can I do?</em> in the results for the quickest routes. In every case the site, or the CDN
                or hosting in front of it, has to answer <code>Accept: text/markdown</code> requests with markdown.
              </p>`
            : nothing}
          <p>
            <strong>What about llms.txt?</strong> You may be advised to add one. Google has said its AI systems
            don't use it, and there's no public evidence that major AI crawlers request it. Agents read the page
            itself, which is exactly what this view shows you. Put the effort into what they do read: content in
            the page's HTML, a markdown version, accurate structured data and clear content signals.
            <span class="muted">Last reviewed ${GUIDE_LAST_REVIEWED}.</span>
          </p>`,
        html`${this.#link(GUIDE_LINKS.markdownForAgents, "Cloudflare: Markdown for Agents")}
        ${this.#link(GUIDE_LINKS.googleAiOptimization, "Google: AI optimization myths")}`,
      )}
      ${this.#box(
        "signals",
        "Content signals",
        html`The <code>Content-Signal</code> header states what the site owner is happy for AI to do with this
        page.`,
        html`<ul>
            <li><strong>search</strong>: may appear in search results.</li>
            <li><strong>ai-input</strong>: may be used live to answer a question (for example by an AI assistant).</li>
            <li><strong>ai-train</strong>: may be used to train AI models.</li>
          </ul>
          <p>
            <em>yes</em> means allowed and <em>no</em> means not allowed. These are published preferences, not
            enforcement. Well-behaved crawlers respect them. The header is optional. Whoever looks after the site's
            technical setup can set it in the server or CDN configuration and mirror it in robots.txt.
          </p>
          <p>
            <strong>Cloudflare fills in a default.</strong> With Markdown for Agents switched on, if the site doesn't
            send its own header, Cloudflare adds <code>ai-train=yes, search=yes, ai-input=yes</code>. So a signal
            you see here may be that default rather than a deliberate choice. Set your own to be explicit.
          </p>
          ${result && !result.headers["content-signal"]
            ? html`<p>
                <strong>No content signal was found for this page.</strong> That's not an error. It simply means no
                preference is declared.
              </p>`
            : nothing}`,
        this.#link(GUIDE_LINKS.contentSignals, "Content Signals"),
      )}
      ${this.#box(
        "jsonld",
        "Structured data (JSON-LD)",
        html`Structured data labels what the page is about in a form machines can read, such as an article, a
        product or an organisation.`,
        html`<p>
            It is invisible to visitors but is read by search engines and agents. Accurate schema.org types help
            machines understand who published the page, what it covers and how it relates to the rest of the site.
            That supports richer search results and more accurate summaries. It helps machines understand the page;
            it doesn't guarantee a place in any AI answer.
          </p>
          <p>
            <strong>This is separate from markdown.</strong> Structured data lives in the page's HTML, so it can be
            added whether or not the site can serve markdown to agents, and switching on Markdown for Agents doesn't
            add it.
          </p>
          <p>Use <strong>Validate at schema.org</strong> on the JSON-LD tab to check it for errors.</p>
          <p>
            <strong>Going further:</strong> sites that want agents to do things, such as search stock, book or
            request a quote, can expose those actions through an <strong>MCP server</strong>. That's a next step for
            sites with transactional features, not something every content page needs.
          </p>`,
        html`${this.#link(GUIDE_LINKS.schemaOrg, "schema.org: Getting started")}
        ${this.#link(GUIDE_LINKS.googleStructuredData, "Google: Structured data")}
        ${this.#link(GUIDE_LINKS.mcp, "Model Context Protocol")}`,
      )}
    `;
  }

  static override styles = [
    css`
      :host {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-layout-1);
      }

      uui-box {
        margin: 0;
      }

      uui-box.active {
        outline: 2px solid var(--uui-color-selected);
        outline-offset: -2px;
      }

      p {
        margin-top: 0;
      }

      .lead {
        font-weight: bold;
      }

      .muted {
        color: var(--uui-color-text-alt);
      }

      details {
        margin-bottom: var(--uui-size-space-4);
      }

      summary {
        cursor: pointer;
        color: var(--uui-color-interactive);
        margin-bottom: var(--uui-size-space-3);
      }

      ul {
        padding-left: var(--uui-size-space-5);
      }

      .links {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2);
      }

      code {
        font-family: monospace;
        background: var(--uui-color-surface-alt);
        border-radius: var(--uui-border-radius);
      }
    `,
  ];
}

export default CrumpledAgentSpecsGuideElement;

declare global {
  interface HTMLElementTagNameMap {
    "crumpled-agent-specs-guide": CrumpledAgentSpecsGuideElement;
  }
}
