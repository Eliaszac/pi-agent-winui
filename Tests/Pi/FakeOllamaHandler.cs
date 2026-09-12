namespace PiAgentGui.Tests.Pi;

internal sealed class FakeOllamaHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.Method + " " + request.RequestUri!.AbsolutePath);
        return Task.FromResult(respond(request));
    }
}
