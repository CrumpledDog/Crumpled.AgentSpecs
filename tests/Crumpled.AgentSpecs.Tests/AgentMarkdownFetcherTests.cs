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
    public async Task FetchAsync_SendsAcceptMarkdownAndDefaultUserAgent()
    {
        var handler = new StubHandler(_ => Markdown("# Hello"));

        var result = await CreateFetcher(handler).FetchAsync(new Uri("https://example.com/"), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("text/markdown", request.Headers.Accept.ToString());
        Assert.StartsWith("Crumpled.AgentSpecs/", request.Headers.UserAgent.ToString());
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

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
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
}
