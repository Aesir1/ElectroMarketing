using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Domain.Products;
using ElectroMarketing.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ElectroMarketing.Api.Messaging;

/// <summary>Stores an audit row for every product.* domain event.</summary>
public sealed class ProductAuditConsumer(IConnection connection, IServiceScopeFactory scopes, ILogger<ProductAuditConsumer> logger)
    : HandlerConsumer<ProductChanged>(connection, scopes, logger)
{
    protected override string Queue => MessagingTopology.ProductAuditQueue;
}
