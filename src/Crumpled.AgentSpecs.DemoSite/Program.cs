
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();


// Test-only stand-in for Cloudflare Markdown for Agents.
app.UseMiddleware<Crumpled.AgentSpecs.DemoSite.MarkdownForAgents.MarkdownForAgentsMiddleware>();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
