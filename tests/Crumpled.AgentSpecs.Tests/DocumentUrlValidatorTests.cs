using Crumpled.AgentSpecs.Services;
using static Crumpled.AgentSpecs.Tests.UrlTestSupport;

namespace Crumpled.AgentSpecs.Tests;

public class DocumentUrlValidatorTests
{
    [Fact]
    public async Task ValidateAsync_UnknownDocument_ReturnsNotFound()
    {
        var (outcome, _, _) = await CreateValidator(false, []).ValidateAsync(DocumentId, "/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.DocumentNotFound, outcome);
    }

    [Fact]
    public async Task ValidateAsync_RelativeUrlListedForDocument_ResolvesAgainstBackofficeHost()
    {
        var (outcome, requestUrl, displayUrl) = await CreateValidator(true, ["/about/"]).ValidateAsync(DocumentId, "/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, outcome);
        Assert.Equal("https://backoffice.example.com/about/", requestUrl!.ToString());
        Assert.Equal("https://backoffice.example.com/about/", displayUrl!.ToString());
    }

    [Fact]
    public async Task ValidateAsync_AbsoluteUrlListedForDocument_IsValid()
    {
        var (outcome, _, _) = await CreateValidator(true, ["https://www.example.com/about/"])
            .ValidateAsync(DocumentId, "https://WWW.example.com/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, outcome);
    }

    [Theory]
    [InlineData("https://evil.example.net/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("")]
    public async Task ValidateAsync_UrlNotBelongingToDocument_IsRejected(string url)
    {
        var (outcome, requestUrl, displayUrl) = await CreateValidator(true, ["https://www.example.com/about/"]).ValidateAsync(DocumentId, url);

        Assert.Equal(DocumentUrlValidator.Outcome.UrlNotAllowed, outcome);
        Assert.Null(requestUrl);
        Assert.Null(displayUrl);
    }

    [Fact]
    public async Task ValidateAsync_ConfiguredBaseUrl_IsUsedInsteadOfTheRequestHost()
    {
        var (outcome, requestUrl, _) = await CreateValidator(true, ["/about/"], baseUrl: "https://www.example.com")
            .ValidateAsync(DocumentId, "/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, outcome);
        Assert.Equal("https://www.example.com/about/", requestUrl!.ToString());
    }

    [Theory]
    [InlineData("/ABOUT/")]
    [InlineData("/about")]
    [InlineData("/about/?x=1")]
    [InlineData("/about%2F")]
    public async Task ValidateAsync_PathOrQueryThatDiffersFromTheDocumentUrl_IsRejected(string requested)
    {
        var (outcome, _, _) = await CreateValidator(true, ["/about/"]).ValidateAsync(DocumentId, requested);

        Assert.Equal(DocumentUrlValidator.Outcome.UrlNotAllowed, outcome);
    }

    [Fact]
    public async Task ValidateAsync_ProcessorNarrowsTheUrls_OnlyTheOfferedOnesAreAllowed()
    {
        var processor = new FakeUrlProcessor(urls => urls
            .Where(url => url.Contains("www.example.com"))
            .Select(url => new AgentSpecsUrlTarget(url, url))
            .ToList());
        var validator = CreateValidator(true, ["https://www.example.com/about/", "https://old.example.net/about/"], processor: processor);

        var (offered, _, _) = await validator.ValidateAsync(DocumentId, "https://www.example.com/about/");
        var (hidden, _, _) = await validator.ValidateAsync(DocumentId, "https://old.example.net/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, offered);
        Assert.Equal(DocumentUrlValidator.Outcome.UrlNotAllowed, hidden);
    }

    [Fact]
    public async Task ValidateAsync_ProcessorAddsToTheRequest_RequestsTheProcessedUrlButShowsTheListedOne()
    {
        var processor = new FakeUrlProcessor(urls => urls
            .Select(url => new AgentSpecsUrlTarget(url, url + "?UMB_AUTH_TOKEN=secret"))
            .ToList());

        var (outcome, requestUrl, displayUrl) = await CreateValidator(true, ["https://www.example.com/about/"], processor: processor)
            .ValidateAsync(DocumentId, "https://www.example.com/about/");

        Assert.Equal(DocumentUrlValidator.Outcome.Valid, outcome);
        Assert.Equal("https://www.example.com/about/?UMB_AUTH_TOKEN=secret", requestUrl!.ToString());
        Assert.Equal("https://www.example.com/about/", displayUrl!.ToString());
    }

    [Fact]
    public async Task ValidateAsync_TheCallerCannotAskForAProcessedUrlDirectly()
    {
        var processor = new FakeUrlProcessor(urls => urls
            .Select(url => new AgentSpecsUrlTarget(url, url + "?UMB_AUTH_TOKEN=secret"))
            .ToList());

        var (outcome, _, _) = await CreateValidator(true, ["https://www.example.com/about/"], processor: processor)
            .ValidateAsync(DocumentId, "https://www.example.com/about/?UMB_AUTH_TOKEN=secret");

        Assert.Equal(DocumentUrlValidator.Outcome.UrlNotAllowed, outcome);
    }
}
