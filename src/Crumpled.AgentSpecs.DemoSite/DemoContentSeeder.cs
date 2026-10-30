using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Crumpled.AgentSpecs.DemoSite;

/// <summary>
/// Creates a "Page" document type, its template and a published Home page on first start, so the
/// Agent Specs view has something to request. Idempotent, and only ever warns on failure.
/// </summary>
public class DemoContentComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoContentSeeder>();
}

public class DemoContentSeeder(
    IContentTypeService contentTypeService,
    IContentService contentService,
    ITemplateService templateService,
    IShortStringHelper shortStringHelper,
    IWebHostEnvironment environment,
    ILogger<DemoContentSeeder> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            if (contentTypeService.Get("page") is not null)
            {
                return;
            }

            var viewPath = Path.Combine(environment.ContentRootPath, "Views", "page.cshtml");
            var template = await templateService.CreateAsync(
                "Page",
                "page",
                System.IO.File.Exists(viewPath) ? await System.IO.File.ReadAllTextAsync(viewPath, cancellationToken) : null,
                Umbraco.Cms.Core.Constants.Security.SuperUserKey);

            var contentType = new ContentType(shortStringHelper, -1)
            {
                Alias = "page",
                Name = "Page",
                AllowedAsRoot = true,
                Icon = "icon-document",
            };
            if (template.Success)
            {
                contentType.AllowedTemplates = [template.Result];
                contentType.SetDefaultTemplate(template.Result);
            }

            await contentTypeService.CreateAsync(contentType, Umbraco.Cms.Core.Constants.Security.SuperUserKey);

            var home = contentService.Create("Home", Umbraco.Cms.Core.Constants.System.Root, "page");
            contentService.Save(home);
            contentService.Publish(home, ["*"]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not seed the DemoSite content");
        }
    }
}
