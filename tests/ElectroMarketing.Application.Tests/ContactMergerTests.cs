using ElectroMarketing.Application.Imports;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Tests;

public class ContactMergerTests
{
    private static ContactRow Row(string? name = null, string? email = null, string? phone = null, string? e164 = null, string? city = null, DateOnly? on = null) =>
        new(2, name, email, phone, e164, null, null, city, null, on);

    [Fact]
    public void New_contact_gets_every_given_field_as_a_change()
    {
        var contact = new Contact();

        var changes = ContactMerger.Apply(contact, Row("Max", "a@b.de", "0170 1", "+491701", "Köln", new DateOnly(2026, 3, 1)));

        changes.Select(c => c.Field).ShouldBe(["Name", "Email", "Phone", "City", "ObtainedOn"]);
        (contact.Name, contact.Email, contact.PhoneE164, contact.City, contact.ObtainedOn).ShouldBe(("Max", "a@b.de", "+491701", "Köln", new DateOnly(2026, 3, 1)));
        contact.SearchText.ShouldContain("köln");
    }

    [Fact]
    public void Empty_incoming_value_never_erases()
    {
        var contact = new Contact { Name = "Max", City = "Köln" };

        ContactMerger.Apply(contact, Row(email: "a@b.de")).Select(c => c.Field).ShouldBe(["Email"]);

        (contact.Name, contact.City).ShouldBe(("Max", "Köln"));
    }

    [Fact]
    public void Different_value_is_recorded_with_old_and_new()
    {
        var contact = new Contact { City = "Köln" };

        var change = ContactMerger.Apply(contact, Row(city: "Bonn")).Single();

        (change.Field, change.Old, change.New).ShouldBe(("City", "Köln", "Bonn"));
    }

    [Fact]
    public void Same_phone_in_other_notation_is_unchanged()
    {
        var contact = new Contact { Phone = "0170 1", PhoneE164 = "+491701" };

        ContactMerger.Apply(contact, Row(phone: "+49 170 1", e164: "+491701")).ShouldBeEmpty();
        contact.Phone.ShouldBe("0170 1");
    }

    [Fact]
    public void Obtained_date_keeps_the_newest()
    {
        var contact = new Contact { ObtainedOn = new DateOnly(2026, 5, 1) };

        ContactMerger.Apply(contact, Row(on: new DateOnly(2026, 1, 1))).ShouldBeEmpty();
        ContactMerger.Apply(contact, Row(on: new DateOnly(2026, 6, 1))).Single().New.ShouldBe("2026-06-01");
    }
}
