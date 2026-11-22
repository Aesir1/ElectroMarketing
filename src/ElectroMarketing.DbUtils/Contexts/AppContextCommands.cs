using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectroMarketing.DbUtils.Contexts;

public sealed class AppContextCommands(AppDbContext db, IObjectStorage storage, TimeProvider time, ILogger<AppContextCommands> logger)
    : ContextCommands<AppDbContext>(db, logger)
{
    public override string Name => "App";

    public override async Task<int> MaintainAsync(MaintainOptions options, CancellationToken ct)
    {
        var affected = await base.MaintainAsync(options, ct);
        if (options.PurgeSoftDeletedOlderThan is { } age)
        {
            var cutoff = time.GetUtcNow() - age;
            var documents = await Db.Documents.IgnoreQueryFilters().Where(d => d.IsDeleted && d.DeletedAtUtc < cutoff).ExecuteDeleteAsync(ct);
            Logger.LogInformation("Purged {Documents} documents deleted before {Cutoff}", documents, cutoff);
            affected += documents;
        }

        if (options.OrphanObjects)
        {
            affected += await OrphanObjectsAsync(options.DeleteOrphans, ct);
        }

        return affected;
    }

    /// <summary>Objects in the documents bucket that no live Document row references.</summary>
    public async Task<int> OrphanObjectsAsync(bool delete, CancellationToken ct)
    {
        var objects = await storage.ListAsync(Document.Bucket, ct: ct);
        var live = (await Db.Documents.Select(d => new { d.OwnerId, d.Id }).ToListAsync(ct))
            .Select(d => $"{d.OwnerId}/{d.Id}")
            .ToHashSet(StringComparer.Ordinal);
        var orphans = objects.Where(o => !live.Contains(o.Key)).ToList();
        foreach (var orphan in orphans)
        {
            Logger.LogInformation("Orphan object {Key} ({Bytes} bytes){Action}", orphan.Key, orphan.SizeBytes, delete ? " deleted" : string.Empty);
            if (delete)
            {
                await storage.DeleteAsync(Document.Bucket, orphan.Key, ct);
            }
        }

        return orphans.Count;
    }
}
