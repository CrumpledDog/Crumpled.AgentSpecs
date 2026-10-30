using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace Crumpled.AgentSpecs.Composers
{
    // Umbraco 17 (Swashbuckle) OpenAPI document registration. See OpenApiRegistration.Umbraco18.cs for the
    // Umbraco 18 (native OpenApi) equivalent - the .csproj conditionally compiles only one of the two based
    // on $(UmbracoTargetMajor) from the repo-root Directory.Build.props.
    public class CrumpledAgentSpecsOpenApiComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>();

            builder.Services.Configure<SwaggerGenOptions>(opt =>
            {
                opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
                {
                    Title = "Crumpled Package Template Backoffice API",
                    Version = "1.0",
                });

                opt.OperationFilter<CrumpledAgentSpecsOperationSecurityFilter>();
            });
        }

        public class CrumpledAgentSpecsOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
        {
            protected override string ApiName => Constants.ApiName;
        }

        // Reuses Umbraco's own OperationIdHandler naming (no Handle override), scoped to this package's
        // [MapToApi] controllers, so Umbraco 17 emits the same operationIds - and therefore the same generated
        // SDK names - as Umbraco 18. This keeps Client/src call sites identical across the v1/v2 lines.
        public class CustomOperationHandler : OperationIdHandler
        {
            public CustomOperationHandler(IOptions<ApiVersioningOptions> apiVersioningOptions) : base(apiVersioningOptions)
            {
            }

            protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
                => controllerActionDescriptor.HasMapToApiAttribute(Constants.ApiName);
        }
    }
}
