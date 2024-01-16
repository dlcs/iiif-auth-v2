using System.Net;
using System.Text;

namespace IIIFAuth2.API.Tests.TestingInfrastructure;

/// <summary>
/// Controllable HttpMessageHandler for unit testing HttpClient.
/// </summary>
public class ControllableHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage response;
    public List<string> CallsMade { get; } = new();

    public Action<HttpRequestMessage> Callback { get; private set; }

    /// <summary>
    /// Helper method to generate an HttpResponseMessage object
    /// </summary>
    public HttpResponseMessage GetResponseMessage(string content, HttpStatusCode httpStatusCode)
    {
        var httpContent = new StringContent(content, Encoding.UTF8, "application/json");

        response = new HttpResponseMessage
        {
            StatusCode = httpStatusCode,
            Content = httpContent,
        };
        return response;
    }
        
    /// <summary>
    /// Set a pre-canned response 
    /// </summary>
    public void SetResponse(HttpResponseMessage response) => this.response = response;

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

        return Task.FromResult(response);
    }
}