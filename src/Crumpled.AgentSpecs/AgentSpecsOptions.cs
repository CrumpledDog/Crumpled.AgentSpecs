namespace Crumpled.AgentSpecs
{
    public class AgentSpecsOptions
    {
        public const string SectionName = "Crumpled:AgentSpecs";

        /// <summary>
        /// The User-Agent header sent when requesting a page as an agent. When empty, a default identifying
        /// this package (and its version) is used. Set this if your WAF/bot rules need a specific value.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// The origin (e.g. https://www.example.com) that root-relative document URLs are resolved against.
        /// When empty, the scheme and host of the backoffice request are used, which relies on the site only
        /// accepting trusted Host headers. Set this for a fixed, trusted origin.
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Aliases of document types that don't get the Agent Specs view, for example folders or settings nodes
        /// that have no page of their own. Matched case-insensitively against the document's own document type
        /// (not the compositions it uses). An excluded document also offers no URLs to request.
        /// </summary>
        public string[] ExcludedDocumentTypes { get; set; } = [];

        public int TimeoutSeconds { get; set; } = 30;

        public int MaxBodyBytes { get; set; } = 8 * 1024 * 1024;
    }
}
