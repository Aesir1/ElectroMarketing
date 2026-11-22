using System.Globalization;
using System.Text;
using CsvHelper;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Imports;

public sealed record CsvPreview(string Delimiter, string Encoding, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, ColumnMapping SuggestedMapping);

/// <summary>
/// A contact CSV as written by Excel or other tools: UTF-8 (with or without BOM) or Windows-1252,
/// delimiter <c>;</c>, <c>,</c> or tab, first line is the header.
/// </summary>
public sealed class CsvContactFile
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
    private static readonly char[] LineEnds = ['\r', '\n'];
    private static readonly string[] Delimiters = [";", ",", "\t"];

    // Header aliases, normalized by Key(): lower-case letters and digits only.
    private static readonly (string Field, string[] Aliases)[] Aliases =
    [
        (nameof(ColumnMapping.Name), ["name", "fullname", "vollständigername", "kontakt", "contact", "firma", "company"]),
        (nameof(ColumnMapping.Email), ["email", "mail", "emailadresse", "emailaddress"]),
        (nameof(ColumnMapping.Phone), ["phone", "telefon", "tel", "telnr", "telefonnr", "telefonnummer", "mobil", "mobile", "handy", "phonenumber"]),
        (nameof(ColumnMapping.Street), ["street", "straße", "strasse", "address", "adresse", "anschrift"]),
        (nameof(ColumnMapping.PostalCode), ["zip", "zipcode", "postalcode", "postcode", "plz", "postleitzahl"]),
        (nameof(ColumnMapping.City), ["city", "ort", "stadt", "town", "wohnort"]),
        (nameof(ColumnMapping.Country), ["country", "land"]),
        (nameof(ColumnMapping.ObtainedOn), ["date", "datum", "obtained", "obtainedon", "erhaltenam", "created"]),
    ];

    private readonly string text;

    private CsvContactFile(string text, string encodingName)
    {
        this.text = text;
        EncodingName = encodingName;
        Delimiter = DetectDelimiter(text);
        using var reader = Reader();
        reader.ReadHeader();
        Headers = reader.Headers.ToArray();
    }

    public string Delimiter { get; }

    public string EncodingName { get; }

    public IReadOnlyList<string> Headers { get; }

    /// <summary>Reads the whole file; uploads are limited to 25 MB.</summary>
    public static async Task<CsvContactFile> ReadAsync(Stream content, CancellationToken ct)
    {
        // ponytail: whole file in memory (max 25 MB upload); stream it if the limit grows.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return new CsvContactFile(utf8.GetString(bytes).TrimStart('﻿'), "utf-8");
        }
        catch (DecoderFallbackException)
        {
            return new CsvContactFile(Windows1252.GetString(bytes), "windows-1252");
        }
    }

    public static ColumnMapping SuggestMapping(IReadOnlyList<string> headers)
    {
        string? Find(string field) => headers.FirstOrDefault(h => Aliases.First(a => a.Field == field).Aliases.Contains(Key(h)));
        return new ColumnMapping
        {
            Name = Find(nameof(ColumnMapping.Name)),
            Email = Find(nameof(ColumnMapping.Email)),
            Phone = Find(nameof(ColumnMapping.Phone)),
            Street = Find(nameof(ColumnMapping.Street)),
            PostalCode = Find(nameof(ColumnMapping.PostalCode)),
            City = Find(nameof(ColumnMapping.City)),
            Country = Find(nameof(ColumnMapping.Country)),
            ObtainedOn = Find(nameof(ColumnMapping.ObtainedOn)),
        };
    }

    public CsvPreview Preview(int rows = 10)
    {
        using var reader = Reader();
        reader.ReadHeader();
        var result = new List<IReadOnlyList<string>>();
        while (result.Count < rows && reader.Read())
        {
            result.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetString(i)).ToArray());
        }

        return new CsvPreview(Delimiter, EncodingName, Headers, result, SuggestMapping(Headers));
    }

    /// <summary>Data rows; <see cref="RawRow.RowNumber"/> is the CSV line (header = 1). Header names match case-insensitively.</summary>
    public IEnumerable<RawRow> Rows(ColumnMapping mapping)
    {
        int? Index(string? header) =>
            Headers.Select((h, i) => (Name: h, Index: (int?)i))
                .Where(x => string.Equals(x.Name, header, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Index)
                .FirstOrDefault();

        var (name, email, phone, street, postal, city, country, obtained) = (
            Index(mapping.Name), Index(mapping.Email), Index(mapping.Phone), Index(mapping.Street),
            Index(mapping.PostalCode), Index(mapping.City), Index(mapping.Country), Index(mapping.ObtainedOn));

        using var reader = Reader();
        reader.ReadHeader();
        while (reader.Read())
        {
            string? Field(int? index) => index is { } i && i < reader.FieldCount ? reader.GetString(i) : null;
            yield return new RawRow(
                (int)reader.LineNumber,
                Field(name),
                Field(email),
                Field(phone),
                Field(street),
                Field(postal),
                Field(city),
                Field(country),
                Field(obtained));
        }
    }

    private static string Key(string header) => new(header.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(LineEnds);
        var header = end < 0 ? text : text[..end];
        return Delimiters.MaxBy(d => header.Count(c => c == d[0]))!;
    }

    private CsvReader Reader() => CsvReader.Create(new StringReader(text), new CsvReaderOptions
    {
        Separator = Delimiter[0],
        TrimWhitespace = true,
        TrackLineNumbers = true,
        FormatProvider = CultureInfo.InvariantCulture,
    });
}
