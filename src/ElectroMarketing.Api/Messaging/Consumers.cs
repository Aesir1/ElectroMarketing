using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace ElectroMarketing.Api.Messaging;

/// <summary>Imports each new CSV batch (bound to <c>import.#</c>; only <c>import.created</c> does work).</summary>
public sealed class ContactImportConsumer(IConnection connection, IServiceScopeFactory scopes, ILogger<ContactImportConsumer> logger)
    : HandlerConsumer<ImportBatchChanged>(connection, scopes, logger)
{
    protected override string Queue => MessagingTopology.ContactImportQueue;
}
