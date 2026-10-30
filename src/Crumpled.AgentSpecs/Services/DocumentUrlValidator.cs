using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
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
        IHttpContextAccessor httpContextAccessor,
        IOptions<AgentSpecsOptions> options)
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
                .Any(x => x is not null && IsSameUrl(x, requested));

            return allowed ? (Outcome.Valid, requested) : (Outcome.UrlNotAllowed, null);
        }

        // Only the scheme and host are case-insensitive. The path and query are compared ordinally in their
        // escaped form, so differently-escaped or differently-cased paths can't match the allow-list while
        // the fetcher sends the caller's original URL.
        private static bool IsSameUrl(Uri allowed, Uri requested) =>
            Uri.Compare(allowed, requested, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0
            && Uri.Compare(allowed, requested, UriComponents.PathAndQuery, UriFormat.UriEscaped, StringComparison.Ordinal) == 0;

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

            var baseUri = ResolveBaseUri();
            return baseUri is not null && Uri.TryCreate(baseUri, url, out var resolved) ? resolved : null;
        }

        // Prefer the configured trusted origin; otherwise fall back to the backoffice request's own authority.
        private Uri? ResolveBaseUri()
        {
            var configured = options.Value.BaseUrl;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Uri.TryCreate(configured, UriKind.Absolute, out var baseUrl) && baseUrl.Scheme is "http" or "https"
                    ? baseUrl
                    : null;
            }

            var request = httpContextAccessor.HttpContext?.Request;
            return request is not null && Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var requestBase)
                ? requestBase
                : null;
        }
    }
}
