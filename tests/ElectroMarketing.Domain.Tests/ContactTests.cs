// tests/ElectroMarketing.Domain.Tests/ContactTests.cs
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Domain.Tests;

public class ContactTests
{
    [Fact]
    public void Search_text_is_lower_case_and_contains_phone_digits()
    {
        var contact = new Contact { Name = "Jörg MÜLLER", Email = "j@x.de", Phone = "0170 123-45", PhoneE164 = "+4917012345", City = "Köln" };

        contact.RefreshSearchText();

        contact.SearchText.ShouldBe("jörg müller j@x.de 0170 123-45 +4917012345 017012345 köln");
    }

    [Fact]
    public void Category_normalizes_name() => Category.Normalize("  Fair 2026 ").ShouldBe("FAIR 2026");
}
