using Crumpled.AgentSpecs.Services;
using static Crumpled.AgentSpecs.Tests.UrlTestSupport;

namespace Crumpled.AgentSpecs.Tests;

public class DocumentUrlTargetServiceTests
{
    [Fact]
    public async Task GetTargetsAsync_UnknownDocument_ReturnsNull()
    {
        var (service, _) = CreateTargetService(false, ["/about/"]);

        Assert.Null(await service.GetTargetsAsync(DocumentId));
    }

    [Fact]
    public async Task GetTargetsAsync_NoProcessor_OffersEveryListedUrlAsIs()
    {
        var (service, _) = CreateTargetService(true, ["https://a.example/x", "https://b.example/x"]);

        var targets = await service.GetTargetsAsync(DocumentId);

        Assert.Equal(["https://a.example/x", "https://b.example/x"], targets!.Select(t => t.Url));
        Assert.All(targets!, t => Assert.Equal(t.Url, t.RequestUrl));
    }

    [Fact]
    public async Task GetTargetsAsync_DuplicateUrls_AreOfferedOnce()
    {
        var (service, _) = CreateTargetService(true, ["/about/", "/about/", "/contact/"]);

        var targets = await service.GetTargetsAsync(DocumentId);

        Assert.Equal(["/about/", "/contact/"], targets!.Select(t => t.Url));
    }

    [Fact]
    public async Task GetTargetsAsync_ProcessorNarrowsToOneUrl_OnlyThatIsOffered()
    {
        var processor = new FakeUrlProcessor(urls => [new AgentSpecsUrlTarget(urls[1], urls[1])]);
        var (service, _) = CreateTargetService(true, ["https://a.example/x", "https://b.example/x", "https://c.example/x"], processor);

        var targets = await service.GetTargetsAsync(DocumentId);

        var only = Assert.Single(targets!);
        Assert.Equal("https://b.example/x", only.Url);
    }

    [Fact]
    public async Task GetTargetsAsync_ProcessorCannotIntroduceUrlsTheDocumentDoesNotHave()
    {
        var processor = new FakeUrlProcessor(urls => [new AgentSpecsUrlTarget("https://evil.example/", "https://evil.example/")]);
        var (service, _) = CreateTargetService(true, ["/about/"], processor);

        var targets = await service.GetTargetsAsync(DocumentId);

        Assert.Empty(targets!);
    }

    [Fact]
    public async Task GetTargetsAsync_ProcessorRequestUrlIsKeptSeparateFromTheListedUrl()
    {
        var processor = new FakeUrlProcessor(urls => urls.Select(url => new AgentSpecsUrlTarget(url, url + "?token=t")).ToList());
        var (service, _) = CreateTargetService(true, ["https://a.example/x"], processor);

        var target = Assert.Single((await service.GetTargetsAsync(DocumentId))!);

        Assert.Equal("https://a.example/x", target.Url);
        Assert.Equal("https://a.example/x?token=t", target.RequestUrl);
    }

    [Fact]
    public async Task GetTargetsAsync_TheLastRegisteredProcessorWins()
    {
        var first = new FakeUrlProcessor(urls => []);
        var last = new FakeUrlProcessor(urls => urls.Select(url => new AgentSpecsUrlTarget(url, url)).ToList());
        var (service, _) = CreateTargetService(true, ["/about/"], first, last);

        var targets = await service.GetTargetsAsync(DocumentId);

        Assert.Equal(["/about/"], targets!.Select(t => t.Url));
    }
}
