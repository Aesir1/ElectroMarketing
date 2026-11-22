// tests/ElectroMarketing.Application.Tests/CsvContactFileTests.cs
using System.Text;
using ElectroMarketing.Application.Imports;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Tests;

public class CsvContactFileTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<CsvContactFile> Read(byte[] bytes) => CsvContactFile.ReadAsync(new MemoryStream(bytes), Ct);

    [Fact]
    public async Task German_excel_file_with_windows_1252_and_semicolons()
    {
        var win1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        var file = await Read(win1252.GetBytes("Name;E-Mail;Telefon;Straße;PLZ;Ort;Datum\r\nJörg Müller;j@x.de;0170 1234567;Hauptstr. 1;50667;Köln;01.03.2026\r\n"));

        file.Delimiter.ShouldBe(";");
        file.EncodingName.ShouldBe("windows-1252");
        var mapping = CsvContactFile.SuggestMapping(file.Headers);
        (mapping.Name, mapping.Email, mapping.Phone, mapping.Street, mapping.PostalCode, mapping.City, mapping.ObtainedOn)
            .ShouldBe(("Name", "E-Mail", "Telefon", "Straße", "PLZ", "Ort", "Datum"));
        file.Rows(mapping).Single().ShouldBe(new RawRow(2, "Jörg Müller", "j@x.de", "0170 1234567", "Hauptstr. 1", "50667", "Köln", null, "01.03.2026"));
    }

    [Fact]
    public async Task Utf8_with_bom_commas_and_quoted_fields()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("email,name,city\n\"a@b.de\",\"Doe, Jane\",Zürich\n")).ToArray();
        var file = await Read(bytes);

        (file.Delimiter, file.EncodingName).ShouldBe((",", "utf-8"));
        file.Headers.ShouldBe(["email", "name", "city"]);
        var row = file.Rows(new ColumnMapping { Email = "EMAIL", Name = "name", City = "city" }).Single();
        (row.Email, row.Name, row.City).ShouldBe(("a@b.de", "Doe, Jane", "Zürich"));
    }

    [Fact]
    public async Task Short_rows_and_unmapped_columns_give_nulls()
    {
        var file = await Read(Encoding.UTF8.GetBytes("Email;Name;City\na@b.de\n"));

        var row = file.Rows(new ColumnMapping { Email = "Email", Name = "Name", Country = "Missing" }).Single();
        (row.Email, row.Name, row.Country).ShouldBe(("a@b.de", null, null));
    }

    [Fact]
    public async Task Preview_returns_first_rows_and_suggestion()
    {
        var csv = "Mail;Handy\n" + string.Join('\n', Enumerable.Range(1, 20).Select(i => $"u{i}@x.de;0170{i:D7}"));
        var preview = (await Read(Encoding.UTF8.GetBytes(csv))).Preview();

        preview.Rows.Count.ShouldBe(10);
        preview.Rows[0].ShouldBe(["u1@x.de", "01700000001"]);
        (preview.SuggestedMapping.Email, preview.SuggestedMapping.Phone).ShouldBe(("Mail", "Handy"));
    }
}
