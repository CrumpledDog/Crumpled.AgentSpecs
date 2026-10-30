using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Crumpled.AgentSpecs.Models;
using Microsoft.Extensions.Options;

namespace Crumpled.AgentSpecs.Services
{
    public class AgentMarkdownFetcher(IHttpClientFactory httpClientFactory, IOptions<AgentSpecsOptions> options)
    {
        public const string HttpClientName = "Crumpled.AgentSpecs";

        public const string DefaultUserAgent = "Crumpled.AgentSpecs/1.0";

        private const int MaxRedirects = 5;

        private static readonly string[] ReportedHeaders =
        [
            "content-signal", "cf-cache-status", "vary", "cache-control", "server", "cf-ray", "age",
        ];

        public async Task<AgentSpecsResponseModel> FetchAsync(Uri url, CancellationToken cancellationToken)
        {
            var settings = options.Value;
            var userAgent = ResolveUserAgent(settings);
            var client = httpClientFactory.CreateClient(HttpClientName);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)));

            var stopwatch = Stopwatch.StartNew();
            var current = url;

            for (var hop = 0; ; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/markdown"));

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                var location = response.Headers.Location;
                if ((int)response.StatusCode is >= 300 and < 400 && location is not null)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    if (hop >= MaxRedirects || !IsAllowedRedirect(url, next))
                    {
                        throw new InvalidOperationException($"Refused to follow a redirect to {next}. Only redirects within the requested origin (or an http to https upgrade) are followed (max {MaxRedirects}).");
                    }

                    current = next;
                    continue;
                }

                var (bytes, truncated) = await ReadBodyAsync(response, settings.MaxBodyBytes, timeout.Token);
                stopwatch.Stop();
                var body = Decode(bytes, response.Content.Headers.ContentType?.CharSet);

                var isMarkdown = response.Content.Headers.ContentType?.MediaType?.Equals("text/markdown", StringComparison.OrdinalIgnoreCase) == true;
                var parsed = CloudflareMarkdownParser.Parse(body);

                return new AgentSpecsResponseModel
                {
                    RequestedUrl = url.ToString(),
                    FinalUrl = current.ToString(),
                    StatusCode = (int)response.StatusCode,
                    ContentType = response.Content.Headers.ContentType?.ToString(),
                    IsMarkdown = isMarkdown,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    Truncated = truncated,
                    UserAgent = userAgent,
                    Headers = CollectHeaders(response),
                    Raw = body,
                    Frontmatter = parsed.Frontmatter,
                    Markdown = isMarkdown ? parsed.Markdown : string.Empty,
                    JsonLd = isMarkdown ? parsed.JsonLd : [],
                };
            }
        }

        // The validated URL's origin is the only trust boundary: same host, same effective port, same scheme.
        // The one exception is the common http -> https upgrade on the default ports.
        private static bool IsAllowedRedirect(Uri original, Uri next)
        {
            if (!string.Equals(next.Host, original.Host, StringComparison.OrdinalIgnoreCase) || next.Scheme is not ("http" or "https"))
            {
                return false;
            }

            if (next.Scheme == original.Scheme)
            {
                return next.Port == original.Port;
            }

            return original.Scheme == "http" && original.IsDefaultPort && next.Scheme == "https" && next.IsDefaultPort;
        }

        public static string ResolveUserAgent(AgentSpecsOptions settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.UserAgent))
            {
                return settings.UserAgent.Trim();
            }

            return DefaultUserAgent;
        }

        private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in ReportedHeaders)
            {
                if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
                {
                    headers[name] = string.Join(", ", values);
                }
            }

            return headers;
        }

        // Honour the declared charset (UTF-8 when absent or unknown) and any byte order mark.
        private static string Decode(byte[] bytes, string? charset)
        {
            var encoding = Encoding.UTF8;
            if (!string.IsNullOrWhiteSpace(charset))
            {
                try
                {
                    encoding = Encoding.GetEncoding(charset.Trim('"', (char)39, ' '));
                }
                catch (ArgumentException)
                {
                    // Unknown charset name: keep UTF-8.
                }
            }

            using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        private static async Task<(byte[] Body, bool Truncated)> ReadBodyAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            var truncated = false;

            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    buffer.Write(chunk, 0, (int)(maxBytes - buffer.Length));
                    truncated = true;
                    break;
                }

                buffer.Write(chunk, 0, read);
            }

            return (buffer.ToArray(), truncated);
        }
    }
}
