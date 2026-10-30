using System.Text.Json;
using Crumpled.AgentSpecs.Services;

namespace Crumpled.AgentSpecs.Tests;

public class CloudflareMarkdownParserTests
{
    [Fact]
    public void Parse_RealCloudflareResponse_SplitsFrontmatterBodyAndJsonLd()
    {
        var raw = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cloudflare-markdown.md"));

        var result = CloudflareMarkdownParser.Parse(raw);

        Assert.True(result.Frontmatter.Fields.ContainsKey("title"));
        Assert.StartsWith("[Skip to Content]", result.Markdown);
        Assert.DoesNotContain("```json", result.Markdown);
        var block = Assert.Single(result.JsonLd);
        Assert.Null(block.Error);
        using var doc = JsonDocument.Parse(block.Raw);
        Assert.Equal("https://schema.org", doc.RootElement.GetProperty("@context").GetString());
    }

    [Fact]
    public void Parse_MultipleJsonValuesOnSeparateLines_ReturnsEachValue()
    {
        var raw = "# Hi\n\n```json\n{\"@type\":\"A\"}\n{\"@type\":\"B\"}\n```\n";

        var result = CloudflareMarkdownParser.Parse(raw);

        Assert.Equal(2, result.JsonLd.Count);
        Assert.Equal("# Hi", result.Markdown);
    }

    [Fact]
    public void Parse_NoFrontmatterOrJsonLd_ReturnsBodyOnly()
    {
        var result = CloudflareMarkdownParser.Parse("# Just markdown\r\n\r\ntext");

        Assert.Null(result.Frontmatter.Raw);
        Assert.Empty(result.Frontmatter.Fields);
        Assert.Empty(result.JsonLd);
        Assert.Equal("# Just markdown\n\ntext", result.Markdown);
    }

    [Fact]
    public void Parse_JsonFenceInTheMiddleOfTheBody_IsLeftInTheBody()
    {
        var raw = "Intro\n\n```json\n{\"a\":1}\n```\n\nMore text after the fence";

        var result = CloudflareMarkdownParser.Parse(raw);

        Assert.Empty(result.JsonLd);
        Assert.Contains("```json", result.Markdown);
    }

    [Fact]
    public void Parse_InvalidJson_KeepsRawBlockWithError()
    {
        var result = CloudflareMarkdownParser.Parse("Body\n\n```json\n{ not json\n```");

        var block = Assert.Single(result.JsonLd);
        Assert.NotNull(block.Error);
        Assert.Contains("not json", block.Raw);
    }

    [Fact]
    public void Parse_Frontmatter_ParsesQuotedAndPlainValues()
    {
        var result = CloudflareMarkdownParser.Parse("---\ntitle: \"A: title\"\ndescription: plain text\n---\n\nBody");

        Assert.Equal("A: title", result.Frontmatter.Fields["title"]);
        Assert.Equal("plain text", result.Frontmatter.Fields["description"]);
        Assert.Equal("Body", result.Markdown);
    }

    [Fact]
    public void Parse_EmptyFrontmatterBlock_ReturnsTheBody()
    {
        var result = CloudflareMarkdownParser.Parse("---\n---\n\nBody");

        Assert.Empty(result.Frontmatter.Fields);
        Assert.Equal("Body", result.Markdown);
    }

    [Fact]
    public void Parse_BodyStartingWithAnIndentedCodeBlock_KeepsTheIndentation()
    {
        var result = CloudflareMarkdownParser.Parse("---\ntitle: T\n---\n\n    first code line\n    second code line\n\n");

        Assert.Equal("    first code line\n    second code line", result.Markdown);
    }

    [Fact]
    public void Parse_BlankLinesAroundTheBody_AreTrimmed()
    {
        var result = CloudflareMarkdownParser.Parse("\n  \n# Title\n\ntext\n\n\n");

        Assert.Equal("# Title\n\ntext", result.Markdown);
    }
}
