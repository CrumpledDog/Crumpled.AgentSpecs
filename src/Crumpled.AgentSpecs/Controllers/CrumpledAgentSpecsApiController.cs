using Asp.Versioning;
using Crumpled.AgentSpecs.Models;
using Crumpled.AgentSpecs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crumpled.AgentSpecs.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Crumpled.AgentSpecs")]
    public class CrumpledAgentSpecsApiController(
        DocumentUrlValidator urlValidator,
        AgentMarkdownFetcher fetcher) : CrumpledAgentSpecsApiControllerBase
    {
        /// <summary>
        /// Requests one of the document's front-end URLs with <c>Accept: text/markdown</c> and returns the
        /// parsed response. Only URLs belonging to the document are accepted.
        /// </summary>
        [HttpGet("markdown")]
        [ProducesResponseType<AgentSpecsResponseModel>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetMarkdown(Guid documentId, string url, CancellationToken cancellationToken)
        {
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
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
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
