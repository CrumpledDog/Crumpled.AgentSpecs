using Crumpled.AgentSpecs.Controllers;

namespace Crumpled.AgentSpecs.Tests;

public class CrumpledAgentSpecsApiControllerTests
{
    [Fact]
    public void Ping_ReturnsPong()
    {
        var sut = new CrumpledAgentSpecsApiController();

        var result = sut.Ping();

        Assert.Equal("Pong", result);
    }
}
