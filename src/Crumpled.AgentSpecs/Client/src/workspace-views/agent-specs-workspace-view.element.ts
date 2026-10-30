import "@alenaksu/json-viewer";
import "@umbraco-cms/backoffice/code-editor";
import {
  css,
  customElement,
  html,
  nothing,
  repeat,
  state,
  unsafeHTML,
} from "@umbraco-cms/backoffice/external/lit";
import { DOMPurify } from "@umbraco-cms/backoffice/external/dompurify";
import { marked } from "@umbraco-cms/backoffice/external/marked";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/document";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadStructureForEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import { UMB_VARIANT_CONTEXT } from "@umbraco-cms/backoffice/variant";
import { getMarkdown, getUrls } from "../api/index.js";
import type { AgentSpecsResponseModel } from "../api/index.js";

type DocumentLink = { culture: string | null; url: string };
type ViewTab = "preview" | "markdown" | "jsonld";

@customElement("crumpled-agent-specs-workspace-view")
export class CrumpledAgentSpecsWorkspaceViewElement extends UmbLitElement {
  @state() private _links: Array<DocumentLink> = [];
  @state() private _selectedUrl?: string;
  @state() private _isNew = true;
  @state() private _loading = false;
  @state() private _result?: AgentSpecsResponseModel;
  @state() private _error?: string;
  @state() private _tab: ViewTab = "preview";

  #unique?: string;
  #entityType?: string;
  #culture?: string | null;
  #reloadTimer?: ReturnType<typeof setTimeout>;
  #fetchId = 0;
  #userPickedUrl = false;
  #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;

  // Publishing, unpublishing or renaming a document changes its URLs, so refresh the links when it is saved.
  #onReloadRequest = (event: Event) => {
    const reload = event as UmbRequestReloadStructureForEntityEvent;
    if (reload.getUnique() !== this.#unique || reload.getEntityType() !== this.#entityType) return;
    clearTimeout(this.#reloadTimer);
    this.#reloadTimer = setTimeout(() => this.#loadLinks(), 300);
  };

  constructor() {
    super();

    this.consumeContext(UMB_VARIANT_CONTEXT, (context) => {
      this.observe(
        context?.displayVariantId,
        (variantId) => {
          this.#culture = variantId?.culture;
          this.#selectDefaultLink();
        },
        "observeDisplayVariantId",
      );
    });

    this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (context) => {
      this.#eventContext?.removeEventListener(UmbRequestReloadStructureForEntityEvent.TYPE, this.#onReloadRequest);
      this.#eventContext = context;
      this.#eventContext?.addEventListener(UmbRequestReloadStructureForEntityEvent.TYPE, this.#onReloadRequest);
    });

    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (context) => {
      this.#entityType = context?.getEntityType();
      this.observe(
        context?.isNew,
        (isNew) => {
          this._isNew = isNew ?? true;
        },
        "observeIsNew",
      );
      this.observe(
        context?.unique,
        (unique) => {
          if (unique === this.#unique) return;
          this.#unique = unique ?? undefined;
          this.#fetchId++; // any fetch still in flight belongs to the previous document
          this.#userPickedUrl = false;
          this._loading = false;
          this._result = undefined;
          this._error = undefined;
          this._links = [];
          this._selectedUrl = undefined;
          this.#loadLinks();
        },
        "observeUnique",
      );
    });
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    clearTimeout(this.#reloadTimer);
    this.#eventContext?.removeEventListener(UmbRequestReloadStructureForEntityEvent.TYPE, this.#onReloadRequest);
  }

  async #loadLinks() {
    const unique = this.#unique;
    if (!unique) return;

    // The server lists the same URLs as the Links panel, after any URL processing a site has registered, so a
    // site that narrows its Links panel (for example to one hostname) gets the same list here.
    const { data } = await getUrls({ query: { documentId: unique } });
    if (unique !== this.#unique) return;
    this._links = (data?.urls ?? []).map((link) => ({ culture: link.culture ?? null, url: link.url }));
    this.#selectDefaultLink();
  }

  #selectDefaultLink() {
    if (!this._links.length) return;
    const forCulture = this._links.find((link) => link.culture === this.#culture);
    // Keep a pick the editor made themselves while it is still one of the document's links. An automatic
    // default is not a choice, so it follows the culture being edited.
    const keepPick = this.#userPickedUrl && this._links.some((link) => link.url === this._selectedUrl);
    if (!keepPick) {
      this.#userPickedUrl = false;
      this._selectedUrl = (forCulture ?? this._links[0]).url;
    }
  }

  async #fetch() {
    if (!this.#unique || !this._selectedUrl) return;

    const fetchId = ++this.#fetchId;
    this._loading = true;
    this._error = undefined;
    this._result = undefined;

    const { data, error, response } = await getMarkdown({
      query: { documentId: this.#unique, url: this._selectedUrl },
    });

    // The editor moved to another document (or fetched again) while this was in flight.
    if (fetchId !== this.#fetchId) return;

    if (data) {
      this._result = data;
      this._tab = data.isMarkdown ? "preview" : "markdown";
    } else {
      const problem = error as { title?: string; detail?: string } | undefined;
      this._error = [problem?.title, problem?.detail].filter(Boolean).join(": ") || "The request failed.";
      // A refused URL usually means the document's links changed since they were loaded.
      if (response?.status === 400) this.#loadLinks();
    }

    this._loading = false;
  }

  #renderMarkdown(markdown: string, pageUrl: string) {
    const clean = DOMPurify.sanitize(marked.parse(markdown, { async: false }) as string);
    const template = document.createElement("template");
    template.innerHTML = clean;
    // Relative links and images belong to the fetched page, not the backoffice, so resolve them against it.
    template.content.querySelectorAll("[href], [src]").forEach((element) => {
      for (const attribute of ["href", "src"]) {
        const value = element.getAttribute(attribute);
        if (value === null) continue;
        try {
          element.setAttribute(attribute, new URL(value, pageUrl).href);
        } catch {
          element.removeAttribute(attribute);
        }
      }
    });
    template.content.querySelectorAll("a[href]").forEach((anchor) => {
      anchor.setAttribute("target", "_blank");
      anchor.setAttribute("rel", "noopener noreferrer");
    });
    return unsafeHTML(template.innerHTML);
  }

  #parseJson(raw: string): unknown {
    try {
      return JSON.parse(raw);
    } catch {
      return undefined;
    }
  }

  #copy(text: string) {
    navigator.clipboard?.writeText(text);
  }

