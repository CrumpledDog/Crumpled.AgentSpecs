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
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                });

            builder.Services.AddScoped<DocumentUrlValidator>();
            builder.Services.AddScoped<AgentMarkdownFetcher>();
        }
    }
}
