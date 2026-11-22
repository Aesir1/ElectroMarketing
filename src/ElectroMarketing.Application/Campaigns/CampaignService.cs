using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Exceptions;
using DomainValidationException = ElectroMarketing.Domain.Exceptions.ValidationException;

namespace ElectroMarketing.Application.Campaigns;

public sealed record CampaignDto(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    Guid? ContactListId,
    bool SendEmail,
    bool SendSms,
    bool SendPost,
    [property: StringLength(300)] string? EmailSubject,
    [property: StringLength(100_000)] string? EmailBody,
    [property: StringLength(1000)] string? SmsText,
    [property: StringLength(20_000)] string? LetterBody,
    [property: StringLength(200)] string? LandingTitle,
    [property: StringLength(20_000)] string? LandingBody);

public interface ICampaignStore
{
    Task<bool> ListExistsAsync(Guid listId, CancellationToken ct);

    /// <summary>Adds (does not save) one message per list member and selected channel; returns how many are Queued.</summary>
    Task<int> AddRecipientsAsync(Campaign campaign, CancellationToken ct);

    /// <summary>Tracked Queued messages of one channel, with their contact, oldest first.</summary>
    Task<IReadOnlyList<CampaignMessage>> NextQueuedAsync(Guid campaignId, Channel channel, int take, CancellationToken ct);

    /// <summary>Saves, then detaches messages and contacts; the campaign stays tracked.</summary>
    Task SaveAsync(CancellationToken ct);
}

public sealed class CampaignService(IRepository<Campaign, Guid> campaigns, ICampaignStore store, TimeProvider time)
    : CrudService<Campaign, Guid, CampaignDto, CampaignDto>(campaigns)
{
    public override async Task<Campaign> CreateAsync(CampaignDto dto, CancellationToken ct = default)
    {
        DtoValidator.Validate(dto);
        await RequireListAsync(dto.ContactListId, ct);
        return await base.CreateAsync(dto, ct);
    }

    /// <summary>Updates a draft; <paramref name="rowVersion"/> is the version the editor loaded, so a stale save throws ConcurrencyException.</summary>
    public async Task<Campaign> UpdateAsync(Guid id, CampaignDto dto, uint rowVersion, CancellationToken ct = default)
    {
        DtoValidator.Validate(dto);
        await RequireListAsync(dto.ContactListId, ct);
        var entity = await Repository.GetByIdAsync(id, ct) ?? throw NotFoundException.For<Campaign>(id);
        Apply(dto, entity);
        entity.RowVersion = rowVersion;
        return await Repository.UpdateAsync(entity, ct);
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await RequireDraftAsync(id, "Only draft campaigns can be deleted.", ct);
        await base.DeleteAsync(id, ct);
    }

    public async Task<Campaign> StartAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await RequireDraftAsync(id, "The campaign was already started.", ct);
        Validate(campaign);

        // The store adds the messages to the same scoped DbContext; UpdateAsync commits them with the status change.
        // ponytail: a token collision (≈ never at 62^7) fails the start with a unique-index ConflictError; just start again.
        if (await store.AddRecipientsAsync(campaign, ct) == 0)
        {
            throw new ConflictException("No contact in the list can be reached on the selected channels.");
        }

        campaign.Status = CampaignStatus.Sending;
        campaign.StartedAtUtc = time.GetUtcNow();
        return await Repository.UpdateAsync(campaign, ct);
    }

    protected override Campaign Map(CampaignDto dto)
    {
        var campaign = new Campaign { Name = dto.Name.Trim() };
        Copy(dto, campaign);
        return campaign;
    }

    protected override void Apply(CampaignDto dto, Campaign entity)
    {
        if (entity.Status != CampaignStatus.Draft)
        {
            throw new ConflictException("Only draft campaigns can be edited.");
        }

        entity.Name = dto.Name.Trim();
        Copy(dto, entity);
    }

    private static void Copy(CampaignDto dto, Campaign c)
    {
        (c.ContactListId, c.SendEmail, c.SendSms, c.SendPost) = (dto.ContactListId, dto.SendEmail, dto.SendSms, dto.SendPost);
        (c.EmailSubject, c.EmailBody, c.SmsText, c.LetterBody) = (dto.EmailSubject, dto.EmailBody, dto.SmsText, dto.LetterBody);
        (c.LandingTitle, c.LandingBody) = (dto.LandingTitle, dto.LandingBody);
    }

    private static void Validate(Campaign c)
    {
        var errors = new Dictionary<string, string[]>();
        if (c.ContactListId is null)
        {
            errors["contactListId"] = ["Pick a contact list."];
        }

        if (!c.Channels().Any())
        {
            errors["channels"] = ["Pick at least one channel."];
        }

        if (c.SendEmail && (string.IsNullOrWhiteSpace(c.EmailSubject) || string.IsNullOrWhiteSpace(c.EmailBody)))
        {
            errors["emailBody"] = ["E-mail needs a subject and a body."];
        }

        if (c.SendSms && string.IsNullOrWhiteSpace(c.SmsText))
        {
            errors["smsText"] = ["SMS needs a text."];
        }

        if (c.SendPost && string.IsNullOrWhiteSpace(c.LetterBody))
        {
            errors["letterBody"] = ["Post needs a letter text."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }

    private async Task RequireListAsync(Guid? listId, CancellationToken ct)
    {
        if (listId is { } id && !await store.ListExistsAsync(id, ct))
        {
            throw new DomainValidationException(new Dictionary<string, string[]> { ["contactListId"] = ["The contact list does not exist."] });
        }
    }

    private async Task<Campaign> RequireDraftAsync(Guid id, string message, CancellationToken ct)
    {
        var campaign = await Repository.GetByIdAsync(id, ct) ?? throw NotFoundException.For<Campaign>(id);
        return campaign.Status == CampaignStatus.Draft ? campaign : throw new ConflictException(message);
    }
}
