using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Core.Services;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// SSRF guard: the proxy may only ever request one of the document's own URLs, as listed by the
    /// same factory behind the backoffice Links panel.
    /// </summary>
    public class DocumentUrlValidator(
        IContentService contentService,
        IDocumentUrlFactory documentUrlFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        public enum Outcome
        {
            Valid,
            DocumentNotFound,
            UrlNotAllowed,
        }

        public async Task<(Outcome Outcome, Uri? Url)> ValidateAsync(Guid documentId, string url)
        {
            var content = contentService.GetById(documentId);
            if (content is null)
            {
                return (Outcome.DocumentNotFound, null);
            }

            var requested = Resolve(url);
            if (requested is null)
            {
                return (Outcome.UrlNotAllowed, null);
            }

            var urls = await documentUrlFactory.CreateUrlsAsync(content);
            var allowed = urls
                .Select(x => Resolve(x.Url))
                .Any(x => x is not null && Uri.Compare(x, requested, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0);

            return allowed ? (Outcome.Valid, requested) : (Outcome.UrlNotAllowed, null);
        }

        private Uri? Resolve(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            // On Linux, "/about/" parses as an absolute file:// URI, so root-relative paths are handled first.
            var isRootRelative = url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal);
            if (!isRootRelative && Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            {
                return absolute.Scheme is "http" or "https" ? absolute : null;
            }

            var request = httpContextAccessor.HttpContext?.Request;
            if (request is null || !Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var baseUri))
            {
                return null;
            }

            return Uri.TryCreate(baseUri, url, out var resolved) ? resolved : null;
        }
    }
}
