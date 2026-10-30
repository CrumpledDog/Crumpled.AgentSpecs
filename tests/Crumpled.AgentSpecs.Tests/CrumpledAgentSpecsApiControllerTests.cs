using System.Net;
using System.Security.Claims;
using Crumpled.AgentSpecs.Controllers;
using Crumpled.AgentSpecs.Models;
using Crumpled.AgentSpecs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Services;
using static Crumpled.AgentSpecs.Tests.UrlTestSupport;

namespace Crumpled.AgentSpecs.Tests;

public class CrumpledAgentSpecsApiControllerTests
{
    private const string PageUrl = "https://www.example.com/";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    // Declares a body that then fails mid-read, like an upstream that closes the connection early.
    private sealed class FailingBody : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new IOException("The response ended prematurely.");

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            throw new IOException("The response ended prematurely.");

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private static (CrumpledAgentSpecsApiController Controller, IContentService ContentService, StubHandler Handler) Create(
        bool authorized,
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null,
        IAgentSpecsUrlProcessor? processor = null,
        bool documentExists = true)
    {
        var handler = new StubHandler(respond ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(AgentMarkdownFetcher.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        var options = Options.Create(new AgentSpecsOptions());

        var (targetService, contentService) = CreateTargetService(documentExists, [PageUrl], processor is null ? [] : [processor]);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext());

        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var controller = new CrumpledAgentSpecsApiController(
            targetService,
            new DocumentUrlValidator(targetService, accessor, options),
            new AgentMarkdownFetcher(clients, options),
            authorization,
            NullLogger<CrumpledAgentSpecsApiController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        return (controller, contentService, handler);
    }

    [Fact]
    public async Task GetMarkdown_EditorWithoutAccessToTheDocument_GetsForbiddenAndNothingIsLookedUpOrFetched()
    {
        var (controller, contentService, handler) = Create(authorized: false);

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        contentService.DidNotReceiveWithAnyArgs().GetById(default(Guid));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetMarkdown_AuthorizedEditor_FetchesThePage()
    {
        var (controller, _, handler) = Create(authorized: true);

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetMarkdown_UpstreamBodyReadFailure_Returns502()
    {
        var (controller, _, _) = Create(authorized: true, _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingBody() });

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task GetMarkdown_ProcessorAddsAToken_TheTokenIsRequestedButNeverReported()
    {
        var processor = new FakeUrlProcessor(urls => urls.Select(url => new AgentSpecsUrlTarget(url, url + "?UMB_AUTH_TOKEN=secret")).ToList());
        var (controller, _, handler) = Create(authorized: true, processor: processor);

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        var model = Assert.IsType<AgentSpecsResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("https://www.example.com/?UMB_AUTH_TOKEN=secret", Assert.Single(handler.Requests).RequestUri!.ToString());
        Assert.Equal(PageUrl, model.RequestedUrl);
        Assert.Equal("https://www.example.com/?UMB_AUTH_TOKEN=redacted", model.FinalUrl);
        Assert.DoesNotContain("secret", model.RequestedUrl + model.FinalUrl);
    }

    private static IAgentSpecsUrlProcessor TokenProcessor() =>
        new FakeUrlProcessor(urls => urls.Select(url => new AgentSpecsUrlTarget(url, url + "?UMB_AUTH_TOKEN=secret")).ToList());

    private static string DetailOf(IActionResult result) =>
        Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value).Detail ?? string.Empty;

    [Fact]
    public async Task GetMarkdown_RedirectToAnotherOriginCarryingTheToken_ErrorDetailDoesNotExposeIt()
    {
        var (controller, _, _) = Create(
            authorized: true,
            _ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://www.example.com:8443/next?UMB_AUTH_TOKEN=secret") } },
            TokenProcessor());

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.DoesNotContain("secret", DetailOf(result));
        Assert.Contains("redacted", DetailOf(result));
    }

    [Fact]
    public async Task GetMarkdown_RedirectLoopThatKeepsTheToken_ErrorDetailDoesNotExposeIt()
    {
        var (controller, _, _) = Create(
            authorized: true,
            request => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = request.RequestUri } },
            TokenProcessor());

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.DoesNotContain("secret", DetailOf(result));
    }

    [Fact]
    public async Task GetMarkdown_FrameworkExceptionMessage_IsNotReturned()
    {
        var (controller, _, _) = Create(
            authorized: true,
            request => throw new HttpRequestException($"Connection failed for {request.RequestUri}"),
            TokenProcessor());

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.DoesNotContain("secret", DetailOf(result));
        Assert.DoesNotContain("Connection failed", DetailOf(result));
    }

    [Fact]
    public async Task GetUrls_EditorWithoutAccessToTheDocument_GetsForbiddenAndNothingIsLookedUp()
    {
        var (controller, contentService, _) = Create(authorized: false);

        var result = await controller.GetUrls(DocumentId, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        contentService.DidNotReceiveWithAnyArgs().GetById(default(Guid));
    }

    [Fact]
    public async Task GetUrls_UnknownDocument_Returns404()
    {
        var (controller, _, _) = Create(authorized: true, documentExists: false);

        var result = await controller.GetUrls(DocumentId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetUrls_ListsTheDocumentsUrls()
    {
        var (controller, _, _) = Create(authorized: true);

        var result = await controller.GetUrls(DocumentId, CancellationToken.None);

        var model = Assert.IsType<AgentSpecsUrlsResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal([PageUrl], model.Urls.Select(url => url.Url));
    }

    [Fact]
    public async Task GetUrls_ProcessorAddsAToken_TheTokenIsNotSentToTheBrowser()
    {
        var processor = new FakeUrlProcessor(urls => urls.Select(url => new AgentSpecsUrlTarget(url, url + "?UMB_AUTH_TOKEN=secret")).ToList());
        var (controller, _, handler) = Create(authorized: true, processor: processor);

        var result = await controller.GetUrls(DocumentId, CancellationToken.None);

        var model = Assert.IsType<AgentSpecsUrlsResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal([PageUrl], model.Urls.Select(url => url.Url));
        Assert.Empty(handler.Requests);
    }
}
