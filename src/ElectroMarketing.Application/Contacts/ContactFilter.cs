namespace ElectroMarketing.Application.Contacts;

/// <summary>
/// <see cref="Search"/>: whitespace-separated terms; every term must appear in some field (substring, case-insensitive).
/// Category and batch filters match contacts touched by any import of those.
/// </summary>
public sealed record ContactFilter(
    string? Search = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    IReadOnlyList<Guid>? BatchIds = null,
    DateOnly? ObtainedFrom = null,
    DateOnly? ObtainedTo = null);
