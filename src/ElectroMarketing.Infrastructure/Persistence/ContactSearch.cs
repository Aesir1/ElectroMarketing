using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

/// <summary>LIKE on the lower-case <see cref="Contact.SearchText"/>; the pg_trgm GIN index serves terms of 3+ characters.</summary>
public static class ContactSearch
{
    private const int MaxTerms = 8;

    public static IQueryable<Contact> Apply(IQueryable<Contact> query, ContactFilter filter)
    {
        foreach (var term in Terms(filter.Search))
        {
            var pattern = "%" + Escape(term) + "%";
            query = query.Where(c => EF.Functions.Like(c.SearchText, pattern, "\\"));
        }

        if (filter.CategoryIds is { Count: > 0 } categories)
        {
            query = query.Where(c => c.Imports.Any(i => categories.Contains(i.ImportBatch!.CategoryId)));
        }

        if (filter.BatchIds is { Count: > 0 } batches)
        {
            query = query.Where(c => c.Imports.Any(i => batches.Contains(i.ImportBatchId)));
        }

        if (filter.ObtainedFrom is { } from)
        {
            query = query.Where(c => c.ObtainedOn >= from);
        }

        if (filter.ObtainedTo is { } to)
        {
            query = query.Where(c => c.ObtainedOn <= to);
        }

        return query;
    }

    /// <summary>Lower-case distinct terms; a phone-like term ("0170-1234", "(030)") keeps only its digits to match the stored phone digits.</summary>
    public static IReadOnlyList<string> Terms(string? search) =>
        (search ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Select(t => IsPhoneLike(t) ? new string(t.Where(char.IsAsciiDigit).ToArray()) : t)
            .Where(t => t.Length > 0)
            .Distinct()
            .Take(MaxTerms)
            .ToList();

    private static bool IsPhoneLike(string term) =>
        term.Any(char.IsAsciiDigit) && term.All(ch => char.IsAsciiDigit(ch) || ch is '+' or '-' or '/' or '(' or ')');

    private static string Escape(string term) => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
