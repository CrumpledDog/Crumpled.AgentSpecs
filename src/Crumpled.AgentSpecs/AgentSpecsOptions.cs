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

        public int TimeoutSeconds { get; set; } = 30;

        public int MaxBodyBytes { get; set; } = 8 * 1024 * 1024;
    }
}
