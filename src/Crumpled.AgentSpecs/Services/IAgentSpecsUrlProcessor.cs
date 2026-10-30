namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// Decides which of a document's URLs Agent Specs offers, and what is actually requested for each.
    /// </summary>
    /// <remarks>
    /// By default every URL Umbraco lists for the document is offered and requested as-is. Register an
    /// implementation (the last registration wins) when your site narrows or rewrites the URLs editors see,
    /// for example to hide hostnames that don't belong to this environment or to append an access token:
    /// <code>
    /// builder.Services.AddScoped&lt;IAgentSpecsUrlProcessor, MyUrlProcessor&gt;();
    /// </code>
    /// Use a scoped (or transient) registration if the implementation depends on request-scoped services such as
    /// the current backoffice user.
    /// </remarks>
    public interface IAgentSpecsUrlProcessor
    {
        /// <summary>
        /// Processes the document's URLs, exactly as Umbraco lists them (relative or absolute).
        /// </summary>
        /// <param name="urls">The document's distinct URLs.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// The URLs to offer. Entries whose <see cref="AgentSpecsUrlTarget.Url"/> is not one of <paramref name="urls"/> are ignored.
        /// </returns>
        Task<IReadOnlyList<AgentSpecsUrlTarget>> ProcessAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A URL offered to the editor, and the URL requested on their behalf.
    /// </summary>
    /// <param name="Url">The document URL, as shown to the editor. This is what identifies the target.</param>
    /// <param name="RequestUrl">What is actually requested. It may add to <paramref name="Url"/>, for example an access token, which is never sent to the browser.</param>
    public sealed record AgentSpecsUrlTarget(string Url, string RequestUrl);
}
