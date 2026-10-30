using Asp.Versioning;
using Crumpled.AgentSpecs.Models;
using Crumpled.AgentSpecs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Web.Common.Authorization;

namespace Crumpled.AgentSpecs.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Crumpled.AgentSpecs")]
    public class CrumpledAgentSpecsApiController(
        DocumentUrlValidator urlValidator,
        AgentMarkdownFetcher fetcher,
        IAuthorizationService authorizationService) : CrumpledAgentSpecsApiControllerBase
    {
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
            // The section policy only says the editor can use the Content section. Check they may actually browse
            // this document (this also enforces their start nodes) before it is looked up or its URL is requested.
            var authorization = await authorizationService.AuthorizeAsync(
                User,
                ContentPermissionResource.WithKeys(ActionBrowse.ActionLetter, documentId),
                AuthorizationPolicies.ContentPermissionByResource);
            if (!authorization.Succeeded)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Access denied",
                    Detail = "You do not have access to this document.",
                    Status = StatusCodes.Status403Forbidden,
                });
            }

            var (outcome, validUrl) = await urlValidator.ValidateAsync(documentId, url);
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
                return Ok(await fetcher.FetchAsync(validUrl!, cancellationToken));
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or IOException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Title = "The page could not be requested",
                    Detail = ex.Message,
                    Status = StatusCodes.Status502BadGateway,
                });
            }
        }
    }
}
