namespace Crumpled.AgentSpecs.Services
{
    /// <summary>
    /// A page request failed in a way worth telling the editor about. The message is safe to show: it never
    /// contains anything URL processing added to the request, such as an access token.
    /// </summary>
    public sealed class AgentSpecsFetchException(string message) : Exception(message)
    {
    }
}
