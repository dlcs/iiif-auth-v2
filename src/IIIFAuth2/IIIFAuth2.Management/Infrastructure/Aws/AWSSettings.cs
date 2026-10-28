namespace IIIFAuth2.Management.Infrastructure.Aws;

public class AWSSettings
{
    public const string SettingsName = "AWS";

    /// <summary>
    /// If true, service will use LocalStack and custom ServiceUrl
    /// </summary>
    public bool UseLocalStack { get; set; } = false;

    /// <summary>
    /// SQS Settings
    /// </summary>
    public SQSSettings SQS { get; set; } = new();
}

public class SQSSettings
{
    /// <summary>
    /// Name of queue that will receive notifications when a new customer is created
    /// </summary>
    public string? CustomerCreatedQueueName { get; set; }

    /// <summary>
    /// The duration (in seconds) for which the call waits for a message to arrive in the queue before returning
    /// </summary>
    public int WaitTimeSecs { get; set; } = 20;

    /// <summary>
    /// The maximum number of messages to fetch from SQS in single request (valid 1-10)
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Service root for SQS. Ignored if not running LocalStack
    /// </summary>
    public string ServiceUrl { get; set; } = "http://localhost:4566/";
}
