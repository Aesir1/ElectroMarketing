using System.Globalization;
using ElectroMarketing.Application.Imports;

namespace ElectroMarketing.Application.Tests;

public class ContactNormalizerTests
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    [Theory]
    [InlineData(" Max@Example.DE ", "max@example.de")]
    [InlineData("Max <max@example.de>", null)]
    [InlineData("not-an-email", null)]
    [InlineData("", null)]
    public void Email_is_trimmed_lower_case_and_valid(string raw, string? expected) => ContactNormalizer.Email(raw).ShouldBe(expected);

    [Theory]
    [InlineData("0170 1234567", "DE", "+491701234567")]
    [InlineData("+49 (170) 123-4567", "DE", "+491701234567")]
    [InlineData("0049 170 1234567", "DE", "+491701234567")]
    [InlineData("123", "DE", null)]
    [InlineData("abc", "DE", null)]
    public void Phone_becomes_e164(string raw, string region, string? expected) => ContactNormalizer.PhoneE164(raw, region).ShouldBe(expected);

    [Theory]
    [InlineData("01.03.2026", 2026, 3, 1)]
    [InlineData("2026-03-01", 2026, 3, 1)]
    [InlineData("1.3.2026 14:30", 2026, 3, 1)]
    public void Date_parses_culture_and_iso(string raw, int y, int m, int d) => ContactNormalizer.Date(raw, De).ShouldBe(new DateOnly(y, m, d));

    [Fact]
    public void Row_without_email_and_phone_is_skipped()
    {
        var parsed = ContactNormalizer.Parse(new RawRow(2, "Max", "bad", "1", null, null, null, null, null), De);

        parsed.Row.ShouldBeNull();
        parsed.SkipReason.ShouldBe("No valid e-mail or phone number.");
        parsed.Warnings.ShouldBe(["Invalid e-mail 'bad' ignored.", "Invalid phone number '1' ignored."]);
    }

    [Fact]
    public void Too_long_value_skips_the_row()
    {
        var parsed = ContactNormalizer.Parse(new RawRow(2, new string('x', 201), "a@b.de", null, null, null, null, null, null), De);

        parsed.SkipReason.ShouldBe("Name is longer than 200 characters.");
    }

    [Fact]
    public void Bad_date_is_a_warning_not_a_skip()
    {
        var parsed = ContactNormalizer.Parse(new RawRow(2, " Max ", "a@b.de", null, " Hauptstr. 1 ", "12345", "Köln", null, "someday"), De);

        parsed.Row.ShouldBe(new ContactRow(2, "Max", "a@b.de", null, null, "Hauptstr. 1", "12345", "Köln", null, null));
        parsed.Warnings.ShouldBe(["Invalid date 'someday' ignored."]);
    }

    [Fact]
    public void Region_comes_from_culture() => ContactNormalizer.Region(CultureInfo.GetCultureInfo("de-AT")).ShouldBe("AT");
}
