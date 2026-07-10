namespace VirusTotalOneClick.Core.Tests;

public sealed class RecordingHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;

    public RecordingHttpHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string?> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        return _respond(request, Requests.Count - 1);
    }
}
