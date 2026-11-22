using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectroMarketing.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.Property(d => d.FileName).HasMaxLength(255);
        b.Property(d => d.ContentType).HasMaxLength(128);
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(d => d.OwnerId);
        b.Ignore(d => d.ObjectKey);
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> b) => b.Property(m => m.Id).HasMaxLength(128);
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.Type).HasMaxLength(128);
        b.Property(m => m.RoutingKey).HasMaxLength(128);
        b.Property(m => m.Payload).HasColumnType("jsonb");
        b.Property(m => m.TraceParent).HasMaxLength(64);
        b.HasIndex(m => m.OccurredAtUtc).HasFilter("\"PublishedAtUtc\" IS NULL");
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(100);
        b.Property(c => c.NormalizedName).HasMaxLength(100);
        b.HasIndex(c => c.NormalizedName).IsUnique();
    }
}

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.Culture).HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.OwnsOne(x => x.Mapping, o => o.ToJson());
        b.OwnsMany(x => x.Errors, o => o.ToJson());
        b.HasIndex(x => x.CreatedAtUtc);
        b.Ignore(x => x.EventName);
    }
}

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> b)
    {
        b.Property(c => c.Name).HasMaxLength(Contact.NameMax);
        b.Property(c => c.Email).HasMaxLength(Contact.EmailMax);
        b.Property(c => c.Phone).HasMaxLength(Contact.PhoneMax);
        b.Property(c => c.PhoneE164).HasMaxLength(16);
        b.Property(c => c.Street).HasMaxLength(Contact.StreetMax);
        b.Property(c => c.PostalCode).HasMaxLength(Contact.PostalCodeMax);
        b.Property(c => c.City).HasMaxLength(Contact.CityMax);
        b.Property(c => c.Country).HasMaxLength(Contact.CountryMax);
        b.Property(c => c.SearchText).HasMaxLength(2000);
        b.HasIndex(c => c.Email).IsUnique();
        b.HasIndex(c => c.PhoneE164).IsUnique();
        b.HasIndex(c => c.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
        b.HasIndex(c => c.ObtainedOn);
        b.HasMany(c => c.Imports).WithOne(i => i.Contact).HasForeignKey(i => i.ContactId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ContactImportConfiguration : IEntityTypeConfiguration<ContactImport>
{
    public void Configure(EntityTypeBuilder<ContactImport> b)
    {
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.Change).HasConversion<string>().HasMaxLength(16);
        b.HasOne(i => i.ImportBatch).WithMany().HasForeignKey(i => i.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        b.OwnsMany(i => i.Changes, o => o.ToJson());
        b.HasIndex(i => i.ImportBatchId);
    }
}
