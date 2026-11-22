// tests/ElectroMarketing.Application.Tests/CampaignContentTests.cs
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Tests;

public class CampaignContentTests
{
    private readonly CampaignContent content = new(new TrackingOptions { BaseUrl = "https://em.example/", LandingBaseUrl = "https://app.example" });
    private readonly Contact eve = new() { Name = "<b>Eve</b> & Co", Email = "eve@x.de", PhoneE164 = "+491701234567", Street = "Hauptstr. 1", PostalCode = "50667", City = "Köln" };

    [Fact]
    public void Links_are_short()
    {
        (content.Link("Ab3dE5g"), content.Pixel("Ab3dE5g"), content.Unsubscribe("Ab3dE5g"), content.Landing("Ab3dE5g"))
            .ShouldBe(("https://em.example/Ab3dE5g", "https://em.example/o/Ab3dE5g", "https://em.example/u/Ab3dE5g", "https://app.example/l/Ab3dE5g"));
    }

    [Fact]
    public void Email_encodes_contact_values_and_adds_link_unsubscribe_and_pixel()
    {
        var campaign = new Campaign { Name = "C", EmailSubject = "Hi {{Name}}", EmailBody = "<p>Hello {{Name}}, see {{Link}}</p>" };

        var mail = content.Email(campaign, eve, "Ab3dE5g");

        mail.Subject.ShouldBe("Hi <b>Eve</b> & Co");
        mail.HtmlBody.ShouldStartWith("<p>Hello &lt;b&gt;Eve&lt;/b&gt; &amp; Co, see https://em.example/Ab3dE5g</p>");
        mail.HtmlBody.ShouldContain("""<a href="https://em.example/u/Ab3dE5g">Unsubscribe</a>""");
        mail.HtmlBody.ShouldContain("""<img src="https://em.example/o/Ab3dE5g" width="1" height="1" alt="">""");
        (mail.To, mail.UnsubscribeUrl).ShouldBe(("eve@x.de", "https://em.example/u/Ab3dE5g"));
    }

    [Fact]
    public void Email_without_link_placeholder_gets_the_link_appended()
    {
        var mail = content.Email(new Campaign { Name = "C", EmailSubject = "S", EmailBody = "<p>Hi</p>" }, eve, "Ab3dE5g");

        mail.HtmlBody.ShouldContain("""<p><a href="https://em.example/Ab3dE5g">https://em.example/Ab3dE5g</a></p>""");
    }

    [Theory]
    [InlineData("Hi {{Name}}: {{Link}}", "Hi <b>Eve</b> & Co: https://em.example/Ab3dE5g")]
    [InlineData("Hi {{Unknown}}", "Hi {{Unknown}} https://em.example/Ab3dE5g")]
    public void Sms_is_plain_text_and_always_has_the_link(string template, string expected) =>
        content.Sms(new Campaign { Name = "C", SmsText = template }, eve, "Ab3dE5g").ShouldBe(expected);

    [Fact]
    public void Letter_carries_address_body_and_url()
    {
        var letter = content.Letter(new Campaign { Name = "C", LetterBody = "Dear {{Name}}" }, eve, "Ab3dE5g");

        letter.ShouldBe(new Letter("<b>Eve</b> & Co", "Hauptstr. 1", "50667", "Köln", null, "Dear <b>Eve</b> & Co", "https://em.example/Ab3dE5g"));
    }

    [Fact]
    public void Recipient_rules_skip_missing_addresses_and_opt_outs()
    {
        RecipientRules.SkipReason(Channel.Email, eve).ShouldBeNull();
        RecipientRules.SkipReason(Channel.Email, new Contact { Email = "a@b.de", EmailOptOut = true }).ShouldBe("Unsubscribed from e-mail.");
        RecipientRules.SkipReason(Channel.Sms, new Contact { Email = "a@b.de" }).ShouldBe("No phone number.");
        RecipientRules.SkipReason(Channel.Post, new Contact { Street = "x" }).ShouldBe("No postal address.");
        RecipientRules.SkipReason(Channel.Post, new Contact { Street = "x", City = "y", PostOptOut = true }).ShouldBe("Unsubscribed from post.");
        RecipientRules.SkipReason(Channel.Post, new Contact { Street = "x", City = "y" }).ShouldBeNull();
    }
}