  override render() {
    return html`
      <umb-body-layout header-fit-height>
        ${this.#renderRequestBox()} ${this.#renderResult()}
      </umb-body-layout>
    `;
  }

  #renderRequestBox() {
    if (this._isNew || !this._links.length) {
      return html`<uui-box headline="Agent Specs">
        <p class="muted">
          ${this._isNew
            ? "Save and publish this item to see what an AI agent receives for it."
            : "This item has no front-end URL. Publish it (and check its hostnames) so it appears in the Links panel."}
        </p>
      </uui-box>`;
    }

    return html`
      <uui-box headline="Agent Specs">
        <p class="muted">
          Requests the published page with <code>Accept: text/markdown</code>, the way an agent would
          (needs Cloudflare Markdown for Agents on the site).
        </p>
        <div class="request">
          ${this._links.length > 1
            ? html`<uui-select
                label="URL"
                .options=${this._links.map((link) => ({
                  name: link.culture ? `${link.url}  (${link.culture})` : link.url,
                  value: link.url,
                  selected: link.url === this._selectedUrl,
                }))}
                @change=${(e: Event) => {
                  this.#userPickedUrl = true;
                  this._selectedUrl = (e.target as HTMLInputElement).value;
                }}
              ></uui-select>`
            : html`<span class="single-url">${this._selectedUrl}</span>`}
          <uui-button
            look="primary"
            color="positive"
            label="Fetch as agent"
            .state=${this._loading ? "waiting" : undefined}
            @click=${this.#fetch}
          ></uui-button>
        </div>
        ${this._error
          ? html`<uui-tag color="danger" class="error"><uui-icon name="icon-alert"></uui-icon> ${this._error}</uui-tag>`
          : nothing}
      </uui-box>
    `;
  }

  #renderResult() {
    const result = this._result;
    if (!result) return nothing;

    return html`
      <uui-box>
        <div class="summary">
          <uui-tag look="primary" color=${result.statusCode < 400 ? "positive" : "danger"}>${result.statusCode}</uui-tag>
          <uui-tag look="secondary">${result.contentType ?? "no content type"}</uui-tag>
          ${result.headers["content-signal"]
            ? html`<uui-tag look="outline">Content-Signal: ${result.headers["content-signal"]}</uui-tag>`
            : nothing}
          ${result.headers["cf-cache-status"]
            ? html`<uui-tag look="outline">CF: ${result.headers["cf-cache-status"]}</uui-tag>`
            : nothing}
          <uui-tag look="outline">${(result.raw.length / 1024).toFixed(1)} KB</uui-tag>
          <uui-tag look="outline">${result.elapsedMs} ms</uui-tag>
          ${result.truncated ? html`<uui-tag color="warning">truncated</uui-tag>` : nothing}
        </div>
        <p class="muted small">
          ${result.finalUrl} &middot; User-Agent: <code>${result.userAgent}</code>
        </p>
        ${!result.isMarkdown
          ? html`<uui-tag color="warning" class="error">
              <uui-icon name="icon-alert"></uui-icon>
              Markdown for Agents doesn't appear to be enabled for this URL (got
              ${result.contentType ?? "an unknown content type"}).
            </uui-tag>`
          : nothing}
      </uui-box>

      <uui-box class="tabs-box">
        <uui-tab-group slot="header">
          ${result.isMarkdown
            ? html`<uui-tab label="Preview" ?active=${this._tab === "preview"} @click=${() => (this._tab = "preview")}>Preview</uui-tab>`
            : nothing}
          <uui-tab label="Markdown" ?active=${this._tab === "markdown"} @click=${() => (this._tab = "markdown")}>
            ${result.isMarkdown ? "Markdown" : "Raw response"}
          </uui-tab>
          ${result.jsonLd.length
            ? html`<uui-tab label="JSON-LD" ?active=${this._tab === "jsonld"} @click=${() => (this._tab = "jsonld")}>
                JSON-LD${result.jsonLd.length > 1 ? ` (${result.jsonLd.length})` : ""}
              </uui-tab>`
            : nothing}
        </uui-tab-group>
        ${this._tab === "preview" ? this.#renderPreview(result) : nothing}
        ${this._tab === "markdown" ? this.#renderSource(result) : nothing}
        ${this._tab === "jsonld" ? this.#renderJsonLd(result) : nothing}
      </uui-box>
    `;
  }

  #renderPreview(result: AgentSpecsResponseModel) {
    const fields = Object.entries(result.frontmatter.fields);
    return html`
      ${fields.length
        ? html`<dl class="frontmatter">
            ${fields.map(([key, value]) => html`<dt>${key}</dt><dd>${value}</dd>`)}
          </dl>`
        : nothing}
      <div class="markdown">${this.#renderMarkdown(result.markdown, result.finalUrl)}</div>
    `;
  }

  #renderSource(result: AgentSpecsResponseModel) {
    return html`
      <div class="toolbar">
        <uui-button compact look="secondary" label="Copy" @click=${() => this.#copy(result.raw)}>
          <uui-icon name="icon-documents"></uui-icon> Copy
        </uui-button>
      </div>
      <umb-code-editor
        language=${result.isMarkdown ? "markdown" : "html"}
        .code=${result.raw}
        readonly
      ></umb-code-editor>
    `;
  }

  // The Schema Markup Validator fetches the page itself, so it can only check pages it can reach: publicly
  // addressable ones. The editor-facing URL is used, never the one requested (which may carry an access token).
  #schemaValidatorUrl(pageUrl: string): string | undefined {
    try {
      const { protocol, hostname } = new URL(pageUrl);
      const isPublic =
        (protocol === "http:" || protocol === "https:") &&
        hostname.includes(".") &&
        !hostname.endsWith(".localhost") &&
        !hostname.endsWith(".local") &&
        !hostname.endsWith(".internal") &&
        !/^[\d.]+$/.test(hostname) &&
        !hostname.includes(":");
      return isPublic ? `https://validator.schema.org/#url=${encodeURIComponent(pageUrl)}` : undefined;
    } catch {
      return undefined;
    }
  }

  #renderJsonLd(result: AgentSpecsResponseModel) {
    const validatorUrl = this.#schemaValidatorUrl(result.requestedUrl);
    return html`${validatorUrl
        ? html`<div class="toolbar">
            <uui-button
              compact
              look="secondary"
              label="Validate at schema.org"
              href=${validatorUrl}
              target="_blank"
              rel="noopener noreferrer"
            >
              <uui-icon name="icon-out"></uui-icon> Validate at schema.org
            </uui-button>
          </div>`
        : nothing}
      ${repeat(
      result.jsonLd,
      (_, index) => index,
      (item, index) => {
        const parsed = this.#parseJson(item.raw);
        return html`
          ${result.jsonLd.length > 1 ? html`<div class="item-label">[${index}]</div>` : nothing}
          ${item.error
            ? html`<uui-tag color="warning"><uui-icon name="icon-alert"></uui-icon> ${item.error}</uui-tag>`
            : nothing}
          ${parsed !== undefined
            ? html`<json-viewer class="decoded" .data=${parsed}></json-viewer>`
            : html`<pre class="raw">${item.raw}</pre>`}
        `;
      },
    )}`;
  }

  static override styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      uui-box {
        margin-bottom: var(--uui-size-layout-1);
      }

      .muted {
        color: var(--uui-color-text-alt);
        margin-top: 0;
      }

      .small {
        font-size: var(--uui-type-small-size);
        margin: var(--uui-size-space-3) 0 0;
        word-break: break-all;
      }

      .request {
        display: flex;
        gap: var(--uui-size-space-3);
        align-items: center;
      }

      .single-url {
        flex: 1;
        min-width: 0;
        word-break: break-all;
        font-family: monospace;
      }

      .request uui-select {
        flex: 1;
        min-width: 0;
      }

      .summary {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2);
      }

      .error {
        margin-top: var(--uui-size-space-3);
        white-space: normal;
        height: auto;
      }

      .toolbar {
        display: flex;
        justify-content: flex-end;
        margin-bottom: var(--uui-size-space-3);
      }

      umb-code-editor {
        height: 60vh;
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
      }

      .frontmatter {
        display: grid;
        grid-template-columns: max-content 1fr;
        gap: var(--uui-size-space-2) var(--uui-size-space-4);
        margin: 0 0 var(--uui-size-space-5);
        padding: var(--uui-size-space-4);
        background: var(--uui-color-surface-alt);
        border-radius: var(--uui-border-radius);
        font-size: var(--uui-type-small-size);
      }

      .frontmatter dt {
        font-weight: bold;
      }

      .frontmatter dd {
        margin: 0;
        word-break: break-word;
      }

      .markdown {
        line-height: 1.6;
        overflow-wrap: anywhere;
      }

      .markdown img {
        max-width: 100%;
      }

      .markdown pre,
      .markdown code {
        font-family: monospace;
        background: var(--uui-color-surface-alt);
        border-radius: var(--uui-border-radius);
      }

      .markdown pre {
        padding: var(--uui-size-space-4);
        overflow: auto;
      }

      .markdown table {
        border-collapse: collapse;
      }

      .markdown th,
      .markdown td {
        border: 1px solid var(--uui-color-border);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
      }

      .item-label {
        margin-top: var(--uui-size-space-4);
        font-weight: bold;
      }

      pre.raw {
        white-space: pre-wrap;
        word-break: break-all;
      }

      /* Same theming as the hosting kit's Redis key viewer: map json-viewer's colours to UUI tokens. */
      json-viewer.decoded {
        display: block;
        margin-top: var(--uui-size-space-3);
        padding: var(--uui-size-space-4);
        background: var(--uui-color-surface);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        max-height: 50vh;
        overflow: auto;
        font-size: var(--uui-type-small-size);
        --font-family: monospace;
        --background-color: transparent;
        --color: var(--uui-color-text);
        --property-color: var(--uui-color-selected-standalone);
        --string-color: var(--uui-color-positive-standalone);
        --number-color: var(--uui-color-warning-standalone);
        --boolean-color: var(--uui-color-danger-standalone);
        --null-color: var(--uui-color-text-alt);
        --preview-color: var(--uui-color-text-alt);
        --highlight-color: var(--uui-color-warning);
        --indentguide-color: var(--uui-color-border);
        --indentguide-color-active: var(--uui-color-border-emphasis);
      }
    `,
  ];
}

export default CrumpledAgentSpecsWorkspaceViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "crumpled-agent-specs-workspace-view": CrumpledAgentSpecsWorkspaceViewElement;
  }
}
