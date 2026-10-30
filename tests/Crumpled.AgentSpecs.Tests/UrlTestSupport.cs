using Crumpled.AgentSpecs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Api.Management.ViewModels.Document;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Crumpled.AgentSpecs.Tests;

/// <summary>
/// A URL processor defined by a function, for tests.
/// </summary>
internal sealed class FakeUrlProcessor(Func<IReadOnlyList<string>, IReadOnlyList<AgentSpecsUrlTarget>> process) : IAgentSpecsUrlProcessor
{
    public Task<IReadOnlyList<AgentSpecsUrlTarget>> ProcessAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken) =>
        Task.FromResult(process(urls));
}

internal static class UrlTestSupport
{
    public static readonly Guid DocumentId = Guid.NewGuid();

    /// <summary>
    /// A target service over a document whose Umbraco-listed URLs are <paramref name="documentUrls"/>.
    /// </summary>
    public static (DocumentUrlTargetService Service, IContentService ContentService) CreateTargetService(
        bool documentExists,
        IEnumerable<string> documentUrls,
        params IAgentSpecsUrlProcessor[] processors)
    {
        var content = Substitute.For<IContent>();
        var contentService = Substitute.For<IContentService>();
        contentService.GetById(DocumentId).Returns(documentExists ? content : null);

        var factory = Substitute.For<IDocumentUrlFactory>();
        factory.CreateUrlsAsync(content).Returns(documentUrls
            .Select(url => new DocumentUrlInfo { Culture = null, Url = url, Provider = "test", Message = null })
            .ToArray());

        return (new DocumentUrlTargetService(contentService, factory, processors), contentService);
    }

    public static DocumentUrlValidator CreateValidator(
        bool documentExists,
        IEnumerable<string> documentUrls,
        string? baseUrl = null,
        IAgentSpecsUrlProcessor? processor = null)
    {
        var (service, _) = CreateTargetService(documentExists, documentUrls, processor is null ? [] : [processor]);

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("backoffice.example.com");
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        return new DocumentUrlValidator(service, accessor, Options.Create(new AgentSpecsOptions { BaseUrl = baseUrl }));
    }
}
