using System.Collections.Concurrent;

namespace IIIFAuth2.TestUI.Infrastructure;

/// <summary>
/// Details of a single downstream request made to iiif-auth-v2
/// </summary>
public record CallLogEntry(
    long Id,
    DateTime Timestamp,
    string Method,
    string Url,
    Dictionary<string, string> RequestHeaders,
    int? StatusCode,
    Dictionary<string, string> ResponseHeaders,
    string? ResponseBody,
    long DurationMs,
    string? Error);

/// <summary>
/// Bounded, in-memory log of all downstream requests made to iiif-auth-v2
/// </summary>
public class CallLog
{
    private const int MaxEntries = 200;
    private readonly ConcurrentQueue<CallLogEntry> entries = new();
    private long lastId;

    public long LatestId => Interlocked.Read(ref lastId);

    public long NextId() => Interlocked.Increment(ref lastId);

    public void Add(CallLogEntry entry)
    {
        entries.Enqueue(entry);
        while (entries.Count > MaxEntries) entries.TryDequeue(out _);
    }

    /// <summary>
    /// Get all entries with an Id greater than specified value, oldest first
    /// </summary>
    public IReadOnlyList<CallLogEntry> GetAfter(long id) => entries.Where(e => e.Id > id).OrderBy(e => e.Id).ToList();

    public void Clear() => entries.Clear();
}
