using Amazon.SQS.Model;

namespace IIIFAuth2.Management.Infrastructure.Sqs;

/// <summary>
/// Generic representation of message pulled from queue.
/// </summary>
public class QueueMessage
{
    public QueueMessage(string body, Dictionary<string, string> attributes, string messageId)
    {
        Body = body;
        Attributes = attributes;
        MessageId = messageId;
    }

    /// <remarks>
    /// This only maps StringValues from attributes, it won't handle other types (BinaryValue or MemoryStream)
    /// </remarks>
    public QueueMessage(string body, Dictionary<string, MessageAttributeValue> attributes,
        Dictionary<string, string> messageAttributes, string messageId)
    {
        Body = body;
        Attributes = attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.StringValue);
        MessageId = messageId;
        ApproximateReceiveCount = messageAttributes.TryGetValue(QueueAttributes.ApproximateReceiveCount, out var count)
            ? int.Parse(count)
            : 0;
    }

    public string Body { get; }

    public Dictionary<string, string> Attributes { get; }

    public string MessageId { get; }

    public int ApproximateReceiveCount { get; }
}

/// <summary>
/// Known SQS queue attributes
/// </summary>
internal static class QueueAttributes
{
    public const string ApproximateReceiveCount = "ApproximateReceiveCount";
}
