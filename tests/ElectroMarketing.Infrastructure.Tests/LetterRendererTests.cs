using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Infrastructure.Campaigns;
using UglyToad.PdfPig;

namespace ElectroMarketing.Infrastructure.Tests;

public class LetterRendererTests
{
    [Fact]
    public void One_page_per_letter_with_address_body_qr_and_printed_link()
    {
        var renderer = new QuestLetterRenderer(new CampaignOptions { SenderLine = "EM · Street 1 · 12345 Town" });
        Letter[] letters =
        [
            new("Jörg Müller", "Hauptstr. 1", "50667", "Köln", null, "Dear Jörg,\nwe have news.", "https://em.test/Ab3dE5g"),
            new("Eva", "Ring 2", "53111", "Bonn", "Germany", "Dear Eva", "https://em.test/Zz9yX8w"),
        ];

        using var pdf = PdfDocument.Open(renderer.Render(letters));

        pdf.NumberOfPages.ShouldBe(2);
        var first = string.Join(' ', pdf.GetPage(1).GetWords().Select(w => w.Text));
        first.ShouldContain("Jörg Müller");
        first.ShouldContain("50667 Köln");
        first.ShouldContain("https://em.test/Ab3dE5g");
        pdf.GetPage(1).GetImages().Count().ShouldBe(1);
        string.Join(' ', pdf.GetPage(2).GetWords().Select(w => w.Text)).ShouldContain("Germany");
    }
}
