using Amazon.SQS;
using Amazon.SQS.Model;
using FakeItEasy;
using IIIFAuth2.Management.Infrastructure.Aws;
using IIIFAuth2.Management.Infrastructure.Sqs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IIIFAuth2.Management.Tests.Infrastructure.Sqs;

public class SqsListenerTests
{
    private readonly IAmazonSQS client = A.Fake<IAmazonSQS>();
    private readonly IMessageHandler messageHandler = A.Fake<IMessageHandler>();
    private readonly SqsListener sut;

    public SqsListenerTests()
    {
        A.CallTo(() => client.GetQueueUrlAsync(A<string>._, A<CancellationToken>._))
            .Returns(new GetQueueUrlResponse { QueueUrl = "http://queue-url" });

        var serviceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => serviceProvider.GetService(typeof(IMessageHandler))).Returns(messageHandler);

        var scope = A.Fake<IServiceScope>();
        A.CallTo(() => scope.ServiceProvider).Returns(serviceProvider);

        var scopeFactory = A.Fake<IServiceScopeFactory>();
        A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);

        var options = Options.Create(new AWSSettings());

        sut = new SqsListener(client, options, scopeFactory,
            new SqsQueueUtilities(client, options, A.Fake<ILogger<SqsQueueUtilities>>()),
            A.Fake<ILogger<SqsListener>>());
    }

    [Fact]
    public async Task StartListenLoop_HandlesMessage_WhenMessageAttributesAndAttributesAreNull()
    {
        // Arrange
        // AWSSDK v4 no longer initialises collection properties by default, so a message with
        // no custom/system attributes comes back with both dictionaries null, not empty
        var message = new Message
        {
            Body = "{\"foo\":\"bar\"}",
            MessageId = "message-id",
            ReceiptHandle = "receipt-handle",
            MessageAttributes = null!,
            Attributes = null!,
        };

        A.CallTo(() => client.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse { Messages = [message] });

        // safety-net timeout: if constructing QueueMessage throws before the handler is reached,
        // the listen loop never sees a cancel request and spins forever on the same message - fail
        // fast here instead of hanging the test run
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        A.CallTo(() => messageHandler.HandleMessage(A<QueueMessage>._, A<CancellationToken>._))
            .Invokes(() => cts.Cancel())
            .Returns(true);

        // Act
        var act = () => sut.StartListenLoop<IMessageHandler>("queue-name", cts.Token);

        // Assert
        await act.Should().NotThrowAsync();

        A.CallTo(() => messageHandler.HandleMessage(
                A<QueueMessage>.That.Matches(m => m.MessageId == "message-id" && m.ApproximateReceiveCount == 0),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => client.DeleteMessageAsync(
                A<DeleteMessageRequest>.That.Matches(r => r.ReceiptHandle == "receipt-handle"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StartListenLoop_DoesNotDeleteMessage_IfHandlerReturnsFalse()
    {
        // Arrange
        var message = new Message
        {
            Body = "{\"foo\":\"bar\"}",
            MessageId = "message-id",
            ReceiptHandle = "receipt-handle",
        };

        A.CallTo(() => client.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse { Messages = [message] });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        A.CallTo(() => messageHandler.HandleMessage(A<QueueMessage>._, A<CancellationToken>._))
            .Invokes(() => cts.Cancel())
            .Returns(false);

        // Act
        await sut.StartListenLoop<IMessageHandler>("queue-name", cts.Token);

        // Assert
        A.CallTo(() => client.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
