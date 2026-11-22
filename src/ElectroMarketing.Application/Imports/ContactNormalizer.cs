using System.Globalization;
using System.Net.Mail;
using ElectroMarketing.Domain.Contacts;
using PhoneNumbers;

namespace ElectroMarketing.Application.Imports;

/// <summary>One CSV row as text, by contact field (after column mapping).</summary>
public sealed record RawRow(int RowNumber, string? Name, string? Email, string? Phone, string? Street, string? PostalCode, string? City, string? Country, string? ObtainedOn);

/// <summary>A validated row: e-mail normalized, phone in E.164, at least one of both present.</summary>
public sealed record ContactRow(int RowNumber, string? Name, string? Email, string? Phone, string? PhoneE164, string? Street, string? PostalCode, string? City, string? Country, DateOnly? ObtainedOn);

public sealed record ParsedRow(ContactRow? Row, string? SkipReason, IReadOnlyList<string> Warnings);

public static class ContactNormalizer
{
    private static readonly PhoneNumberUtil Phones = PhoneNumberUtil.GetInstance();

    public static ParsedRow Parse(RawRow raw, CultureInfo culture)
    {
        var warnings = new List<string>();
        var name = Clean(raw.Name);
        var email = Email(raw.Email);
        if (email is null && Clean(raw.Email) is { } badEmail)
        {
            warnings.Add($"Invalid e-mail '{badEmail}' ignored.");
        }

        var phone = Clean(raw.Phone);
        var e164 = PhoneE164(phone, Region(culture));
        if (e164 is null && phone is not null)
        {
            warnings.Add($"Invalid phone number '{phone}' ignored.");
            phone = null;
        }

        var obtained = Date(raw.ObtainedOn, culture);
        if (obtained is null && Clean(raw.ObtainedOn) is { } badDate)
        {
            warnings.Add($"Invalid date '{badDate}' ignored.");
        }

        var row = new ContactRow(raw.RowNumber, name, email, phone, e164, Clean(raw.Street), Clean(raw.PostalCode), Clean(raw.City), Clean(raw.Country), obtained);
        var skip = TooLong(row) ?? (email is null && e164 is null ? "No valid e-mail or phone number." : null);
        return new ParsedRow(skip is null ? row : null, skip, warnings);
    }

    public static string? Email(string? raw)
    {
        var value = Clean(raw)?.ToLowerInvariant();
        return value is not null && value.Length <= Contact.EmailMax && MailAddress.TryCreate(value, out var address) && address.Address == value
            ? value
            : null;
    }

    public static string? PhoneE164(string? raw, string region)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var number = Phones.Parse(raw, region);
            return Phones.IsValidNumber(number) ? Phones.Format(number, PhoneNumberFormat.E164) : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    public static DateOnly? Date(string? raw, CultureInfo culture)
    {
        var value = Clean(raw);
        if (value is null)
        {
            return null;
        }

        return DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out var date)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)
            ? DateOnly.FromDateTime(date)
            : null;
    }

    public static string Region(CultureInfo culture) => new RegionInfo(culture.Name).TwoLetterISORegionName;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TooLong(ContactRow row) =>
        Check(nameof(Contact.Name), row.Name, Contact.NameMax)
        ?? Check(nameof(Contact.Phone), row.Phone, Contact.PhoneMax)
        ?? Check(nameof(Contact.Street), row.Street, Contact.StreetMax)
        ?? Check(nameof(Contact.PostalCode), row.PostalCode, Contact.PostalCodeMax)
        ?? Check(nameof(Contact.City), row.City, Contact.CityMax)
        ?? Check(nameof(Contact.Country), row.Country, Contact.CountryMax);

    private static string? Check(string field, string? value, int max) =>
        value?.Length > max ? $"{field} is longer than {max} characters." : null;
}
