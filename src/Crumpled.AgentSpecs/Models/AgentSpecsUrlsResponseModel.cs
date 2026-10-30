namespace Crumpled.AgentSpecs.Models
{
    public class AgentSpecsUrlsResponseModel
    {
        public required IReadOnlyList<AgentSpecsUrlModel> Urls { get; init; }
    }

    public class AgentSpecsUrlModel
    {
        public string? Culture { get; init; }

        /// <summary>
        /// The document URL as listed. Never includes anything added to the request, such as an access token.
        /// </summary>
        public required string Url { get; init; }
    }
}
