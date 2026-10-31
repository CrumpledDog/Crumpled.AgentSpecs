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
};
