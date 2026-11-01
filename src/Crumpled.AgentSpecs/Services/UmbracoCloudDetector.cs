using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Hosting;

namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// Tells whether the site Agent Specs is running in is hosted on Umbraco Cloud. The guidance for a page that
    /// isn't answered with markdown differs there, because the Cloudflare in front of it isn't the editor's own.
    /// </summary>
    public class UmbracoCloudDetector(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
    {
        private const string CloudFile = "umbraco-cloud.json";

        public bool IsUmbracoCloud()
        {
            if (configuration.GetValue<bool>("Umbraco:Cloud:IsRunningOnCloud"))
            {
                return true;
            }

            // Set whenever the site is configured for Umbraco Cloud, even when it is running locally.
            if (!string.IsNullOrEmpty(configuration.GetValue<string>("Umbraco:Cloud:Identity:EnvironmentId")))
            {
                return true;
            }

            // Fallback: configuration may not be available yet, depending on registration order.
            return File.Exists(Path.Combine(hostingEnvironment.ApplicationPhysicalPath, CloudFile));
        }
    }
}
