using System.Diagnostics;
using System.Net.Http.Headers;

namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// Records every request/response made to iiif-auth-v2 in <see cref="CallLog"/>
/// </summary>
public class CallLoggingHandler(CallLog callLog, ILogger<CallLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var id = callLog.NextId();
        var timestamp = DateTime.UtcNow;
        var url = request.RequestUri?.ToString() ?? "";
        var requestHeaders = ToDictionary(request.Headers);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var responseHeaders = ToDictionary(response.Headers.Concat(response.Content.Headers));
            callLog.Add(new CallLogEntry(id, timestamp, request.Method.Method, url, requestHeaders,
                (int)response.StatusCode, responseHeaders, body, stopwatch.ElapsedMilliseconds, null));
            logger.LogInformation("{Method} {Url} returned {StatusCode} in {Elapsed}ms", request.Method, url,
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            callLog.Add(new CallLogEntry(id, timestamp, request.Method.Method, url, requestHeaders, null, new(),
                null, stopwatch.ElapsedMilliseconds, ex.Message));
            logger.LogWarning(ex, "{Method} {Url} failed", request.Method, url);
            throw;
        }
    }

    private static Dictionary<string, string> ToDictionary(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        => headers
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.SelectMany(h => h.Value)));

    private static Dictionary<string, string> ToDictionary(HttpHeaders headers) => ToDictionary(headers.AsEnumerable());
}
