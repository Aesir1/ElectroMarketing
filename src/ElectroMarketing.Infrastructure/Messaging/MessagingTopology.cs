using RabbitMQ.Client;

namespace ElectroMarketing.Infrastructure.Messaging;

/// <summary>MQ-1: every exchange, queue and binding, declared idempotently in code.</summary>
public static class MessagingTopology
{
    public const string DomainEvents = "domain-events";
    public const string DomainEventsDlx = "domain-events.dlx";

    public const string ContactImportQueue = "contact-import";

    public static string DeadLetterQueue(string queue) => queue + ".dlq";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        // Domain events: topic exchange; the DLX is direct so each queue's dead letters land only in its own DLQ.
        await channel.ExchangeDeclareAsync(DomainEvents, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DomainEventsDlx, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await DeclareQueueAsync(channel, ContactImportQueue, DomainEvents, "import.created", DomainEventsDlx, ct);
    }

    /// <summary>Durable queue with its own DLQ, dead-lettered through <paramref name="deadLetterExchange"/> by queue name.</summary>
    public static async Task DeclareQueueAsync(IChannel channel, string queue, string? exchange, string? routingKey, string? deadLetterExchange, CancellationToken ct)
    {
        Dictionary<string, object?>? arguments = null;
        if (deadLetterExchange is not null)
        {
            await channel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
            await channel.QueueDeclareAsync(DeadLetterQueue(queue), durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await channel.QueueBindAsync(DeadLetterQueue(queue), deadLetterExchange, queue, cancellationToken: ct);
            arguments = new() { ["x-dead-letter-exchange"] = deadLetterExchange, ["x-dead-letter-routing-key"] = queue };
        }

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: ct);
        if (exchange is not null)
        {
            await channel.QueueBindAsync(queue, exchange, routingKey ?? "#", cancellationToken: ct);
        }
    }
}
