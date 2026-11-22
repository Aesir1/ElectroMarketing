using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Domain.Exceptions;

namespace ElectroMarketing.Domain.Tests;

public class DomainTests
{
    [Fact]
    public void New_entities_get_uuid_v7_ids()
    {
        new Document { FileName = "a", ContentType = "b" }.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Document_object_key_is_owner_then_document()
    {
        var owner = Guid.CreateVersion7();
        var document = new Document { OwnerId = owner, FileName = "a", ContentType = "b" };

        document.ObjectKey.ShouldBe($"{owner}/{document.Id}");
        Document.AllowedContentTypes.ShouldContain("APPLICATION/PDF");
        Document.MaxSizeBytes.ShouldBe(25 * 1024 * 1024);
    }

    [Fact]
    public void Exceptions_have_safe_messages()
    {
        NotFoundException.For<Document>(42).Message.ShouldBe("Document 42 was not found.");
        new ForbiddenException().Message.ShouldBe("You are not allowed to do this.");
        var validation = new ValidationException(new Dictionary<string, string[]> { ["Name"] = ["required"], ["Sku"] = ["bad", "short"] });
        validation.Message.ShouldBe("Validation failed: Name: required; Sku: bad, short");
        validation.Errors["Sku"].Length.ShouldBe(2);
        new ConflictException("c").ShouldBeAssignableTo<DomainException>();
        new ConcurrencyException("c").ShouldBeAssignableTo<DomainException>();
    }
}
