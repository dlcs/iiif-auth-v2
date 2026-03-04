using System.Net;
using System.Text;

namespace IIIFAuth2.API.Tests.TestingInfrastructure;

/// <summary>
/// Controllable HttpMessageHandler for unit testing HttpClient.
/// </summary>
public class ControllableHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responses = new();
    private HttpResponseMessage? fallbackResponse;
    public List<string> CallsMade { get; } = new();

    public Action<HttpRequestMessage>? Callback { get; private set; }

    /// <summary>
    /// Helper method to generate an HttpResponseMessage object
    /// </summary>
    public HttpResponseMessage GetResponseMessage(string content, HttpStatusCode httpStatusCode)
    {
        var httpContent = new StringContent(content, Encoding.UTF8, "application/json");

        var response = new HttpResponseMessage
        {
            StatusCode = httpStatusCode,
            Content = httpContent,
        };
        SetResponse(response);
        return response;
    }
        
    /// <summary>
    /// Enqueue a pre-canned response; will be used in order of registration.
    /// </summary>
    public void SetResponse(HttpResponseMessage response)
    {
        responses.Enqueue(response);
        fallbackResponse ??= CloneResponse(response);
    }

    /// <summary>
    /// Register a callback when SendAsync called. Useful for verifying headers etc.
    /// </summary>
    /// <param name="callback">Function to call when SendAsync request made.</param>
    public void RegisterCallback(Action<HttpRequestMessage> callback) => Callback = callback;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallsMade.Add(request.RequestUri.ToString());
        Callback?.Invoke(request);

        if (responses.Count > 0)
        {
            var next = responses.Dequeue();
            return Task.FromResult(CloneResponse(next) ?? new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        var response = CloneResponse(fallbackResponse);
        return Task.FromResult(response ?? new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private static HttpResponseMessage? CloneResponse(HttpResponseMessage? source)
    {
        if (source == null) return null;

        var clone = new HttpResponseMessage(source.StatusCode)
        {
            ReasonPhrase = source.ReasonPhrase,
            Version = source.Version
        };

        if (source.Content != null)
        {
            var contentString = source.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            clone.Content = new StringContent(contentString, Encoding.UTF8, source.Content.Headers.ContentType?.MediaType);
            foreach (var header in source.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
