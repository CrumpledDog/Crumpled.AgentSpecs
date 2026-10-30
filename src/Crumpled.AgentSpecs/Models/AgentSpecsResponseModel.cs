namespace Crumpled.AgentSpecs.Models
{
    public class AgentSpecsResponseModel
    {
        public required string RequestedUrl { get; init; }

        public required string FinalUrl { get; init; }

        public int StatusCode { get; init; }

        public string? ContentType { get; init; }

        public bool IsMarkdown { get; init; }

        public long ElapsedMs { get; init; }

        public bool Truncated { get; init; }

        public required string UserAgent { get; init; }

        public required Dictionary<string, string> Headers { get; init; }

        public required string Raw { get; init; }

        public required FrontmatterModel Frontmatter { get; init; }

        public required string Markdown { get; init; }

        public required IReadOnlyList<JsonLdModel> JsonLd { get; init; }
    }

    public class FrontmatterModel
    {
        public string? Raw { get; init; }

        public required Dictionary<string, string> Fields { get; init; }
    }

    public class JsonLdModel
    {
        public required string Raw { get; init; }

        public string? Error { get; init; }
    }
}
