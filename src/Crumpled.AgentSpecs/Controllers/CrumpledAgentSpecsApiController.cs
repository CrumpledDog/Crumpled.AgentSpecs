using Asp.Versioning;
using Crumpled.AgentSpecs.Models;
using Crumpled.AgentSpecs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Web.Common.Authorization;

namespace Crumpled.AgentSpecs.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Crumpled.AgentSpecs")]
    public class CrumpledAgentSpecsApiController(
        DocumentUrlTargetService targetService,
        DocumentUrlValidator urlValidator,
        AgentMarkdownFetcher fetcher,
        IAuthorizationService authorizationService,
        ILogger<CrumpledAgentSpecsApiController> logger) : CrumpledAgentSpecsApiControllerBase
    {
        /// <summary>
        /// Lists the front-end URLs that can be requested for the document: the ones behind the Links panel,
        /// after any <see cref="IAgentSpecsUrlProcessor"/> has narrowed or rewritten them.
        /// </summary>
        [HttpGet("urls")]
        [ProducesResponseType<AgentSpecsUrlsResponseModel>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUrls(Guid documentId, CancellationToken cancellationToken)
        {
            var denied = await AuthorizeDocumentAsync(documentId);
            if (denied is not null)
            {
                return denied;
            }

            var targets = await targetService.GetTargetsAsync(documentId, cancellationToken);
            if (targets is null)
            {
                return NotFound(new ProblemDetails { Title = "Document not found", Status = StatusCodes.Status404NotFound });
            }

            // Only the listed URL leaves the server. What is actually requested (it may carry a token) stays here.
            return Ok(new AgentSpecsUrlsResponseModel
            {
                Urls = targets.Select(target => new AgentSpecsUrlModel { Culture = target.Culture, Url = target.Url }).ToList(),
            });
        }

        /// <summary>
        /// Requests one of the document's front-end URLs with <c>Accept: text/markdown</c> and returns the
        /// parsed response. Only URLs belonging to the document are accepted.
        /// </summary>
        [HttpGet("markdown")]
        [ProducesResponseType<AgentSpecsResponseModel>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetMarkdown(Guid documentId, string url, CancellationToken cancellationToken)
        {
            var denied = await AuthorizeDocumentAsync(documentId);
            if (denied is not null)
            {
                return denied;
            }

            var (outcome, requestUrl, displayUrl) = await urlValidator.ValidateAsync(documentId, url, cancellationToken);
            switch (outcome)
            {
                case DocumentUrlValidator.Outcome.DocumentNotFound:
                    return NotFound(new ProblemDetails { Title = "Document not found", Status = StatusCodes.Status404NotFound });
                case DocumentUrlValidator.Outcome.UrlNotAllowed:
                    return BadRequest(new ProblemDetails
                    {
                        Title = "URL not allowed",
                        Detail = "The URL is not one of this document's URLs.",
                        Status = StatusCodes.Status400BadRequest,
                    });
            }

            try
            {
                return Ok(await fetcher.FetchAsync(requestUrl!, cancellationToken, displayUrl));
            }
            catch (AgentSpecsFetchException ex)
            {
                // The message is safe to show by construction: it never contains what URL processing added to the request.
                return BadGateway(ex.Message);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or IOException)
            {
                // The framework's messages can include the request URL, which may carry an access token, so they are
                // logged for diagnosis but never returned.
                logger.LogWarning(ex, "Requesting {Url} for document {DocumentId} failed", displayUrl, documentId);
                return BadGateway("The page could not be reached, or its response did not complete.");
            }
        }

        private ObjectResult BadGateway(string detail) =>
            StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "The page could not be requested",
                Detail = detail,
                Status = StatusCodes.Status502BadGateway,
            });

        // The section policy only says the editor can use the Content section. Check they may actually browse
        // this document (this also enforces their start nodes) before it is looked up or its URL is requested.
        private async Task<IActionResult?> AuthorizeDocumentAsync(Guid documentId)
        {
            var authorization = await authorizationService.AuthorizeAsync(
                User,
                ContentPermissionResource.WithKeys(ActionBrowse.ActionLetter, documentId),
                AuthorizationPolicies.ContentPermissionByResource);

            return authorization.Succeeded
                ? null
                : StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Access denied",
                    Detail = "You do not have access to this document.",
                    Status = StatusCodes.Status403Forbidden,
                });
        }
    }
}
