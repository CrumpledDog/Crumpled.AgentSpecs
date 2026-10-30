using Crumpled.AgentSpecs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Api.Management.ViewModels.Document;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Crumpled.AgentSpecs.Tests;

public class DocumentUrlValidatorTests
{
    private static readonly Guid DocumentId = Guid.NewGuid();

    private static DocumentUrlValidator Create(bool documentExists, params string[] documentUrls) =>
        CreateWithBaseUrl(documentExists, null, documentUrls);

    private static DocumentUrlValidator CreateWithBaseUrl(bool documentExists, string? baseUrl, params string[] documentUrls)
    {
        var content = Substitute.For<IContent>();
        var contentService = Substitute.For<IContentService>();
        contentService.GetById(DocumentId).Returns(documentExists ? content : null);

        var factory = Substitute.For<IDocumentUrlFactory>();
        factory.CreateUrlsAsync(content).Returns(documentUrls.Select(x => new DocumentUrlInfo { Culture = null, Url = x, Provider = "test", Message = null }).ToArray());

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("backoffice.example.com");
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        return new DocumentUrlValidator(contentService, factory, accessor, Options.Create(new AgentSpecsOptions { BaseUrl = baseUrl }));
    }

    [Fact]
    public async Task ValidateAsync_UnknownDocument_ReturnsNotFound()
    {
        var (outcome, _) = await Create(false).ValidateAsync(DocumentId, "/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.DocumentNotFound, outcome);
    }

    [Fact]
    public async Task ValidateAsync_RelativeUrlListedForDocument_ResolvesAgainstBackofficeHost()
    {
        var (outcome, url) = await Create(true, "/about/").ValidateAsync(DocumentId, "/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, outcome);
        Assert.Equal("https://backoffice.example.com/about/", url!.ToString());
    }

    [Fact]
    public async Task ValidateAsync_AbsoluteUrlListedForDocument_IsValid()
    {
        var (outcome, _) = await Create(true, "https://www.example.com/about/").ValidateAsync(DocumentId, "https://WWW.example.com/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, outcome);
    }

    [Theory]
    [InlineData("https://evil.example.net/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("")]
    public async Task ValidateAsync_UrlNotBelongingToDocument_IsRejected(string url)
    {
        var (outcome, resolved) = await Create(true, "https://www.example.com/about/").ValidateAsync(DocumentId, url);

        Assert.Equal(DocumentUrlValidator.Outcome.UrlNotAllowed, outcome);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task ValidateAsync_ConfiguredBaseUrl_IsUsedInsteadOfTheRequestHost()
    {
        var (outcome, url) = await CreateWithBaseUrl(true, "https://www.example.com", "/about/").ValidateAsync(DocumentId, "/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, outcome);
        Assert.Equal("https://www.example.com/about/", url!.ToString());
    }

    [Theory]
    [InlineData("/ABOUT/")]
    [InlineData("/about")]
    [InlineData("/about/?x=1")]
    [InlineData("/about%2F")]
    public async Task ValidateAsync_PathOrQueryThatDiffersFromTheDocumentUrl_IsRejected(string requested)
    {
        var (outcome, _) = await Create(true, "/about/").ValidateAsync(DocumentId, requested);

        Assert.Equal(DocumentUrlValidator.Outcome.UrlNotAllowed, outcome);
    }
}
