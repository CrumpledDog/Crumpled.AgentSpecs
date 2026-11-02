using Crumpled.AgentSpecs.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Umbraco.Cms.Core.Hosting;

namespace Crumpled.AgentSpecs.Tests;

public class UmbracoCloudDetectorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("agentspecs-cloud").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    internal static UmbracoCloudDetector CreateDetector(bool isUmbracoCloud) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Umbraco:Cloud:IsRunningOnCloud"] = isUmbracoCloud ? "true" : "false" })
                .Build(),
            HostingAt(Path.GetTempPath()));

    private static IHostingEnvironment HostingAt(string path)
    {
        var hosting = Substitute.For<IHostingEnvironment>();
        hosting.ApplicationPhysicalPath.Returns(path);
        return hosting;
    }

    private UmbracoCloudDetector Create(Dictionary<string, string?>? settings = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build(), HostingAt(_root));

    [Fact]
    public void RunningOnCloudFlag_IsUmbracoCloud() =>
        Assert.True(Create(new() { ["Umbraco:Cloud:IsRunningOnCloud"] = "true" }).IsUmbracoCloud());

    [Fact]
    public void ConfiguredEnvironmentId_IsUmbracoCloud() =>
        Assert.True(Create(new() { ["Umbraco:Cloud:Identity:EnvironmentId"] = "00000000-0000-0000-0000-000000000001" }).IsUmbracoCloud());

    [Fact]
    public void CloudFileInTheApplicationFolder_IsUmbracoCloud()
    {
        File.WriteAllText(Path.Combine(_root, "umbraco-cloud.json"), "{}");

        Assert.True(Create().IsUmbracoCloud());
    }

    [Fact]
    public void NoSignOfUmbracoCloud_IsNot() =>
        Assert.False(Create(new() { ["Umbraco:Cloud:IsRunningOnCloud"] = "false", ["Umbraco:Cloud:Identity:EnvironmentId"] = "" }).IsUmbracoCloud());
}
