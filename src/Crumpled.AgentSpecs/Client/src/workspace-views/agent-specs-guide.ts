export type SignalState = "allowed" | "declined" | "unspecified";

export type ContentSignals = {
  search: SignalState;
  aiInput: SignalState;
  aiTrain: SignalState;
};

const toState = (value: string | undefined): SignalState =>
  value === "yes" ? "allowed" : value === "no" ? "declined" : "unspecified";

// Parses a header such as "ai-train=yes, search=yes, ai-input=no". Returns undefined when there is no header.
export function parseContentSignal(header: string | undefined | null): ContentSignals | undefined {
  if (!header) return undefined;
  const values = new Map<string, string>();
  for (const part of header.split(",")) {
    const [key, value] = part.split("=").map((piece) => piece.trim().toLowerCase());
    if (key && value) values.set(key, value);
  }
  return {
    search: toState(values.get("search")),
    aiInput: toState(values.get("ai-input")),
    aiTrain: toState(values.get("ai-train")),
  };
}

// Collects the schema.org @type values from JSON-LD blocks (including those nested in an @graph).
export function schemaTypes(rawBlocks: Array<string>): Array<string> {
  const types = new Set<string>();
  const visit = (node: unknown) => {
    if (Array.isArray(node)) return node.forEach(visit);
    if (!node || typeof node !== "object") return;
    const record = node as Record<string, unknown>;
    const type = record["@type"];
    if (typeof type === "string") types.add(type);
    else if (Array.isArray(type)) type.filter((t): t is string => typeof t === "string").forEach((t) => types.add(t));
    if (record["@graph"]) visit(record["@graph"]);
  };
  for (const raw of rawBlocks) {
    try {
      visit(JSON.parse(raw));
    } catch {
      // A block that doesn't parse is reported by the JSON-LD tab itself.
    }
  }
  return [...types];
}

export const GUIDE_LAST_REVIEWED = "October 2026";

export const GUIDE_LINKS = {
  markdownForAgents: "https://developers.cloudflare.com/fundamentals/reference/markdown-for-agents/",
  contentSignals: "https://contentsignals.org/",
  schemaOrg: "https://schema.org/docs/gs.html",
  googleStructuredData: "https://developers.google.com/search/docs/appearance/structured-data/intro-structured-data",
  mcp: "https://modelcontextprotocol.io/",
  googleAiOptimization: "https://developers.google.com/search/docs/fundamentals/ai-optimization-guide#mythbusting",
  umbracoCloudCloudflare:
    "https://docs.umbraco.com/umbraco-cloud/go-live/manage-hostnames#is-your-hostname-managed-proxied-in-your-own-cloudflare-account",
  aiVisibilityPackage: "https://marketplace.umbraco.com/package/umbraco.community.aivisibility",
};

export type HelpLink = { label: string; href: string };
export type HelpOption = {
  id: "cloudflare" | "umbraco-cloud" | "ai-visibility";
  title: string;
  text: string;
  links: Array<HelpLink>;
};
export type Help = { intro?: string; options: Array<HelpOption> };

const AI_VISIBILITY_LINK: HelpLink = { label: "AI Visibility for Umbraco", href: GUIDE_LINKS.aiVisibilityPackage };

// The Cloudflare headers that are on every response from a site behind it.
export function isCloudflare(headers: Record<string, string> | undefined | null): boolean {
  if (!headers) return false;
  const lower = Object.fromEntries(Object.entries(headers).map(([key, value]) => [key.toLowerCase(), value]));
  return Boolean(lower["cf-ray"] || lower["cf-cache-status"] || lower["server"]?.toLowerCase() === "cloudflare");
}

// "What can I do?" when a page isn't answered with markdown. The advice depends on where the site is hosted, so only
// what applies is shown. Used by the panel and repeated in the copied report.
export function helpFor(hosting: { isUmbracoCloud: boolean; isCloudflare: boolean }): Help {
  if (hosting.isUmbracoCloud) {
    return {
      intro:
        "Your site is on Umbraco Cloud. Umbraco Cloud uses Cloudflare, but it doesn't currently support Markdown " +
        "for Agents. We can't tell whether this site uses the Cloudflare that Umbraco Cloud provides or your own, " +
        "so there are two ways forward.",
      options: [
        {
          id: "umbraco-cloud",
          title: "Option 1: use your own Cloudflare account",
          text:
            "Put your hostname through your own Cloudflare account, on a Pro, Business or Enterprise plan, then " +
            "switch on Markdown for Agents (AI Crawl Control).",
          links: [
            { label: "Umbraco Cloud: your own Cloudflare account", href: GUIDE_LINKS.umbracoCloudCloudflare },
            { label: "Cloudflare: Markdown for Agents", href: GUIDE_LINKS.markdownForAgents },
          ],
        },
        {
          id: "ai-visibility",
          title: "Option 2: install AI Visibility for Umbraco",
          text: "This community package makes the site answer agents with a markdown version of each page.",
          links: [AI_VISIBILITY_LINK],
        },
      ],
    };
  }

  if (hosting.isCloudflare) {
    return {
      intro: "Your site is using Cloudflare, but it isn't answering with markdown.",
      options: [
        {
          id: "cloudflare",
          title: "Switch on Markdown for Agents",
          text:
            "In your Cloudflare dashboard select your site, open AI Crawl Control and turn on Markdown for Agents. " +
            "If you can't find it, the site is probably on the Free plan: Markdown for Agents needs at least a " +
            "Cloudflare Pro plan (or Business or Enterprise), so the plan has to be upgraded first.",
          links: [{ label: "Cloudflare: Markdown for Agents", href: GUIDE_LINKS.markdownForAgents }],
        },
        {
          id: "ai-visibility",
          title: "If upgrading Cloudflare isn't an option",
          text: "Install the community package AI Visibility for Umbraco instead.",
          links: [AI_VISIBILITY_LINK],
        },
      ],
    };
  }

  return {
    options: [
      {
        id: "ai-visibility",
        title: "Install AI Visibility for Umbraco",
        text:
          "This page didn't come back as markdown, and we didn't detect Cloudflare or Umbraco Cloud in front of the " +
          "site. One option is the community package AI Visibility for Umbraco, which makes the site answer agents " +
          "with a markdown version of each page.",
        links: [AI_VISIBILITY_LINK],
      },
    ],
  };
}

export const HELP_NOT_SURE =
  "Not sure? Send this report to someone who looks after your website's technical setup. They can take it from here.";

export const REPORT_FOOTER = "Generated by Agent Specs for Umbraco (Crumpled.AgentSpecs).";
