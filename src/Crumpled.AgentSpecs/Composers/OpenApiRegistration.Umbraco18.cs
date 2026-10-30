using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Crumpled.AgentSpecs.Composers;

// Umbraco 18 (native OpenApi) document registration. See OpenApiRegistration.Umbraco17.cs for the Umbraco 17
// (Swashbuckle) equivalent - the .csproj conditionally compiles only one of the two based on
// $(UmbracoTargetMajor) from the repo-root Directory.Build.props.
public class CrumpledAgentSpecsOpenApiComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddBackOfficeOpenApiDocument(
            Constants.ApiName,
            document => document
                .WithTitle("Crumpled Agent Specs Backoffice API")
                .WithBackOfficeAuthentication()
                .WithJsonOptions(Umbraco.Cms.Core.Constants.JsonOptionsNames.BackOffice)
                .ConfigureOpenApiOptions(options =>
                    options.AddDocumentTransformer((doc, _, _) =>
                    {
                        doc.Info.Version = "1.0";
                        return Task.CompletedTask;
                    })));
}
