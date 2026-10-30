using System.Net;
using System.Security.Claims;
using Crumpled.AgentSpecs.Controllers;
using Crumpled.AgentSpecs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Api.Management.Factories;
using Umbraco.Cms.Api.Management.ViewModels.Document;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Crumpled.AgentSpecs.Tests;

public class CrumpledAgentSpecsApiControllerTests
{
    private const string PageUrl = "https://www.example.com/";

    private static readonly Guid DocumentId = Guid.NewGuid();

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
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
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var handler = new StubHandler(respond ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(AgentMarkdownFetcher.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        var options = Options.Create(new AgentSpecsOptions());

        var content = Substitute.For<IContent>();
        var contentService = Substitute.For<IContentService>();
        contentService.GetById(DocumentId).Returns(content);

        var urlFactory = Substitute.For<IDocumentUrlFactory>();
        urlFactory.CreateUrlsAsync(content).Returns([new DocumentUrlInfo { Culture = null, Url = PageUrl, Provider = "test", Message = null }]);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext());

        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var controller = new CrumpledAgentSpecsApiController(
            new DocumentUrlValidator(contentService, urlFactory, accessor, options),
            new AgentMarkdownFetcher(clients, options),
            authorization)
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
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task GetMarkdown_AuthorizedEditor_FetchesThePage()
    {
        var (controller, _, handler) = Create(authorized: true);

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task GetMarkdown_UpstreamBodyReadFailure_Returns502()
    {
        var (controller, _, _) = Create(authorized: true, _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingBody() });

        var result = await controller.GetMarkdown(DocumentId, PageUrl, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, response.StatusCode);
    }
}
