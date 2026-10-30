using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Core.Services;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// A URL an editor may request for a document: the URL as listed (and shown), and the URL actually requested.
    /// </summary>
    public sealed record DocumentUrlTarget(string? Culture, string Url, string RequestUrl);

    /// <summary>
    /// Lists the URLs Agent Specs offers for a document: Umbraco's own list, as behind the backoffice Links
    /// panel, passed through the registered <see cref="IAgentSpecsUrlProcessor"/>.
    /// </summary>
    public class DocumentUrlTargetService(
        IContentService contentService,
        IDocumentUrlFactory documentUrlFactory,
        IEnumerable<IAgentSpecsUrlProcessor> processors)
    {
        /// <returns>The targets, or <c>null</c> when the document doesn't exist.</returns>
        public async Task<IReadOnlyList<DocumentUrlTarget>?> GetTargetsAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var content = contentService.GetById(documentId);
            if (content is null)
            {
                return null;
            }

            var listed = (await documentUrlFactory.CreateUrlsAsync(content))
                .Where(info => !string.IsNullOrWhiteSpace(info.Url))
                .Select(info => (info.Culture, Url: info.Url!))
                .DistinctBy(info => info.Url, StringComparer.Ordinal)
                .ToList();
            if (listed.Count == 0)
            {
                return [];
            }

            var urls = listed.Select(info => info.Url).ToList();

            // The last registration wins, so a site can replace processing registered by another package.
            var processor = processors.LastOrDefault();
            var processed = processor is null
                ? urls.Select(url => new AgentSpecsUrlTarget(url, url)).ToList()
                : await processor.ProcessAsync(urls, cancellationToken);

            // A processor can only narrow or rewrite the document's own URLs, never introduce new ones.
            var byUrl = processed
                .Where(target => !string.IsNullOrWhiteSpace(target.RequestUrl))
                .DistinctBy(target => target.Url, StringComparer.Ordinal)
                .ToDictionary(target => target.Url, StringComparer.Ordinal);

            return listed
                .Where(info => byUrl.ContainsKey(info.Url))
                .Select(info => new DocumentUrlTarget(info.Culture, info.Url, byUrl[info.Url].RequestUrl))
                .ToList();
        }
    }
}
