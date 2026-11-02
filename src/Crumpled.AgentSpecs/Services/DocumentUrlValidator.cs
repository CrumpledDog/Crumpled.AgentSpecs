using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// SSRF guard: the proxy may only ever request one of the URLs offered for the document, as listed by
    /// <see cref="DocumentUrlTargetService"/> (Umbraco's own list behind the Links panel, after any URL processing).
    /// </summary>
    public class DocumentUrlValidator(
        DocumentUrlTargetService targetService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AgentSpecsOptions> options)
    {
        public enum Outcome
        {
            Valid,
            DocumentNotFound,
            UrlNotAllowed,
        }

        /// <returns>
        /// When valid, the URL to request and the URL to show the editor. They differ when URL processing adds
        /// to the request, for example an access token.
        /// </returns>
        public async Task<(Outcome Outcome, Uri? RequestUrl, Uri? DisplayUrl)> ValidateAsync(Guid documentId, string url, CancellationToken cancellationToken = default)
        {
            var targets = await targetService.GetTargetsAsync(documentId, cancellationToken);
            if (targets is null)
            {
                return (Outcome.DocumentNotFound, null, null);
            }

            var requested = Resolve(url);
            if (requested is null)
            {
                return (Outcome.UrlNotAllowed, null, null);
            }

            foreach (var target in targets)
            {
                var display = Resolve(target.Url);
                if (display is null || !IsSameUrl(display, requested))
                {
                    continue;
                }

                var request = Resolve(target.RequestUrl);
                return request is null
                    ? (Outcome.UrlNotAllowed, null, null)
                    : (Outcome.Valid, request, display);
            }

            return (Outcome.UrlNotAllowed, null, null);
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
