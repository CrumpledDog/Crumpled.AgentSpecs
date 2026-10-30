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

        public const string DefaultUserAgent = "Crumpled.AgentSpecs-Client/1.0";

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
                    if (hop >= MaxRedirects || !string.Equals(next.Host, url.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Refused to follow a redirect to {next}. Only same-host redirects are followed (max {MaxRedirects}).");
                    }

                    current = next;
                    continue;
                }

                var (body, truncated) = await ReadBodyAsync(response, settings.MaxBodyBytes, timeout.Token);
                stopwatch.Stop();

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

        private static async Task<(string Body, bool Truncated)> ReadBodyAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
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

            return (Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length), truncated);
        }
    }
}
