using System.Net;
using System.Text;
using Crumpled.AgentSpecs.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Crumpled.AgentSpecs.Tests;

public class AgentMarkdownFetcherTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Markdown(string body, string contentType = "text/markdown; charset=utf-8") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue(contentType.Split(';')[0]) { CharSet = "utf-8" }) };

    private static AgentMarkdownFetcher CreateFetcher(StubHandler handler, AgentSpecsOptions? settings = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AgentMarkdownFetcher.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new AgentMarkdownFetcher(factory, Options.Create(settings ?? new AgentSpecsOptions()));
    }

    [Fact]
    public async Task FetchAsync_DecodesTheBodyUsingTheDeclaredCharset()
    {
        var latin1 = Encoding.GetEncoding("iso-8859-1");
        var handler = new StubHandler(_ =>
        {
            var content = new ByteArrayContent(latin1.GetBytes("café"));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/markdown") { CharSet = "iso-8859-1" };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.Equal("café", result.Raw);
        Assert.Equal("café", result.Markdown);
    }

    [Theory]
    [InlineData("not-a-real-charset")]
    [InlineData("utf-7")]
    [InlineData("shift_jis")]
    public async Task FetchAsync_UnknownOrUnavailableCharset_FallsBackToUtf8(string charset)
    {
        var handler = new StubHandler(_ =>
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes("café"));
            content.Headers.TryAddWithoutValidation("Content-Type", $"text/markdown; charset={charset}");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.Equal("café", result.Raw);
    }

    [Fact]
    public async Task FetchAsync_NoCharsetDeclared_DecodesAsUtf8()
    {
        var handler = new StubHandler(_ =>
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes("café"));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/markdown");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.Equal("café", result.Raw);
    }

    [Fact]
    public async Task FetchAsync_SendsAcceptMarkdownAndDefaultUserAgent()
    {
        var handler = new StubHandler(_ => Markdown("# Hello"));

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("text/markdown", request.Headers.Accept.ToString());
        Assert.Equal("Crumpled.AgentSpecs/1.0", request.Headers.UserAgent.ToString());
        Assert.Equal(request.Headers.UserAgent.ToString(), result.UserAgent);
        Assert.True(result.IsMarkdown);
        Assert.Equal("# Hello", result.Markdown);
    }

    [Fact]
    public async Task FetchAsync_ConfiguredUserAgent_IsUsed()
    {
        var handler = new StubHandler(_ => Markdown("x"));
        var fetcher = CreateFetcher(handler, new AgentSpecsOptions { UserAgent = "MyBot/2.0" });

        await fetcher.FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.Equal("MyBot/2.0", handler.Requests.Single().Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task FetchAsync_HtmlResponse_IsFlaggedAsNotMarkdown()
    {
        var handler = new StubHandler(_ => Markdown("<html></html>", "text/html"));

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.False(result.IsMarkdown);
        Assert.Equal(string.Empty, result.Markdown);
        Assert.Equal("<html></html>", result.Raw);
    }

    [Fact]
    public async Task FetchAsync_SameHostRedirect_IsFollowed()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath == "/old"
            ? new HttpResponseMessage(HttpStatusCode.Moved) { Headers = { Location = new Uri("/new", UriKind.Relative) } }
            : Markdown("moved"));

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.com/old"), CancellationToken.None);

        Assert.Equal("https://example.com/new", result.FinalUrl);
        Assert.Equal("moved", result.Markdown);
    }

    [Fact]
    public async Task FetchAsync_CrossHostRedirect_IsRefused()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://internal.local/secret") } });

        await Assert.ThrowsAsync<AgentSpecsFetchException>(() =>
            CreateFetcher(handler).FetchAsync(new Uri("https://example.com/"), CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task FetchAsync_BodyOverLimit_IsTruncated()
    {
        var handler = new StubHandler(_ => Markdown(new string('a', 100)));

        var result = await CreateFetcher(handler, new AgentSpecsOptions { MaxBodyBytes = 10 })
            .FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.True(result.Truncated);
        Assert.Equal(10, result.Raw.Length);
    }

    [Theory]
    [InlineData("https://example.com/page", "https://example.com:8080/admin")]
    [InlineData("https://example.com/page", "http://example.com/page")]
    [InlineData("http://example.com/page", "http://example.com:8080/admin")]
    [InlineData("http://example.com/page", "https://example.com:8443/page")]
    public async Task FetchAsync_RedirectToADifferentPortOrSchemeOfTheSameHost_IsRefused(string requested, string redirect)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri(redirect) } });

        await Assert.ThrowsAsync<AgentSpecsFetchException>(() =>
            CreateFetcher(handler).FetchAsync(new Uri(requested), CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("https://example.com/p", "https://example.com/p?token=secret", "https://example.com/p?token=redacted")]
    [InlineData("https://example.com/p?token=", "https://example.com/p?token=secret", "https://example.com/p?token=redacted")]
    [InlineData("https://example.com/p?token=old", "https://example.com/p?token=secret", "https://example.com/p?token=redacted")]
    [InlineData("https://example.com/p?token=", "https://example.com/p?token=&token=secret", "https://example.com/p?token=&token=redacted")]
    [InlineData("https://example.com/p?a=1", "https://example.com/p?a=1&t=secret", "https://example.com/p?a=1&t=redacted")]
    [InlineData("https://example.com/p?a=1", "https://example.com/p?a=1", "https://example.com/p?a=1")]
    public async Task FetchAsync_AnythingTheRequestAddedOrChangedInTheQuery_IsRedactedFromTheReportedUrls(string display, string request, string expectedFinal)
    {
        var handler = new StubHandler(_ => Markdown("ok"));

        var result = await CreateFetcher(handler).FetchAsync(new Uri(request), CancellationToken.None, new Uri(display));

        Assert.Equal(display, result.RequestedUrl);
        Assert.Equal(expectedFinal, result.FinalUrl);
        Assert.Equal(request, Assert.Single(handler.Requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task FetchAsync_RedirectToAnotherOriginWithTheToken_ExceptionMessageRedactsIt()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://other.example/x?token=secret") } });

        var exception = await Assert.ThrowsAsync<AgentSpecsFetchException>(() =>
            CreateFetcher(handler).FetchAsync(new Uri("https://example.com/p?token=secret"), CancellationToken.None, new Uri("https://example.com/p")));

        Assert.DoesNotContain("secret", exception.Message);
    }

    [Fact]
    public async Task FetchAsync_HttpToHttpsUpgradeOnTheDefaultPorts_IsFollowed()
    {
        var handler = new StubHandler(request => request.RequestUri!.Scheme == "http"
            ? new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri("https://example.com/page") } }
            : Markdown("secure"));

        var result = await CreateFetcher(handler).FetchAsync(new Uri("http://example.com/page"), CancellationToken.None);

        Assert.Equal("https://example.com/page", result.FinalUrl);
        Assert.Equal("secure", result.Markdown);
    }
}
