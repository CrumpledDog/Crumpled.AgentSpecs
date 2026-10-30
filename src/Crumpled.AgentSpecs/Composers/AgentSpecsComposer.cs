using Crumpled.AgentSpecs.Services;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Crumpled.AgentSpecs.Composers
{
    public class AgentSpecsComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.Configure<AgentSpecsOptions>(builder.Config.GetSection(AgentSpecsOptions.SectionName));

            builder.Services.AddHttpContextAccessor();
            builder.Services
                .AddHttpClient(AgentMarkdownFetcher.HttpClientName)
                // The fetcher enforces the configured TimeoutSeconds itself, so the client's built-in 100s limit must not cap it.
                .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    // The handler is pooled and shared by every editor, so cookies from one fetch must never be replayed on another.
                    UseCookies = false,
                });

            builder.Services.AddScoped<DocumentUrlValidator>();
            builder.Services.AddScoped<AgentMarkdownFetcher>();
        }
    }
}
