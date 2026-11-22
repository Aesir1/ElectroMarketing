using ElectroMarketing.Application.Campaigns;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ElectroMarketing.Infrastructure.Campaigns;

/// <summary>
/// DIN 5008 (form B) letters: address window 45 mm from the top, sender line above it, body, then a QR code of the
/// short link with the link printed below for people who type it.
/// </summary>
public sealed class QuestLetterRenderer : ILetterRenderer
{
    private readonly CampaignOptions options;

    public QuestLetterRenderer(CampaignOptions options)
    {
        // Community license: free below USD 1M annual revenue (see the spec).
        QuestPDF.Settings.License = LicenseType.Community;
        this.options = options;
    }

    public byte[] Render(IReadOnlyList<Letter> letters) =>
        QuestPDF.Fluent.Document.Create(doc =>
        {
            foreach (var letter in letters)
            {
                doc.Page(page => Compose(page, letter));
            }
        }).GeneratePdf();

    private void Compose(PageDescriptor page, Letter letter)
    {
        page.Size(PageSizes.A4);
        page.MarginTop(45, Unit.Millimetre);
        page.MarginLeft(20, Unit.Millimetre);
        page.MarginRight(20, Unit.Millimetre);
        page.MarginBottom(20, Unit.Millimetre);
        page.DefaultTextStyle(t => t.FontSize(11));
        page.Content().Column(col =>
        {
            col.Item().Width(85, Unit.Millimetre).Height(45, Unit.Millimetre).Column(address =>
            {
                address.Item().Text(options.SenderLine).FontSize(7).Underline();
                address.Item().PaddingTop(3, Unit.Millimetre).Text(letter.Name ?? string.Empty);
                address.Item().Text(letter.Street ?? string.Empty);
                address.Item().Text($"{letter.PostalCode} {letter.City}".Trim());
                if (!string.IsNullOrEmpty(letter.Country))
                {
                    address.Item().Text(letter.Country);
                }
            });
            col.Item().PaddingTop(20, Unit.Millimetre).Text(letter.Body);
            col.Item().PaddingTop(12, Unit.Millimetre).AlignCenter().Width(35, Unit.Millimetre).Image(Qr(letter.Url));
            col.Item().PaddingTop(2, Unit.Millimetre).AlignCenter().Text(letter.Url).FontSize(10);
        });
    }

    private static byte[] Qr(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }
}
