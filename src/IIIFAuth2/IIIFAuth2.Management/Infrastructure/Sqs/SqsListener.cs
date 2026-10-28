using System.Text.Json.Nodes;
using Amazon.SQS;
using Amazon.SQS.Model;
using IIIFAuth2.Management.Infrastructure.Aws;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.Management.Infrastructure.Sqs;

/// <summary>
/// Subscribes to SQS, using long polling to receive messages
/// </summary>
public class SqsListener(
    IAmazonSQS client,
    IOptions<AWSSettings> options,
    IServiceScopeFactory serviceScopeFactory,
    SqsQueueUtilities queueUtilities,
    ILogger<SqsListener> logger)
{
    private readonly AWSSettings options = options.Value;

    /// <summary>
    /// Start listening to specified queue.
    /// On receipt a handler of type {T} is created DI container and used to handle request.
    /// On successful handle message is deleted from queue.
    /// </summary>
    /// <param name="queueName">Queue to monitor</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <typeparam name="T">Type of message handler</typeparam>
    public async Task StartListenLoop<T>(string queueName, CancellationToken cancellationToken)
        where T : IMessageHandler
    {
        var queueUrl = await queueUtilities.GetQueueUrl(queueName, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            ReceiveMessageResponse? response = null;
            var messageCount = 0;
            try
            {
                response = await GetMessagesFromQueue(queueUrl, cancellationToken);
                messageCount = response.Messages?.Count ?? 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error receiving messages on queue {Queue}", queueUrl);
            }

            if (messageCount == 0) continue;

            try
            {
                foreach (var message in response!.Messages!)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var processed = await HandleMessage<T>(queueUrl, message, cancellationToken);

                    if (processed)
                    {
                        await DeleteMessage(queueUrl, message, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in listen loop for queue {Queue}", queueUrl);
            }
        }
    }

    private Task<ReceiveMessageResponse> GetMessagesFromQueue(string queueUrl, CancellationToken cancellationToken)
        => client.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            WaitTimeSeconds = options.SQS.WaitTimeSecs,
            MaxNumberOfMessages = options.SQS.MaxNumberOfMessages,
            MessageAttributeNames = [".*"], // Include all custom message attributes
            MessageSystemAttributeNames = [QueueAttributes.ApproximateReceiveCount], // Include the number of times received
        }, cancellationToken);

    private async Task<bool> HandleMessage<T>(string queueUrl, Message message, CancellationToken cancellationToken)
        where T : IMessageHandler
    {
        try
        {
            var queueMessage = new QueueMessage(GetJsonPayload(message), message.MessageAttributes ?? [],
                message.Attributes ?? [], message.MessageId);

            // create a new scope to avoid issues with Scoped dependencies
            using var listenerScope = serviceScopeFactory.CreateScope();
            var handler = listenerScope.ServiceProvider.GetRequiredService<T>();

            var processed = await handler.HandleMessage(queueMessage, cancellationToken);
            return processed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message {MessageId} from queue {Queue}", message.MessageId,
                queueUrl);
            return false;
        }
    }

    private static string GetJsonPayload(Message message)
    {
        var messageBody = JsonNode.Parse(message.Body)!.AsObject();
        const string messageKey = "Message";
        if (messageBody.ContainsKey("TopicArn") && messageBody.ContainsKey(messageKey))
        {
            // From SNS without Raw Message Delivery
            var value = messageBody[messageKey]!.GetValue<string>();
            return value;
        }

        // From SQS or SNS with Raw Message Delivery
        return messageBody.ToString();
    }

    private Task DeleteMessage(string queueUrl, Message message, CancellationToken cancellationToken)
        => client.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = queueUrl,
            ReceiptHandle = message.ReceiptHandle
        }, cancellationToken);
}
