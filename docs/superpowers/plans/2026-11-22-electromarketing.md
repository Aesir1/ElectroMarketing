# ElectroMarketing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build ElectroMarketing in four milestones: CSV contact import, contact lists, E-Mail/SMS/Post campaigns with short tracking links, and campaign insights. The project is a copy of the DataHub stack.

**Architecture:** The DataHub template is copied and renamed. It uses Clean Architecture: Domain ← Application ← Infrastructure/Auth ← Api/DbUtils, with an Aspire AppHost and a Next.js web app. Each feature has a folder in each layer.
- Imports run in a RabbitMQ consumer, triggered by the outbox domain event `import.created` (`IPublishesChanges`). Campaign sending runs in a polling hosted service, because it can take hours.
- The public tracking routes are minimal-API endpoints on the Api.

**Tech Stack:**
- Backend: .NET 10, Aspire 13.5.4, HotChocolate 16.6.7, EF Core 10 + Npgsql (PostgreSQL 17), RabbitMQ, MinIO, Keycloak.
- Frontend: Next.js 16 + Bun + Tailwind v4.
- Libraries: CsvHelper, libphonenumber-csharp, MailKit, QuestPDF, QRCoder, recharts.

**Spec:** `docs/superpowers/specs/2026-11-22-electromarketing-design.md`

**Template:** `~/RiderProjects/DataHub`. When a step says "as in DataHub", open that file in the template and copy its shape.

## Global Constraints

- **Versions:** same as the template: `global.json` SDK 10.0.401, `AspireVersion=13.5.4`, `HotChocolateVersion=16.6.7`, EF Core 10.0.12, Next ^16.3.6, Bun.
- **New NuGet packages (only these):** `CsvHelper`, `libphonenumber-csharp`, `MailKit`, `QuestPDF`, `QRCoder`. Test-only: `UglyToad.PdfPig`.
  - Pin each in `Directory.Packages.props` to the newest stable version that has been published for at least 2 weeks.
- **New npm package (only this):** `recharts`, pinned the same way.
- **Mailpit:** runs as a plain Aspire container (`axllent/mailpit`). Do not use the CommunityToolkit hosting package. This is a deliberate deviation from the spec: one dependency fewer.
- **Root namespace:** `ElectroMarketing.*`. Namespaces are file-scoped, with usings outside the namespace. Classes are `sealed` by default.
- **Ids and columns:** ids are UUID v7 (`Guid.CreateVersion7()`). Timestamps are `DateTimeOffset` named `...AtUtc`. A contact's obtained date is a `DateOnly ObtainedOn`. Enums are stored as strings (`HasConversion<string>().HasMaxLength(16)`).
- **UI strings:** English only. Number and date formatting uses `Intl.*Format(undefined, ...)`.
- **Permission strings:**
  - `contacts.read`, `imports.write`, `lists.write`
  - `campaigns.read`, `campaigns.write`, `campaigns.send`
  - `insights.read`
- **Short tokens:** 7 characters base62 from `RandomNumberGenerator`.
- **Globalization:** imports need real cultures (`de-DE` date formats, `RegionInfo`). Run `grep -rn -i "InvariantGlobalization\|GLOBALIZATION_INVARIANT" .`. If it is set, turn it off and use an ICU-capable base image in the Api Dockerfile (Task 2).
- **Privacy:** never store a raw IP address. Store the user agent only (max 512 characters).
- **Commits:** every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Contract regeneration:** after every GraphQL change, run:
  `dotnet run --project src/ElectroMarketing.Api -- schema export --output web/schema.graphql && (cd web && bun run codegen)`

## Review Focus

1. **Contact names containing HTML from a CSV** (`<b>Eve</b>`, `<script>`): the name must appear HTML-encoded in the e-mail body. Pinned in Task 12 (`MessageTemplateTests`).
2. **Row whose e-mail matches contact A while its phone belongs to contact B:** the import must not fail on the unique index. A is updated, A's phone stays unchanged, and a row warning is stored. Pinned in Task 6.
3. **Mail security scanners pre-fetch every link in an e-mail:** a GET on the unsubscribe link must never opt the contact out. Only a POST does. Pinned in Task 17.
4. **CSV saved by German Excel:** `;` delimiter, Windows-1252 umlauts (no BOM) or UTF-8 with BOM, `dd.MM.yyyy` dates, `0170 1234567` phones. These must import correctly. Pinned in Task 5 and Task 4.
5. **A restart after a crash:** a redelivered import message must not apply rows twice, and a restarted campaign dispatcher must only send messages that are still `Queued`. Pinned in Task 6 and Task 16.

## Deviations from the spec (decided while planning)

- **Import file reference:** `ImportBatch` keeps `DocumentId` instead of a copied `ObjectKey`. The CSV stays in the documents bucket. Download uses the existing `/api/documents/{id}/content` route, which is restricted to the owner and platform admins.
- **Search text:** `Contact.SearchText` is built in C# by `Contact.RefreshSearchText()`, not by a PostgreSQL generated column. Container locales make `lower()` on umlauts unreliable.
- **Landing data:** the landing page loads its data from an anonymous REST endpoint, `GET /landing/{token}`. GraphQL cannot be used here because `/graphql` requires authentication.
- **Insights:** computed in C# (`CampaignInsights.Compute`) from two queries instead of SQL views. It is easier to test and fast enough for campaigns of up to about 100k messages.
- **Letters:** one merged PDF per campaign, with one letter per page. There are no separate files per contact.
- **Dropped:** ASP.NET rate limiting on the tracking routes (Caddy rate-limits in production) and "test send to self". The editor shows a client-side preview instead.

## File Structure (new code; the rest comes from the template)

```
src/ElectroMarketing.Domain/
  Contacts/Contact.cs            Contact, ContactImport, FieldChange, ContactChange
  Contacts/ImportBatch.cs        Category, ImportBatch, ImportStatus, ColumnMapping, ImportRowError, ImportBatchChanged
  Lists/ContactList.cs           ContactList, ContactListMember
  Campaigns/Campaign.cs          Campaign, CampaignMessage, TrackingEvent, enums, ShortToken
src/ElectroMarketing.Application/
  Contacts/ContactFilter.cs      search filter record
  Imports/ContactNormalizer.cs   e-mail/phone/date normalization + row parsing
  Imports/ContactMerger.cs       field-by-field upsert rule
  Imports/CsvContactFile.cs      decoding, delimiter detection, preview, mapping suggestion, rows
  Imports/ImportService.cs       preview/start/run + IImportStore + handler
  Imports/CategoryService.cs
  Lists/ListService.cs           + IListStore
  Campaigns/CampaignService.cs   draft CRUD + start + ICampaignStore + RecipientRules
  Campaigns/CampaignContent.cs   MessageTemplate, CampaignLinks, message builders, options
  Campaigns/Senders.cs           IEmailSender, ISmsSender, ILetterRenderer, records
  Campaigns/CampaignSender.cs    sends Queued messages of Sending campaigns
  Tracking/TrackingService.cs
  Insights/CampaignInsights.cs   pure computation + InsightsService
src/ElectroMarketing.Infrastructure/
  Persistence/ImportStore.cs, ListStore.cs, CampaignStore.cs, ContactSearch.cs
  Persistence/Configurations/Configurations.cs   (+ new configurations)
  Campaigns/SmtpEmailSender.cs, TwilioSmsSender.cs, LoggingSmsSender.cs, QuestLetterRenderer.cs
src/ElectroMarketing.Api/
  GraphQl/Imports/ImportFeature.cs, GraphQl/Contacts/ContactFeature.cs, GraphQl/Lists/ListFeature.cs,
  GraphQl/Campaigns/CampaignFeature.cs, GraphQl/Insights/InsightsFeature.cs
  TrackingEndpoints.cs, FileEndpoints.cs (+ letters), CampaignDispatcher.cs, Messaging/Consumers.cs (+ import consumer)
web/src/components/
  ImportsPage.tsx, ImportWizard.tsx, ContactsPage.tsx, ContactDetailPage.tsx, ListsPage.tsx,
  CampaignsPage.tsx, CampaignPage.tsx, CampaignInsightsView.tsx, InsightsPage.tsx
web/src/app/app/{contacts,contacts/[id],imports,lists,campaigns,campaigns/[id],insights}/page.tsx
web/src/app/l/[token]/page.tsx                   public landing page
web/src/app/api/campaigns/[id]/letters/route.ts  BFF download
```

---

# Milestone 0 — Skeleton from template

### Task 1: Copy, rename, drop the webhook

**Files:**
- Create: everything under `~/RiderProjects/ElectroMarketing` (copied).
- Delete: `src/ElectroMarketing.Webhook/`, `tests/ElectroMarketing.Webhook.Tests/`.
- Modify: `aspire/ElectroMarketing.AppHost/AppHost.cs`, `Directory.Packages.props`, `compose.prod.yaml`, `aspire/ElectroMarketing.AppHost/keycloak/realms/*.json`, `src/ElectroMarketing.Infrastructure/Messaging/MessagingTopology.cs`, `src/ElectroMarketing.Api/Messaging/Consumers.cs`, `src/ElectroMarketing.Api/DependencyInjection.cs`, `ElectroMarketing.slnx`.

**Interfaces:**
- Produces: a building solution named `ElectroMarketing.slnx`. Namespaces are `ElectroMarketing.*`. `AddElectroMarketingAuth()` and `AddElectroMarketingGraphQl()` exist, and the HotChocolate module is `ElectroMarketingApi` (generated `AddElectroMarketingApi()`).

- [ ] **Step 1: Copy the template without build output, secrets, or host-specific folders**

```bash
cd ~/RiderProjects
rsync -a --exclude-from=- DataHub/ ElectroMarketing/ <<'EOF'
.git/
bin/
obj/
node_modules/
.next/
.env
.idea/
graphify-out/
SqlScripts/
server-provision/
loadtest/
.certs/
appsettings.User.json
backup-secrets.txt
DataHub.sln
coverage/
test-results/
playwright-report/
docs/superpowers/
EOF
cd ElectroMarketing && git status --short | head
```
Expected: many new untracked files. `docs/superpowers/specs/...` and `ElectroMarketing.slnx` are still present.

- [ ] **Step 2: Rename file and folder names (deepest first), then file contents**

```bash
cd ~/RiderProjects/ElectroMarketing
find . -depth \( -name '*DataHub*' -o -name '*datahub*' \) -not -path './.git/*' -not -path './node_modules/*' |
  while read -r p; do
    n="$(basename "$p" | sed 's/DataHub/ElectroMarketing/g; s/datahub/electromarketing/g')"
    mv "$p" "$(dirname "$p")/$n"
  done
grep -rlI --exclude-dir=.git --exclude-dir=node_modules -e DataHub -e Datahub -e datahub -e DATAHUB . |
  xargs sed -i 's/DataHub/ElectroMarketing/g; s/Datahub/Electromarketing/g; s/datahub/electromarketing/g; s/DATAHUB/ELECTROMARKETING/g'
grep -rniI --exclude-dir=.git --exclude-dir=node_modules datahub . ; echo "exit=$?"
```
Expected: no matches (`exit=1`).

- [ ] **Step 3: Remove the webhook function app**

```bash
rm -rf src/ElectroMarketing.Webhook tests/ElectroMarketing.Webhook.Tests
```

Then edit by hand:

1. `AppHost.cs`: delete the `webhookClientSecret`, `webhookHmacSecret` and `storage` (Azurite) variables, the whole `var webhook = builder.AddAzureFunctionsProject...` block, the `.WithEnvironment("WEBHOOK_CLIENT_SECRET", ...)` line on keycloak, and `.Probe("webhook", ...)`. Also remove the two parameters from `aspire/ElectroMarketing.AppHost/appsettings.json`.
2. `Directory.Packages.props`: delete the `Webhook` label group and the `Aspire.Hosting.Azure.*` / `Microsoft.Azure.Functions.*` entries.
3. `compose.prod.yaml` and `.env.example`: delete the `webhook` service and its variables.
4. Keycloak realm JSON: delete the `webhook-container` client.
5. `MessagingTopology.cs`: delete the `Webhook*` constants and the "Webhooks" block in `DeclareAsync`.
6. Delete `src/ElectroMarketing.Domain/Messaging/` (`WebhookReceived`, `ContainerSenders`).
7. `Consumers.cs` and `Api/DependencyInjection.cs`: delete `WebhookInboxConsumer` and its registration.
8. `.github/workflows/ci.yml`, `deploy.yml` and `release` scripts: delete every webhook step and image.

```bash
grep -rniI --exclude-dir=.git --exclude-dir=node_modules -e webhook -e azurite -e AzureFunctions . ; echo "exit=$?"
```
Expected: matches only in `README.md`, which Task 2 rewrites. Delete any other match.

- [ ] **Step 4: Put the projects into the slnx**

```bash
dotnet sln ElectroMarketing.slnx add $(find src aspire tests -name '*.csproj' | sort)
dotnet build ElectroMarketing.slnx
```
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore: scaffold ElectroMarketing from DataHub template without webhook

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Strip sample features, fresh migrations, new secrets, docs

**Files:**
- Delete: `src/ElectroMarketing.Domain/{Products,Containers}/`, `src/ElectroMarketing.Application/{Products,Ingestion}/`, `src/ElectroMarketing.Infrastructure/Persistence/{ProductRepository.cs,TemperatureStore.cs,Sql/ContainerSummaries.sql}`, `src/ElectroMarketing.Infrastructure/Persistence/Migrations/*`, the Auth migrations folder contents, `src/ElectroMarketing.Api/GraphQl/{Products,Containers}/`, `src/ElectroMarketing.DbUtils/MasterData/products.json`, `web/src/components/{ProductsPage,ProductsPage.test,ContainerBrowser,ReadingsTable,ReadingsTable.test}.tsx`, `web/src/app/app/products/`, `web/e2e/products.spec.ts`, and the matching tests in `tests/*`.
- Modify: `AppDbContext.cs`, `Configurations.cs`, `Infrastructure/DependencyInjection.cs`, `Application/DependencyInjection.cs`, `MessagingTopology.cs`, `Consumers.cs`, `Api/DependencyInjection.cs`, `Auth/Permissions.cs`, `DbUtils/MasterData/auth-roles.json`, the DbUtils seed code, `web/src/components/AppNav.tsx`, `web/src/app/app/page.tsx`, `aspire/ElectroMarketing.AppHost/appsettings.json`, `README.md`, `CLAUDE.md`.

**Interfaces:**
- Produces: an `AppDbContext` with only `Documents`, `ProcessedMessages` and `OutboxMessages`. `Permissions` contains only `Documents` and `Queues`. Migrations are fresh `Initial` ones.

- [ ] **Step 1: Delete the sample features**

Delete the files listed above. Then remove every reference to them:
- `DbSet`s, configuration classes, DI registrations.
- `ProductAuditQueue` / `ProductAuditConsumer`.
- `Permissions.Containers` / `Permissions.Products`.
- Seed fixture code that reads `products.json` or creates containers.
- `ArchitectureTests` entries that name those types.

Replace `web/src/app/app/page.tsx` with:

```tsx
import { redirect } from "next/navigation";

export default function Page() {
  redirect("/app/documents");
}
```

In `AppNav.tsx`, keep only the Documents and Admin · Queues links. In `auth-roles.json`, keep only `documents.read`, `documents.write` and (for platform-admin) `queues.manage`.

```bash
grep -rnI --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=gql -e Product -e Temperature -e ContainerSummar -e ContainerBrowser -e ContainerQueries -e 'Domain.Containers' -e Ingest src tests web/src web/e2e aspire ; echo "exit=$?"
```
Expected: `exit=1`.

- [ ] **Step 2: Fresh migrations**

```bash
rm -f src/ElectroMarketing.Infrastructure/Persistence/Migrations/*
AUTHMIG=$(dirname "$(grep -rl 'partial class' src/ElectroMarketing.Auth --include='*_*.cs' | head -1)")
rm -f "$AUTHMIG"/*
dotnet ef migrations add Initial --context AppDbContext --project src/ElectroMarketing.Infrastructure --startup-project src/ElectroMarketing.DbUtils --output-dir Persistence/Migrations
dotnet ef migrations add Initial --context AuthDbContext --project src/ElectroMarketing.Auth --startup-project src/ElectroMarketing.DbUtils --output-dir "${AUTHMIG#src/ElectroMarketing.Auth/}"
```
Expected: two `*_Initial.cs` files.

- [ ] **Step 3: New dev secrets**

```bash
f=aspire/ElectroMarketing.AppHost/appsettings.json
for k in $(jq -r '.Parameters | keys[]' "$f"); do
  jq --arg k "$k" --arg v "$(openssl rand -hex 16)" '.Parameters[$k]=$v' "$f" > "$f.tmp" && mv "$f.tmp" "$f"
done
jq '.Parameters' "$f"
```
Expected: every parameter has a new 32-hex value. Copy `seed-user-password` and `seed-admin-password` into the README quickstart (next step).

- [ ] **Step 4: Docs**

`README.md`:
1. Title → "ElectroMarketing".
2. Replace the feature overview with the four milestones from the spec.
3. Delete the webhook, container and product sections.
4. Keep quickstart, Aspire resources, observability, production, DbUtils, "How to add a feature", tests and CI.
5. Update the seeded user passwords.

`CLAUDE.md`: delete the graphify line. Keep the context7, agent-browser and "prefer editing existing files" lines.

- [ ] **Step 5: Verify everything is green**

```bash
dotnet build ElectroMarketing.slnx -p:CI=true
for p in tests/*/; do dotnet test --project "$p" || break; done
dotnet run --project src/ElectroMarketing.Api -- schema export --output web/schema.graphql && (cd web && bun install && bun run codegen && bun run lint && bun run typecheck && bun run test)
```
Expected: 0 warnings, all tests pass, lint/typecheck/test pass.

Then run `dotnet run --project aspire/ElectroMarketing.AppHost`, start `dbutils-seed` from the dashboard, open https://localhost:3000, sign in as `user@local.test`, and confirm the Documents page loads (agent-browser).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "chore: strip template sample features, fresh migrations and secrets

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

# Milestone 1 — CSV import

### Task 3: Contact and import domain + persistence

**Files:**
- Create: `src/ElectroMarketing.Domain/Contacts/Contact.cs`, `src/ElectroMarketing.Domain/Contacts/ImportBatch.cs`
- Modify: `src/ElectroMarketing.Infrastructure/Persistence/AppDbContext.cs`, `.../Configurations/Configurations.cs`
- Test: `tests/ElectroMarketing.Domain.Tests/ContactTests.cs`, `tests/ElectroMarketing.Infrastructure.Tests/ContactPersistenceTests.cs`

**Interfaces:**
- Produces: `Contact`, `Category`, `ImportBatch`, `ContactImport`, `FieldChange`, `ColumnMapping`, `ImportRowError`, `ImportStatus`, `ContactChange` and `ImportBatchChanged`, exactly as written below. Also the DbSets `Contacts`, `Categories`, `ImportBatches` and `ContactImports`.

- [ ] **Step 1: Write the failing domain test**

```csharp
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
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --project tests/ElectroMarketing.Domain.Tests`
Expected: FAIL to compile (`Contact` not found).

- [ ] **Step 3: Write the domain types**

```csharp
// src/ElectroMarketing.Domain/Contacts/Contact.cs
using ElectroMarketing.Domain.Abstractions;

namespace ElectroMarketing.Domain.Contacts;

/// <summary>One person or company. Only imports change contact data, through <c>ContactMerger</c>.</summary>
public class Contact : AuditableEntity
{
    public const int NameMax = 200;
    public const int EmailMax = 320;
    public const int PhoneMax = 64;
    public const int StreetMax = 200;
    public const int PostalCodeMax = 20;
    public const int CityMax = 100;
    public const int CountryMax = 100;

    public string? Name { get; set; }

    /// <summary>Trimmed and lower-case; unique. First match key of an import.</summary>
    public string? Email { get; set; }

    /// <summary>As written in the last file that changed it.</summary>
    public string? Phone { get; set; }

    /// <summary>E.164; unique. Second match key of an import.</summary>
    public string? PhoneE164 { get; set; }

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public DateOnly? ObtainedOn { get; set; }

    public bool EmailOptOut { get; set; }

    public bool SmsOptOut { get; set; }

    /// <summary>Lower-case text of every searchable field, behind the trigram index. Call <see cref="RefreshSearchText"/> after changes.</summary>
    public string SearchText { get; private set; } = string.Empty;

    public ICollection<ContactImport> Imports { get; set; } = [];

    public void RefreshSearchText()
    {
        var digits = Phone is null ? null : new string(Phone.Where(char.IsAsciiDigit).ToArray());
        string?[] parts = [Name, Email, Phone, PhoneE164, digits, Street, PostalCode, City, Country];
        SearchText = string.Join(' ', parts.Where(p => !string.IsNullOrEmpty(p))).ToLowerInvariant();
    }
}

public enum ContactChange
{
    Created,
    Updated,
    Unchanged,
}

/// <summary>One CSV row applied to one contact: which file touched the contact and what it changed.</summary>
public class ContactImport : IEntity<Guid>
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public Guid ImportBatchId { get; set; }

    public ImportBatch? ImportBatch { get; set; }

    public int RowNumber { get; set; }

    public ContactChange Change { get; set; }

    public List<FieldChange> Changes { get; set; } = [];
}

public class FieldChange
{
    public required string Field { get; set; }

    public string? Old { get; set; }

    public string? New { get; set; }
}
```

```csharp
// src/ElectroMarketing.Domain/Contacts/ImportBatch.cs
using ElectroMarketing.Domain.Abstractions;

namespace ElectroMarketing.Domain.Contacts;

public class Category : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>Upper-case trimmed <see cref="Name"/>; unique, so "Fair 2026" and "fair 2026" are one category.</summary>
    public required string NormalizedName { get; set; }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}

public enum ImportStatus
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>One imported CSV file (a "lot"). Adding it publishes <c>import.created</c>, which starts the import consumer.</summary>
public class ImportBatch : AuditableEntity, IPublishesChanges
{
    public const int MaxStoredErrors = 1000;

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>The uploaded CSV; its bytes stay in object storage.</summary>
    public Guid DocumentId { get; set; }

    public required string FileName { get; set; }

    /// <summary>Culture name such as <c>de-DE</c>: date formats and default phone region.</summary>
    public required string Culture { get; set; }

    public required ColumnMapping Mapping { get; set; }

    public ImportStatus Status { get; set; } = ImportStatus.Pending;

    public int RowCount { get; set; }

    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int UnchangedCount { get; set; }

    public int SkippedCount { get; set; }

    /// <summary>All warnings and errors; only the first <see cref="MaxStoredErrors"/> are kept in <see cref="Errors"/>.</summary>
    public int ErrorCount { get; set; }

    public List<ImportRowError> Errors { get; set; } = [];

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string EventName => "import";

    public object ToChangedEvent(ChangeType change) => new ImportBatchChanged(Id, change, Status);
}

public sealed record ImportBatchChanged(Guid BatchId, ChangeType Change, ImportStatus Status);

/// <summary>CSV header name for each contact field; null when the file has no such column.</summary>
public class ColumnMapping
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? ObtainedOn { get; set; }
}

/// <summary><see cref="Row"/> is the physical CSV line (header = 1); 0 means the whole file.</summary>
public class ImportRowError
{
    public int Row { get; set; }

    public required string Message { get; set; }
}
```

- [ ] **Step 4: Run the domain test**

Run: `dotnet test --project tests/ElectroMarketing.Domain.Tests`
Expected: PASS.

- [ ] **Step 5: Write the failing persistence test**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/ContactPersistenceTests.cs
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class ContactPersistenceTests(PostgresFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Email_and_phone_are_unique()
    {
        await using (var ctx = db.Create())
        {
            ctx.Contacts.Add(new Contact { Email = "a@x.de", PhoneE164 = "+4917012345" });
            await ctx.SaveChangesAsync(Ct);
        }

        await using var again = db.Create();
        again.Contacts.Add(new Contact { Email = "a@x.de" });
        await Should.ThrowAsync<DbUpdateException>(() => again.SaveChangesAsync(Ct));

        again.ChangeTracker.Clear();
        again.Contacts.Add(new Contact { PhoneE164 = "+4917012345" });
        await Should.ThrowAsync<DbUpdateException>(() => again.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Batch_mapping_and_errors_round_trip_as_json()
    {
        var category = new Category { Name = "Fair", NormalizedName = "FAIR" };
        var document = new Domain.Documents.Document { OwnerId = Guid.CreateVersion7(), FileName = "a.csv", ContentType = "text/csv" };
        var batch = new ImportBatch
        {
            CategoryId = category.Id,
            DocumentId = document.Id,
            FileName = "a.csv",
            Culture = "de-DE",
            Mapping = new ColumnMapping { Email = "E-Mail" },
            Errors = [new ImportRowError { Row = 3, Message = "bad" }],
        };
        await using (var ctx = db.Create())
        {
            ctx.AddRange(category, document, batch);
            await ctx.SaveChangesAsync(Ct);
        }

        await using var read = db.Create();
        var loaded = await read.ImportBatches.SingleAsync(Ct);
        loaded.Mapping.Email.ShouldBe("E-Mail");
        loaded.Errors.Single().Row.ShouldBe(3);
    }
}
```

- [ ] **Step 6: Run it to verify it fails**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ContactPersistenceTests"`
Expected: FAIL to compile (`ctx.Contacts` not found).

- [ ] **Step 7: Configure EF and add the migration**

In `AppDbContext.cs`, add the DbSets and the extension:

```csharp
public DbSet<Contact> Contacts => Set<Contact>();

public DbSet<Category> Categories => Set<Category>();

public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

public DbSet<ContactImport> ContactImports => Set<ContactImport>();
```

Make this the first line of `OnModelCreating`:

```csharp
modelBuilder.HasPostgresExtension("pg_trgm");
```

Append to `Configurations.cs`:

```csharp
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
```

```bash
dotnet ef migrations add ContactsAndImports --context AppDbContext --project src/ElectroMarketing.Infrastructure --startup-project src/ElectroMarketing.DbUtils --output-dir Persistence/Migrations
```

- [ ] **Step 8: Run the persistence tests**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ContactPersistenceTests"`
Expected: PASS (2 tests).

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat(contacts): contact, category and import batch model

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 4: Row normalization and merge rule

**Files:**
- Create: `src/ElectroMarketing.Application/Imports/ContactNormalizer.cs`, `src/ElectroMarketing.Application/Imports/ContactMerger.cs`
- Modify: `Directory.Packages.props` (add `libphonenumber-csharp`), `src/ElectroMarketing.Application/ElectroMarketing.Application.csproj` (PackageReference)
- Test: `tests/ElectroMarketing.Application.Tests/ContactNormalizerTests.cs`, `tests/ElectroMarketing.Application.Tests/ContactMergerTests.cs`

**Interfaces:**
- Consumes: `Contact`, `FieldChange` (Task 3).
- Produces:
  - `RawRow(int RowNumber, string? Name, string? Email, string? Phone, string? Street, string? PostalCode, string? City, string? Country, string? ObtainedOn)`
  - `ContactRow(int RowNumber, string? Name, string? Email, string? Phone, string? PhoneE164, string? Street, string? PostalCode, string? City, string? Country, DateOnly? ObtainedOn)`
  - `ParsedRow(ContactRow? Row, string? SkipReason, IReadOnlyList<string> Warnings)`
  - `ContactNormalizer.Parse(RawRow, CultureInfo) : ParsedRow`
  - `ContactNormalizer.Email(string?) : string?`
  - `ContactNormalizer.PhoneE164(string?, string region) : string?`
  - `ContactNormalizer.Date(string?, CultureInfo) : DateOnly?`
  - `ContactNormalizer.Region(CultureInfo) : string`
  - `ContactMerger.Apply(Contact, ContactRow) : List<FieldChange>`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Application.Tests/ContactNormalizerTests.cs
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
```

```csharp
// tests/ElectroMarketing.Application.Tests/ContactMergerTests.cs
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
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Application.Tests`
Expected: FAIL to compile.

- [ ] **Step 3: Add the package and implement**

In `Directory.Packages.props`, under a new `<!-- Imports -->` label group, add `<PackageVersion Include="libphonenumber-csharp" Version="X" />` (latest stable, at least 2 weeks old). Add `<PackageReference Include="libphonenumber-csharp" />` to the Application csproj.

```csharp
// src/ElectroMarketing.Application/Imports/ContactNormalizer.cs
using System.Globalization;
using System.Net.Mail;
using ElectroMarketing.Domain.Contacts;
using PhoneNumbers;

namespace ElectroMarketing.Application.Imports;

/// <summary>One CSV row as text, by contact field (after column mapping).</summary>
public sealed record RawRow(int RowNumber, string? Name, string? Email, string? Phone, string? Street, string? PostalCode, string? City, string? Country, string? ObtainedOn);

/// <summary>A validated row: e-mail normalized, phone in E.164, at least one of both present.</summary>
public sealed record ContactRow(int RowNumber, string? Name, string? Email, string? Phone, string? PhoneE164, string? Street, string? PostalCode, string? City, string? Country, DateOnly? ObtainedOn);

public sealed record ParsedRow(ContactRow? Row, string? SkipReason, IReadOnlyList<string> Warnings);

public static class ContactNormalizer
{
    private static readonly PhoneNumberUtil Phones = PhoneNumberUtil.GetInstance();

    public static ParsedRow Parse(RawRow raw, CultureInfo culture)
    {
        var warnings = new List<string>();
        var name = Clean(raw.Name);
        var email = Email(raw.Email);
        if (email is null && Clean(raw.Email) is { } badEmail)
        {
            warnings.Add($"Invalid e-mail '{badEmail}' ignored.");
        }

        var phone = Clean(raw.Phone);
        var e164 = PhoneE164(phone, Region(culture));
        if (e164 is null && phone is not null)
        {
            warnings.Add($"Invalid phone number '{phone}' ignored.");
            phone = null;
        }

        var obtained = Date(raw.ObtainedOn, culture);
        if (obtained is null && Clean(raw.ObtainedOn) is { } badDate)
        {
            warnings.Add($"Invalid date '{badDate}' ignored.");
        }

        var row = new ContactRow(raw.RowNumber, name, email, phone, e164, Clean(raw.Street), Clean(raw.PostalCode), Clean(raw.City), Clean(raw.Country), obtained);
        var skip = TooLong(row) ?? (email is null && e164 is null ? "No valid e-mail or phone number." : null);
        return new ParsedRow(skip is null ? row : null, skip, warnings);
    }

    public static string? Email(string? raw)
    {
        var value = Clean(raw)?.ToLowerInvariant();
        return value is not null && value.Length <= Contact.EmailMax && MailAddress.TryCreate(value, out var address) && address.Address == value
            ? value
            : null;
    }

    public static string? PhoneE164(string? raw, string region)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var number = Phones.Parse(raw, region);
            return Phones.IsValidNumber(number) ? Phones.Format(number, PhoneNumberFormat.E164) : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    public static DateOnly? Date(string? raw, CultureInfo culture)
    {
        var value = Clean(raw);
        if (value is null)
        {
            return null;
        }

        return DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out var date)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date)
            ? DateOnly.FromDateTime(date)
            : null;
    }

    public static string Region(CultureInfo culture) => new RegionInfo(culture.Name).TwoLetterISORegionName;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TooLong(ContactRow row) =>
        Check(nameof(Contact.Name), row.Name, Contact.NameMax)
        ?? Check(nameof(Contact.Phone), row.Phone, Contact.PhoneMax)
        ?? Check(nameof(Contact.Street), row.Street, Contact.StreetMax)
        ?? Check(nameof(Contact.PostalCode), row.PostalCode, Contact.PostalCodeMax)
        ?? Check(nameof(Contact.City), row.City, Contact.CityMax)
        ?? Check(nameof(Contact.Country), row.Country, Contact.CountryMax);

    private static string? Check(string field, string? value, int max) =>
        value?.Length > max ? $"{field} is longer than {max} characters." : null;
}
```

```csharp
// src/ElectroMarketing.Application/Imports/ContactMerger.cs
using System.Globalization;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Imports;

/// <summary>
/// The upsert rule: a non-empty incoming value replaces a different stored value; an empty one never erases.
/// The phone counts as changed only when its E.164 form differs. The obtained date keeps the newest.
/// </summary>
public static class ContactMerger
{
    public static List<FieldChange> Apply(Contact contact, ContactRow row)
    {
        var changes = new List<FieldChange>();
        Set(changes, nameof(Contact.Name), contact.Name, row.Name, v => contact.Name = v);
        Set(changes, nameof(Contact.Email), contact.Email, row.Email, v => contact.Email = v);
        if (row.PhoneE164 is not null && row.PhoneE164 != contact.PhoneE164)
        {
            changes.Add(new FieldChange { Field = nameof(Contact.Phone), Old = contact.Phone, New = row.Phone });
            contact.Phone = row.Phone;
            contact.PhoneE164 = row.PhoneE164;
        }

        Set(changes, nameof(Contact.Street), contact.Street, row.Street, v => contact.Street = v);
        Set(changes, nameof(Contact.PostalCode), contact.PostalCode, row.PostalCode, v => contact.PostalCode = v);
        Set(changes, nameof(Contact.City), contact.City, row.City, v => contact.City = v);
        Set(changes, nameof(Contact.Country), contact.Country, row.Country, v => contact.Country = v);
        if (row.ObtainedOn is { } obtained && (contact.ObtainedOn is null || obtained > contact.ObtainedOn))
        {
            changes.Add(new FieldChange { Field = nameof(Contact.ObtainedOn), Old = Iso(contact.ObtainedOn), New = Iso(obtained) });
            contact.ObtainedOn = obtained;
        }

        contact.RefreshSearchText();
        return changes;
    }

    private static void Set(List<FieldChange> changes, string field, string? current, string? incoming, Action<string> set)
    {
        if (incoming is null || string.Equals(current, incoming, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new FieldChange { Field = field, Old = current, New = incoming });
        set(incoming);
    }

    private static string? Iso(DateOnly? date) => date?.ToString("O", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Application.Tests`
Expected: PASS. If a phone `InlineData` fails because libphonenumber's metadata disagrees, fix the test data to a valid DE mobile number. Do not loosen the code.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(imports): row normalization and merge rule

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 5: CSV file reading

**Files:**
- Create: `src/ElectroMarketing.Application/Imports/CsvContactFile.cs`
- Modify: `Directory.Packages.props` (add `CsvHelper` to the Imports group), Application csproj
- Test: `tests/ElectroMarketing.Application.Tests/CsvContactFileTests.cs`

**Interfaces:**
- Consumes: `RawRow` (Task 4), `ColumnMapping` (Task 3).
- Produces:
  - `CsvContactFile.ReadAsync(Stream, CancellationToken) : Task<CsvContactFile>`
  - Properties: `Delimiter : string`, `EncodingName : string`, `Headers : IReadOnlyList<string>`
  - Methods: `Preview(int rows = 10) : CsvPreview`, `Rows(ColumnMapping) : IEnumerable<RawRow>`, static `SuggestMapping(IReadOnlyList<string>) : ColumnMapping`
  - `CsvPreview(string Delimiter, string Encoding, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, ColumnMapping SuggestedMapping)`

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Application.Tests --filter-class "*CsvContactFileTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement**

```csharp
// src/ElectroMarketing.Application/Imports/CsvContactFile.cs
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
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
        Headers = reader.Read() && reader.ReadHeader() ? reader.HeaderRecord ?? [] : [];
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
        reader.Read();
        reader.ReadHeader();
        var result = new List<IReadOnlyList<string>>();
        while (result.Count < rows && reader.Read())
        {
            result.Add(reader.Parser.Record ?? []);
        }

        return new CsvPreview(Delimiter, EncodingName, Headers, result, SuggestMapping(Headers));
    }

    /// <summary>Data rows; <see cref="RawRow.RowNumber"/> is the CSV line (header = 1). Header names match case-insensitively.</summary>
    public IEnumerable<RawRow> Rows(ColumnMapping mapping)
    {
        int? Index(string? header) =>
            header is null ? null : Headers.Select((h, i) => (h, i)).FirstOrDefault(x => string.Equals(x.h, header, StringComparison.OrdinalIgnoreCase), (string.Empty, -1)).Item2 is var i and >= 0 ? i : null;

        var (name, email, phone, street, postal, city, country, obtained) = (
            Index(mapping.Name), Index(mapping.Email), Index(mapping.Phone), Index(mapping.Street),
            Index(mapping.PostalCode), Index(mapping.City), Index(mapping.Country), Index(mapping.ObtainedOn));

        using var reader = Reader();
        reader.Read();
        reader.ReadHeader();
        while (reader.Read())
        {
            var record = reader.Parser.Record ?? [];
            string? Field(int? index) => index is { } i && i < record.Length ? record[i] : null;
            yield return new RawRow(
                reader.Parser.Row,
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
        var end = text.IndexOfAny(['\r', '\n']);
        var header = end < 0 ? text : text[..end];
        return new[] { ";", ",", "\t" }.MaxBy(d => header.Count(c => c == d[0]))!;
    }

    private CsvReader Reader() => new(new StringReader(text), new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Delimiter = Delimiter,
        BadDataFound = null,
        MissingFieldFound = null,
        DetectColumnCountChanges = false,
        TrimOptions = TrimOptions.Trim,
    });
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Application.Tests --filter-class "*CsvContactFileTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(imports): CSV decoding, delimiter detection and column mapping

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 6: Import service, store and consumer

**Files:**
- Create: `src/ElectroMarketing.Application/Imports/ImportService.cs`, `src/ElectroMarketing.Application/Imports/CategoryService.cs`, `src/ElectroMarketing.Infrastructure/Persistence/ImportStore.cs`
- Modify: `src/ElectroMarketing.Application/DependencyInjection.cs`, `src/ElectroMarketing.Infrastructure/DependencyInjection.cs`, `src/ElectroMarketing.Infrastructure/Messaging/MessagingTopology.cs`, `src/ElectroMarketing.Api/Messaging/Consumers.cs`, `src/ElectroMarketing.Api/DependencyInjection.cs`
- Test: `tests/ElectroMarketing.Infrastructure.Tests/ImportServiceTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 3–5; `DocumentService`, `IRepository<,>`, `IObjectStorage`, `IMessageHandler<T>`, `HandlerConsumer<T>` (template).
- Produces:
  - `StartImportDto(Guid DocumentId, Guid CategoryId, ColumnMapping Mapping, string Culture)`
  - `ImportService.PreviewAsync(Guid documentId, CancellationToken) : Task<CsvPreview>`
  - `ImportService.StartAsync(StartImportDto, CancellationToken) : Task<ImportBatch>`
  - `ImportService.RunAsync(Guid batchId, CancellationToken) : Task`
  - `CreateCategoryDto(string Name)`, `CategoryService : CrudService<Category, Guid, CreateCategoryDto, CreateCategoryDto>`
  - `IImportStore` (below), `MessagingTopology.ContactImportQueue = "contact-import"`

- [ ] **Step 1: Write the failing integration test**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/ImportServiceTests.cs
using System.Text;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Documents;
using ElectroMarketing.Application.Imports;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

/// <summary>The whole import against PostgreSQL with the CSV served by a fake object storage.</summary>
public sealed class ImportServiceTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly IObjectStorage storage = Substitute.For<IObjectStorage>();
    private readonly Category category = new() { Name = "Fair", NormalizedName = "FAIR" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        await using var ctx = db.Create();
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ImportService Service(AppDbContext ctx) => new(
        new ImportStore(ctx),
        new DocumentService(new Repository<Document, Guid>(ctx), storage, Substitute.For<ICurrentUser>()),
        new Repository<Document, Guid>(ctx),
        new Repository<Category, Guid>(ctx),
        new Repository<ImportBatch, Guid>(ctx),
        storage,
        db.Time);

    /// <summary>Stores a document row and a pending batch for <paramref name="csv"/>, then runs the import.</summary>
    private async Task<ImportBatch> ImportAsync(string csv)
    {
        var document = new Document { OwnerId = Guid.CreateVersion7(), FileName = "f.csv", ContentType = "text/csv", Status = DocumentStatus.Available };
        storage.GetAsync(Document.Bucket, document.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(_ => new StoredObject(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "text/csv", csv.Length));
        var batch = new ImportBatch
        {
            CategoryId = category.Id,
            DocumentId = document.Id,
            FileName = "f.csv",
            Culture = "de-DE",
            Mapping = CsvContactFile.SuggestMapping(csv.Split('\n')[0].Split(';')),
        };
        await using (var ctx = db.Create())
        {
            ctx.AddRange(document, batch);
            await ctx.SaveChangesAsync(Ct);
        }

        await using (var ctx = db.Create())
        {
            await Service(ctx).RunAsync(batch.Id, Ct);
        }

        await using var read = db.Create();
        return await read.ImportBatches.SingleAsync(b => b.Id == batch.Id, Ct);
    }

    [Fact]
    public async Task Creates_then_updates_with_history_per_file()
    {
        var first = await ImportAsync("Name;E-Mail;Telefon;Ort\nMax;max@x.de;0170 1234567;Köln\nEva;eva@x.de;;Bonn\n;;;Nowhere\n");
        (first.Status, first.RowCount, first.CreatedCount, first.SkippedCount).ShouldBe((ImportStatus.Done, 3, 2, 1));
        first.Errors.Single().Row.ShouldBe(4);

        // Max matched by phone (new e-mail), Eva matched by e-mail and moves; unchanged row still recorded.
        var second = await ImportAsync("Name;E-Mail;Telefon;Ort\nMax;max.new@x.de;+49 170 1234567;Köln\nEva;EVA@x.de;;Berlin\nMax;max.new@x.de;;\n");
        (second.CreatedCount, second.UpdatedCount, second.UnchangedCount).ShouldBe((0, 2, 1));

        await using var ctx = db.Create();
        var max = await ctx.Contacts.Include(c => c.Imports).SingleAsync(c => c.PhoneE164 == "+491701234567", Ct);
        max.Email.ShouldBe("max.new@x.de");
        max.Imports.Select(i => i.ImportBatchId).ShouldBe([first.Id, second.Id, second.Id], ignoreOrder: true);
        max.Imports.Single(i => i.ImportBatchId == second.Id && i.Change == ContactChange.Updated)
            .Changes.Single().ShouldSatisfyAllConditions(c => c.Field.ShouldBe("Email"), c => c.Old.ShouldBe("max@x.de"));
        (await ctx.Contacts.SingleAsync(c => c.Email == "eva@x.de", Ct)).City.ShouldBe("Berlin");
    }

    [Fact]
    public async Task Email_match_wins_and_foreign_phone_is_not_taken()
    {
        await ImportAsync("E-Mail;Telefon\na@x.de;0170 1111111\nb@x.de;0170 2222222\n");

        var batch = await ImportAsync("E-Mail;Telefon;Ort\na@x.de;0170 2222222;Köln\n");

        batch.Status.ShouldBe(ImportStatus.Done);
        batch.Errors.Single().Message.ShouldBe("Phone +491702222222 belongs to another contact; phone not changed.");
        await using var ctx = db.Create();
        var a = await ctx.Contacts.SingleAsync(c => c.Email == "a@x.de", Ct);
        (a.PhoneE164, a.City).ShouldBe(("+491701111111", "Köln"));
    }

    [Fact]
    public async Task Redelivery_of_a_finished_batch_does_nothing_and_running_batch_fails()
    {
        var done = await ImportAsync("E-Mail\na@x.de\n");
        await using (var ctx = db.Create())
        {
            await Service(ctx).RunAsync(done.Id, Ct);
            (await ctx.ContactImports.CountAsync(Ct)).ShouldBe(1);
            var batch = await ctx.ImportBatches.SingleAsync(Ct);
            batch.Status = ImportStatus.Running;
            await ctx.SaveChangesAsync(Ct);
        }

        await using (var ctx = db.Create())
        {
            await Service(ctx).RunAsync(done.Id, Ct);
        }

        await using var read = db.Create();
        var failed = await read.ImportBatches.SingleAsync(Ct);
        failed.Status.ShouldBe(ImportStatus.Failed);
        (await read.ContactImports.CountAsync(Ct)).ShouldBe(1);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ImportServiceTests"`
Expected: FAIL to compile (`ImportService`, `ImportStore` not found).

- [ ] **Step 3: Implement the service**

```csharp
// src/ElectroMarketing.Application/Imports/ImportService.cs
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Application.Documents;
using ElectroMarketing.Domain.Abstractions;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Domain.Exceptions;
using DomainValidationException = ElectroMarketing.Domain.Exceptions.ValidationException;

namespace ElectroMarketing.Application.Imports;

public sealed record StartImportDto(
    Guid DocumentId,
    Guid CategoryId,
    [property: Required] ColumnMapping Mapping,
    [property: Required, StringLength(16)] string Culture);

/// <summary>Persistence for <see cref="ImportService.RunAsync"/>: one DbContext, the batch stays tracked, rows are detached per chunk.</summary>
public interface IImportStore
{
    Task<ImportBatch?> GetBatchAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked contacts whose e-mail or E.164 phone is in the given sets.</summary>
    Task<IReadOnlyList<Contact>> FindContactsAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> phones, CancellationToken ct);

    void Add(Contact contact);

    void Add(ContactImport import);

    /// <summary>Saves all changes, then detaches everything except the batch so memory stays flat across chunks.</summary>
    Task SaveAsync(CancellationToken ct);

    /// <summary>Drops unsaved contact and history changes after a failed chunk.</summary>
    void DiscardChanges();
}

public sealed class ImportService(
    IImportStore store,
    DocumentService documents,
    IRepository<Document, Guid> documentRows,
    IRepository<Category, Guid> categories,
    IRepository<ImportBatch, Guid> batches,
    IObjectStorage storage,
    TimeProvider time)
{
    public const int ChunkSize = 1000;

    public async Task<CsvPreview> PreviewAsync(Guid documentId, CancellationToken ct = default) =>
        (await OpenAsync(documentId, ct)).File.Preview();

    /// <summary>Validates and stores a Pending batch; the outbox event starts <see cref="RunAsync"/> in the consumer.</summary>
    public async Task<ImportBatch> StartAsync(StartImportDto dto, CancellationToken ct = default)
    {
        DtoValidator.Validate(dto);
        var culture = ParseCulture(dto.Culture);
        var (document, file) = await OpenAsync(dto.DocumentId, ct);
        ValidateMapping(dto.Mapping, file.Headers);
        _ = await categories.GetByIdAsync(dto.CategoryId, ct) ?? throw NotFoundException.For<Category>(dto.CategoryId);
        return await batches.AddAsync(
            new ImportBatch { CategoryId = dto.CategoryId, DocumentId = document.Id, FileName = document.FileName, Culture = culture.Name, Mapping = dto.Mapping },
            ct);
    }

    /// <summary>Imports a Pending batch. Redelivered messages are no-ops; an interrupted (Running) batch is failed, not re-applied.</summary>
    public async Task RunAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await store.GetBatchAsync(batchId, ct) ?? throw NotFoundException.For<ImportBatch>(batchId);
        if (batch.Status != ImportStatus.Pending)
        {
            if (batch.Status == ImportStatus.Running)
            {
                batch.Status = ImportStatus.Failed;
                batch.ErrorCount++;
                batch.Errors.Add(new ImportRowError { Row = 0, Message = "The import was interrupted. Import the file again; unchanged rows are detected." });
                batch.FinishedAtUtc = time.GetUtcNow();
                await store.SaveAsync(ct);
            }

            return;
        }

        batch.Status = ImportStatus.Running;
        await store.SaveAsync(ct);

        var log = new ImportLog();
        try
        {
            var document = await documentRows.GetByIdAsync(batch.DocumentId, ct) ?? throw NotFoundException.For<Document>(batch.DocumentId);
            await using var content = await storage.GetAsync(Document.Bucket, document.ObjectKey, ct)
                ?? throw NotFoundException.For<Document>(batch.DocumentId);
            var file = await CsvContactFile.ReadAsync(content.Content, ct);
            var culture = CultureInfo.GetCultureInfo(batch.Culture);
            foreach (var chunk in file.Rows(batch.Mapping).Chunk(ChunkSize))
            {
                await ImportChunkAsync(batch.Id, chunk, culture, log, ct);
            }

            batch.Status = ImportStatus.Done;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            store.DiscardChanges();
            batch.Status = ImportStatus.Failed;
            log.Error(0, ex is DomainException ? ex.Message : $"Import failed: {ex.Message}");
        }

        log.CopyTo(batch);
        batch.FinishedAtUtc = time.GetUtcNow();
        await store.SaveAsync(ct);
    }

    private async Task ImportChunkAsync(Guid batchId, RawRow[] chunk, CultureInfo culture, ImportLog log, CancellationToken ct)
    {
        var rows = new List<ContactRow>(chunk.Length);
        foreach (var raw in chunk)
        {
            log.Rows++;
            var parsed = ContactNormalizer.Parse(raw, culture);
            foreach (var warning in parsed.Warnings)
            {
                log.Error(raw.RowNumber, warning);
            }

            if (parsed.Row is null)
            {
                log.Skipped++;
                log.Error(raw.RowNumber, parsed.SkipReason!);
                continue;
            }

            rows.Add(parsed.Row);
        }

        var existing = await store.FindContactsAsync(
            rows.Select(r => r.Email).OfType<string>().ToHashSet(),
            rows.Select(r => r.PhoneE164).OfType<string>().ToHashSet(),
            ct);
        var byEmail = existing.Where(c => c.Email is not null).ToDictionary(c => c.Email!);
        var byPhone = existing.Where(c => c.PhoneE164 is not null).ToDictionary(c => c.PhoneE164!);

        foreach (var row in rows)
        {
            // Match key 1: e-mail; match key 2: phone.
            var contact = (row.Email is null ? null : byEmail.GetValueOrDefault(row.Email))
                ?? (row.PhoneE164 is null ? null : byPhone.GetValueOrDefault(row.PhoneE164));
            var created = contact is null;
            if (contact is null)
            {
                contact = new Contact();
                store.Add(contact);
            }

            var effective = row;
            if (row.PhoneE164 is not null && byPhone.TryGetValue(row.PhoneE164, out var owner) && owner != contact)
            {
                log.Error(row.RowNumber, $"Phone {row.PhoneE164} belongs to another contact; phone not changed.");
                effective = row with { Phone = null, PhoneE164 = null };
            }

            var (oldEmail, oldPhone) = (contact.Email, contact.PhoneE164);
            var changes = ContactMerger.Apply(contact, effective);
            Reindex(byEmail, oldEmail, contact.Email, contact);
            Reindex(byPhone, oldPhone, contact.PhoneE164, contact);

            var change = created ? ContactChange.Created : changes.Count > 0 ? ContactChange.Updated : ContactChange.Unchanged;
            log.Count(change);
            store.Add(new ContactImport { ContactId = contact.Id, ImportBatchId = batchId, RowNumber = row.RowNumber, Change = change, Changes = changes });
        }

        await store.SaveAsync(ct);
    }

    private static void Reindex(Dictionary<string, Contact> index, string? oldKey, string? newKey, Contact contact)
    {
        if (oldKey is not null && oldKey != newKey)
        {
            index.Remove(oldKey);
        }

        if (newKey is not null)
        {
            index[newKey] = contact;
        }
    }

    private async Task<(Document Document, CsvContactFile File)> OpenAsync(Guid documentId, CancellationToken ct)
    {
        var (document, content) = await documents.OpenContentAsync(documentId, ct);
        await using (content)
        {
            return (document, await CsvContactFile.ReadAsync(content.Content, ct));
        }
    }

    private static CultureInfo ParseCulture(string name)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(name);
            if (!culture.IsNeutralCulture && culture.Name.Length > 0)
            {
                return culture;
            }
        }
        catch (CultureNotFoundException)
        {
            // falls through to the validation error
        }

        throw Invalid(nameof(StartImportDto.Culture), "Use a specific culture such as de-DE or en-US.");
    }

    private static void ValidateMapping(ColumnMapping mapping, IReadOnlyList<string> headers)
    {
        if (mapping.Email is null && mapping.Phone is null)
        {
            throw Invalid(nameof(StartImportDto.Mapping), "Map at least the e-mail or the phone column.");
        }

        string?[] mapped = [mapping.Name, mapping.Email, mapping.Phone, mapping.Street, mapping.PostalCode, mapping.City, mapping.Country, mapping.ObtainedOn];
        var missing = mapped.OfType<string>().Where(m => !headers.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            throw Invalid(nameof(StartImportDto.Mapping), $"Columns not in the file: {string.Join(", ", missing)}.");
        }
    }

    private static DomainValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private sealed class ImportLog
    {
        private readonly List<ImportRowError> errors = [];
        private int created;
        private int updated;
        private int unchanged;
        private int errorCount;

        public int Rows { get; set; }

        public int Skipped { get; set; }

        public void Error(int row, string message)
        {
            errorCount++;
            if (errors.Count < ImportBatch.MaxStoredErrors)
            {
                errors.Add(new ImportRowError { Row = row, Message = message });
            }
        }

        public void Count(ContactChange change)
        {
            _ = change switch
            {
                ContactChange.Created => created++,
                ContactChange.Updated => updated++,
                _ => unchanged++,
            };
        }

        public void CopyTo(ImportBatch batch)
        {
            (batch.RowCount, batch.CreatedCount, batch.UpdatedCount, batch.UnchangedCount, batch.SkippedCount) = (Rows, created, updated, unchanged, Skipped);
            batch.ErrorCount += errorCount;
            batch.Errors.AddRange(errors.Take(ImportBatch.MaxStoredErrors - batch.Errors.Count));
        }
    }
}

/// <summary>Runs the import for every new batch (routing key <c>import.created</c>).</summary>
public sealed class ImportRequestedHandler(ImportService imports) : IMessageHandler<ImportBatchChanged>
{
    public Task HandleAsync(ImportBatchChanged message, MessageContext context, CancellationToken ct) =>
        message.Change == ChangeType.Created ? imports.RunAsync(message.BatchId, ct) : Task.CompletedTask;
}
```

```csharp
// src/ElectroMarketing.Application/Imports/CategoryService.cs
using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Imports;

public sealed record CreateCategoryDto([property: Required, StringLength(100, MinimumLength = 1)] string Name);

/// <summary>A duplicate name (case-insensitive) hits the unique index and surfaces as ConflictError.</summary>
public sealed class CategoryService(IRepository<Category, Guid> categories)
    : CrudService<Category, Guid, CreateCategoryDto, CreateCategoryDto>(categories)
{
    protected override Category Map(CreateCategoryDto dto) => new() { Name = dto.Name.Trim(), NormalizedName = Category.Normalize(dto.Name) };

    protected override void Apply(CreateCategoryDto dto, Category entity)
    {
        entity.Name = dto.Name.Trim();
        entity.NormalizedName = Category.Normalize(dto.Name);
    }
}
```

```csharp
// src/ElectroMarketing.Infrastructure/Persistence/ImportStore.cs
using ElectroMarketing.Application.Imports;
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

public sealed class ImportStore(AppDbContext db) : IImportStore
{
    public Task<ImportBatch?> GetBatchAsync(Guid id, CancellationToken ct) => db.ImportBatches.SingleOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Contact>> FindContactsAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> phones, CancellationToken ct) =>
        await db.Contacts
            .Where(c => (c.Email != null && emails.Contains(c.Email)) || (c.PhoneE164 != null && phones.Contains(c.PhoneE164)))
            .ToListAsync(ct);

    public void Add(Contact contact) => db.Contacts.Add(contact);

    public void Add(ContactImport import) => db.ContactImports.Add(import);

    public async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        DetachRows();
    }

    public void DiscardChanges() => DetachRows();

    private void DetachRows()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is not (ImportBatch or ImportRowError or ColumnMapping)).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
```

- [ ] **Step 4: Wire DI and messaging**

1. `Application/DependencyInjection.cs`: add these registrations:
   - `services.AddScoped<ImportService>();`
   - `services.AddScoped<CategoryService>();`
   - `services.AddScoped<IMessageHandler<ImportBatchChanged>, ImportRequestedHandler>();`
2. `Infrastructure/DependencyInjection.cs`: add `services.AddScoped<IImportStore, ImportStore>();`
3. `MessagingTopology.cs`: add the constant `public const string ContactImportQueue = "contact-import";` and, in `DeclareAsync`, this line:
   `await DeclareQueueAsync(channel, ContactImportQueue, DomainEvents, "import.created", DomainEventsDlx, ct);`
4. `Api/Messaging/Consumers.cs`: add the consumer below.

```csharp
/// <summary>Imports each new CSV batch (routing key <c>import.created</c>).</summary>
public sealed class ContactImportConsumer(IConnection connection, IServiceScopeFactory scopes, ILogger<ContactImportConsumer> logger)
    : HandlerConsumer<ImportBatchChanged>(connection, scopes, logger)
{
    protected override string Queue => MessagingTopology.ContactImportQueue;
}
```

In `Api/DependencyInjection.cs`, add `services.AddHostedService<ContactImportConsumer>();`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ImportServiceTests"`
Expected: PASS (3 tests). Then run `dotnet build ElectroMarketing.slnx -p:CI=true`. Expected: 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(imports): chunked upsert import with per-file history and consumer

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 7: Import and contact GraphQL API

**Files:**
- Create: `src/ElectroMarketing.Api/GraphQl/Imports/ImportFeature.cs`, `src/ElectroMarketing.Api/GraphQl/Contacts/ContactFeature.cs`
- Modify: `src/ElectroMarketing.Auth/Permissions.cs`, `src/ElectroMarketing.DbUtils/MasterData/auth-roles.json`, `web/schema.graphql` (regenerated)
- Test: `tests/ElectroMarketing.Api.Tests/ImportGraphQlTests.cs`

**Interfaces:**
- Consumes: `ImportService`, `CategoryService`, `CsvPreview`, `ColumnMapping` (Task 6).
- Produces the GraphQL fields below.
  - Queries:
    - `importBatches` (paging, filtering, sorting)
    - `categories`
    - `contact(id)`
    - `contactHistory(contactId)` (paging)
  - Mutations, each followed by its payload field:
    - `createCategory(name)` → `category`
    - `previewImport(documentId)` → `csvPreview`
    - `startImport(documentId, categoryId, mapping, culture)` → `importBatch`
  - Permission constants: `Permissions.Contacts.Read = "contacts.read"` and `Permissions.Imports.Write = "imports.write"`.

- [ ] **Step 1: Write the failing API test**

```csharp
// tests/ElectroMarketing.Api.Tests/ImportGraphQlTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace ElectroMarketing.Api.Tests;

/// <summary>Upload → preview → category → start → outbox → RabbitMQ consumer → Done.</summary>
public sealed class ImportGraphQlTests(ApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Csv_upload_preview_start_and_consumer_import()
    {
        using var client = api.ClientFor($"importer-{Guid.NewGuid():N}@local.test", "user");
        var bytes = Encoding.UTF8.GetBytes($"Name;E-Mail;Telefon\nMax;max-{Guid.NewGuid():N}@x.de;\n");

        var documentId = (await client.Gql($$"""
            mutation { requestDocumentUpload(input: { fileName: "c.csv", contentType: "text/csv", sizeBytes: {{bytes.Length}} }) {
              documentUploadPayload { document { id } } } }
            """)).Data()["requestDocumentUpload"]!["documentUploadPayload"]!["document"]!["id"]!.GetValue<string>();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        (await client.PutAsync($"/documents/{documentId}/content", content, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await client.Gql($$"""mutation { confirmDocumentUpload(input: { id: "{{documentId}}" }) { document { id } } }""");

        var preview = (await client.Gql($$"""
            mutation { previewImport(input: { documentId: "{{documentId}}" }) {
              csvPreview { delimiter headers suggestedMapping { name email phone } } errors { __typename } } }
            """)).Data()["previewImport"]!["csvPreview"]!;
        preview["delimiter"]!.GetValue<string>().ShouldBe(";");
        preview["suggestedMapping"]!["email"]!.GetValue<string>().ShouldBe("E-Mail");

        var categoryId = (await client.Gql($$"""
            mutation { createCategory(input: { name: "Fair {{Guid.NewGuid():N}}" }) { category { id } errors { __typename } } }
            """)).Data()["createCategory"]!["category"]!["id"]!.GetValue<string>();

        var invalid = (await client.Gql($$"""
            mutation { startImport(input: { documentId: "{{documentId}}", categoryId: "{{categoryId}}", culture: "de-DE", mapping: { name: "Name" } }) {
              importBatch { id } errors { __typename } } }
            """)).Data()["startImport"]!;
        invalid["errors"]![0]!["__typename"]!.GetValue<string>().ShouldBe("ValidationError");

        var batchId = (await client.Gql($$"""
            mutation { startImport(input: { documentId: "{{documentId}}", categoryId: "{{categoryId}}", culture: "de-DE",
              mapping: { name: "Name", email: "E-Mail", phone: "Telefon" } }) { importBatch { id } errors { __typename } } }
            """)).Data()["startImport"]!["importBatch"]!["id"]!.GetValue<string>();

        JsonNode? batch = null;
        for (var i = 0; i < 50 && batch?["status"]?.GetValue<string>() is not ("DONE" or "FAILED"); i++)
        {
            await Task.Delay(200, Ct);
            batch = (await client.Gql($$"""
                { importBatches(where: { id: { eq: "{{batchId}}" } }) { nodes { status createdCount category { name } } } }
                """)).Data()["importBatches"]!["nodes"]![0];
        }

        batch!["status"]!.GetValue<string>().ShouldBe("DONE");
        batch["createdCount"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public async Task Duplicate_category_name_is_a_conflict()
    {
        using var client = api.ClientFor("cat@local.test", "user");
        var name = $"Dup {Guid.NewGuid():N}";
        const string create = """mutation($n: String!) { createCategory(input: { name: $n }) { category { id } errors { __typename } } }""";

        await client.Gql(create, new { n = name });
        var second = (await client.Gql(create, new { n = name.ToUpperInvariant() })).Data()["createCategory"]!;

        second["errors"]![0]!["__typename"]!.GetValue<string>().ShouldBe("ConflictError");
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --project tests/ElectroMarketing.Api.Tests --filter-class "*ImportGraphQlTests"`
Expected: FAIL. The GraphQL errors read "The field `previewImport` does not exist".

- [ ] **Step 3: Add permissions**

In `Permissions.cs`:

```csharp
public static class Contacts
{
    public const string Read = "contacts.read";
}

public static class Imports
{
    public const string Write = "imports.write";
}
```

In `auth-roles.json`, append `"contacts.read", "imports.write"` to the permissions of both `user` and `platform-admin`.

- [ ] **Step 4: Implement the GraphQL feature classes**

```csharp
// src/ElectroMarketing.Api/GraphQl/Imports/ImportFeature.cs
using ElectroMarketing.Api.GraphQl.Errors;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Imports;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Contacts;
using HotChocolate.Authorization;

namespace ElectroMarketing.Api.GraphQl.Imports;

[ExtendObjectType(typeof(RootQuery))]
public class ImportQueries : RootQuery
{
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ImportBatch> GetImportBatches([Service] IRepository<ImportBatch, Guid> batches) =>
        batches.Query().OrderByDescending(b => b.CreatedAtUtc);

    [Authorize(Policy = Permissions.Contacts.Read)]
    [UseProjection]
    public IQueryable<Category> GetCategories([Service] IRepository<Category, Guid> categories) => categories.Query().OrderBy(c => c.Name);
}

[ExtendObjectType(typeof(RootMutation))]
public class ImportMutations : RootMutation
{
    [Authorize(Policy = Permissions.Imports.Write)]
    [Error<ValidationError>]
    [Error<ConflictError>]
    public Task<Category> CreateCategory(string name, [Service] CategoryService categories, CancellationToken ct) =>
        categories.CreateAsync(new CreateCategoryDto(name), ct);

    /// <summary>Delimiter, encoding, headers, first 10 rows and a suggested column mapping of an uploaded CSV.</summary>
    [Authorize(Policy = Permissions.Imports.Write)]
    [Error<NotFoundError>]
    public Task<CsvPreview> PreviewImport(Guid documentId, [Service] ImportService imports, CancellationToken ct) =>
        imports.PreviewAsync(documentId, ct);

    /// <summary>Queues the import; poll <c>importBatches</c> for the status.</summary>
    [Authorize(Policy = Permissions.Imports.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<ImportBatch> StartImport(Guid documentId, Guid categoryId, ColumnMapping mapping, string culture, [Service] ImportService imports, CancellationToken ct) =>
        imports.StartAsync(new StartImportDto(documentId, categoryId, mapping, culture), ct);
}

public sealed class ImportBatchType : ObjectType<ImportBatch>
{
    protected override void Configure(IObjectTypeDescriptor<ImportBatch> descriptor)
    {
        descriptor.Ignore(b => b.EventName);
        descriptor.Ignore(b => b.ToChangedEvent(default));
        descriptor.Field(b => b.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}

public sealed class CategoryType : ObjectType<Category>
{
    protected override void Configure(IObjectTypeDescriptor<Category> descriptor)
    {
        descriptor.Ignore(c => c.NormalizedName);
        descriptor.Field(c => c.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}
```

```csharp
// src/ElectroMarketing.Api/GraphQl/Contacts/ContactFeature.cs
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Contacts;
using HotChocolate.Authorization;

namespace ElectroMarketing.Api.GraphQl.Contacts;

[ExtendObjectType(typeof(RootQuery))]
public class ContactQueries : RootQuery
{
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<Contact> GetContact(Guid id, [Service] IRepository<Contact, Guid> contacts) => contacts.Query().Where(c => c.Id == id);

    /// <summary>Every CSV row applied to the contact, newest first (UUID v7 ids sort by time).</summary>
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100)]
    [UseProjection]
    public IQueryable<ContactImport> GetContactHistory(Guid contactId, [Service] IRepository<ContactImport, Guid> imports) =>
        imports.Query().Where(i => i.ContactId == contactId).OrderByDescending(i => i.Id);
}

public sealed class ContactType : ObjectType<Contact>
{
    protected override void Configure(IObjectTypeDescriptor<Contact> descriptor)
    {
        descriptor.Ignore(c => c.SearchText);
        descriptor.Ignore(c => c.Imports);
        descriptor.Field(c => c.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}
```

- [ ] **Step 5: Run the API tests and regenerate the contract**

Run: `dotnet test --project tests/ElectroMarketing.Api.Tests`
Expected: PASS, including `SchemaTests`. If `SchemaTests` compares against a snapshot, update the snapshot as that test's message says.

Then run the contract regeneration command from Global Constraints. Expected: `web/schema.graphql` contains `startImport`.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(imports): GraphQL for categories, preview, start, batches and contact history

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 8: Import wizard, imports page, contact detail (web)

**Files:**
- Create: `web/src/components/ImportWizard.tsx`, `web/src/components/ImportsPage.tsx`, `web/src/components/ContactDetailPage.tsx`, `web/src/app/app/imports/page.tsx`, `web/src/app/app/contacts/[id]/page.tsx`
- Modify: `web/src/components/AppNav.tsx`, `web/src/app/app/page.tsx`
- Test: `web/src/components/ImportWizard.test.tsx`

**Interfaces:**
- Consumes: the GraphQL fields from Task 7, `uploadWithProgress` (`web/src/lib/upload.ts`), `formErrors` (`web/src/lib/errors.ts`) and `execute` (`web/src/lib/graphql.ts`).
- Produces: routes `/app/imports` and `/app/contacts/[id]`. It also exports `ImportWizard({ onStarted })`, plus `ImportsPage` and `ContactDetailPage({ id })`, and `messageOf(errors)` from `ImportWizard.tsx`, which later tasks reuse.

- [ ] **Step 1: Write the failing component test**

```tsx
// web/src/components/ImportWizard.test.tsx
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { graphql, http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/server";
import { ImportWizard } from "./ImportWizard";

describe("ImportWizard", () => {
  it("uploads as text/csv, applies the suggested mapping and starts with a new category", async () => {
    let uploadedType: string | null = null;
    let startInput: Record<string, unknown> | null = null;
    server.use(
      graphql.query("ImportCategories", () => HttpResponse.json({ data: { categories: [] } })),
      graphql.mutation("RequestCsvUpload", () =>
        HttpResponse.json({
          data: { requestDocumentUpload: { documentUploadPayload: { uploadUrl: "/api/documents/d1/content", document: { id: "d1" } }, errors: null } },
        }),
      ),
      http.put("*/api/documents/d1/content", ({ request }) => {
        uploadedType = request.headers.get("content-type");
        return new HttpResponse(null, { status: 204 });
      }),
      graphql.mutation("ConfirmCsvUpload", () => HttpResponse.json({ data: { confirmDocumentUpload: { errors: null } } })),
      graphql.mutation("PreviewImport", () =>
        HttpResponse.json({
          data: {
            previewImport: {
              errors: null,
              csvPreview: {
                delimiter: ";",
                encoding: "windows-1252",
                headers: ["Name", "E-Mail"],
                rows: [["Max", "max@x.de"]],
                suggestedMapping: { name: "Name", email: "E-Mail", phone: null, street: null, postalCode: null, city: null, country: null, obtainedOn: null },
              },
            },
          },
        }),
      ),
      graphql.mutation("CreateImportCategory", () =>
        HttpResponse.json({ data: { createCategory: { category: { id: "c1", name: "Fair" }, errors: null } } }),
      ),
      graphql.mutation("StartImport", ({ variables }) => {
        startInput = variables.input as Record<string, unknown>;
        return HttpResponse.json({ data: { startImport: { importBatch: { id: "b1" }, errors: null } } });
      }),
    );
    let started = false;
    render(<ImportWizard onStarted={() => (started = true)} />);

    const file = new File(["Name;E-Mail\nMax;max@x.de\n"], "contacts.csv", { type: "application/vnd.ms-excel" });
    fireEvent.change(screen.getByLabelText("CSV file"), { target: { files: [file] } });
    expect(await screen.findByText("max@x.de")).toBeTruthy();
    expect((screen.getByLabelText("E-Mail column") as HTMLSelectElement).value).toBe("E-Mail");

    fireEvent.change(screen.getByLabelText("New category"), { target: { value: "Fair" } });
    fireEvent.click(screen.getByRole("button", { name: "Start import" }));

    await waitFor(() => expect(started).toBe(true));
    expect(uploadedType).toBe("text/csv");
    expect(startInput).toEqual({
      documentId: "d1",
      categoryId: "c1",
      culture: "de-DE",
      mapping: { name: "Name", email: "E-Mail", phone: null, street: null, postalCode: null, city: null, country: null, obtainedOn: null },
    });
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && bun run test src/components/ImportWizard.test.tsx`
Expected: FAIL ("Failed to resolve import ./ImportWizard").

- [ ] **Step 3: Implement the wizard**

```tsx
// web/src/components/ImportWizard.tsx
"use client";

import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { PreviewImportMutation } from "@/gql/graphql";
import { formErrors } from "@/lib/errors";
import { execute } from "@/lib/graphql";
import { uploadWithProgress } from "@/lib/upload";

const ImportCategoriesDocument = graphql(`
  query ImportCategories {
    categories {
      id
      name
    }
  }
`);

const CreateImportCategoryDocument = graphql(`
  mutation CreateImportCategory($input: CreateCategoryInput!) {
    createCategory(input: $input) {
      category {
        id
        name
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const RequestCsvUploadDocument = graphql(`
  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {
    requestDocumentUpload(input: $input) {
      documentUploadPayload {
        uploadUrl
        document {
          id
        }
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const ConfirmCsvUploadDocument = graphql(`
  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {
    confirmDocumentUpload(input: $input) {
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const PreviewImportDocument = graphql(`
  mutation PreviewImport($input: PreviewImportInput!) {
    previewImport(input: $input) {
      csvPreview {
        delimiter
        encoding
        headers
        rows
        suggestedMapping {
          name
          email
          phone
          street
          postalCode
          city
          country
          obtainedOn
        }
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const StartImportDocument = graphql(`
  mutation StartImport($input: StartImportInput!) {
    startImport(input: $input) {
      importBatch {
        id
      }
      errors {
        __typename
        ... on Error {
          message
        }
        ... on ValidationError {
          fields {
            field
            messages
          }
        }
      }
    }
  }
`);

const fields = [
  ["name", "Name"],
  ["email", "E-Mail"],
  ["phone", "Phone"],
  ["street", "Street"],
  ["postalCode", "Postal code"],
  ["city", "City"],
  ["country", "Country"],
  ["obtainedOn", "Date obtained"],
] as const;
type Field = (typeof fields)[number][0];
type Mapping = Record<Field, string | null>;
type Preview = NonNullable<PreviewImportMutation["previewImport"]["csvPreview"]>;

const cultures = ["de-DE", "de-AT", "de-CH", "en-GB", "en-US"];

/** One line from typed mutation errors (summary plus field messages), or null when there are none. */
export function messageOf(errors: Parameters<typeof formErrors>[0]): string | null {
  const e = formErrors(errors);
  return e ? [e.message, ...Object.values(e.fields)].filter(Boolean).join(" ") : null;
}

export function ImportWizard({ onStarted }: { onStarted: () => void }) {
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);
  const [categoryId, setCategoryId] = useState("");
  const [newCategory, setNewCategory] = useState("");
  const [documentId, setDocumentId] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [mapping, setMapping] = useState<Mapping | null>(null);
  const [culture, setCulture] = useState("de-DE");
  const [progress, setProgress] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    execute(ImportCategoriesDocument).then(
      (d) => setCategories(d.categories),
      (e: Error) => setError(e.message),
    );
  }, []);

  async function run(action: () => Promise<string | null>) {
    setBusy(true);
    setError(null);
    try {
      setError(await action());
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  const upload = (picked: File) =>
    run(async () => {
      // Excel on Windows reports .csv files as application/vnd.ms-excel; the Api accepts text/csv only.
      const file = new File([picked], picked.name, { type: "text/csv" });
      const { requestDocumentUpload } = await execute(RequestCsvUploadDocument, {
        input: { fileName: file.name, contentType: file.type, sizeBytes: file.size },
      });
      const payload = requestDocumentUpload.documentUploadPayload;
      if (!payload) return messageOf(requestDocumentUpload.errors) ?? "Upload refused.";
      await uploadWithProgress(payload.uploadUrl, file, setProgress);
      const { confirmDocumentUpload } = await execute(ConfirmCsvUploadDocument, { input: { id: payload.document.id } });
      if (confirmDocumentUpload.errors?.length) return messageOf(confirmDocumentUpload.errors);
      const { previewImport } = await execute(PreviewImportDocument, { input: { documentId: payload.document.id } });
      const result = previewImport.csvPreview;
      if (!result) return messageOf(previewImport.errors) ?? "The file could not be read.";
      setDocumentId(payload.document.id);
      setPreview(result);
      setMapping(Object.fromEntries(fields.map(([f]) => [f, result.suggestedMapping[f] ?? null])) as Mapping);
      return null;
    });

  const start = () =>
    run(async () => {
      let id = categoryId;
      if (!id) {
        if (!newCategory.trim()) return "Pick or create a category.";
        const { createCategory } = await execute(CreateImportCategoryDocument, { input: { name: newCategory } });
        if (!createCategory.category) return messageOf(createCategory.errors);
        id = createCategory.category.id;
      }
      const { startImport } = await execute(StartImportDocument, {
        input: { documentId: documentId!, categoryId: id, culture, mapping: mapping! },
      });
      if (!startImport.importBatch) return messageOf(startImport.errors);
      setPreview(null);
      setDocumentId(null);
      setProgress(null);
      onStarted();
      return null;
    });

  return (
    <section aria-label="New import" className="space-y-4 rounded-lg border border-border p-4">
      <h2 className="text-lg font-semibold">New import</h2>
      <label className="block text-sm">
        CSV file
        <input
          type="file"
          accept=".csv,text/csv"
          disabled={busy}
          onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])}
          className="mt-1 block"
        />
      </label>
      {progress !== null && !preview && <progress value={progress} max={100} className="w-full" />}
      {preview && mapping && (
        <>
          <p className="text-sm text-muted">
            Delimiter “{preview.delimiter}”, encoding {preview.encoding}, {preview.headers.length} columns.
          </p>
          <div className="grid gap-2 sm:grid-cols-4">
            {fields.map(([field, label]) => (
              <label key={field} className="text-sm">
                {label} column
                <select
                  aria-label={`${label} column`}
                  value={mapping[field] ?? ""}
                  onChange={(e) => setMapping({ ...mapping, [field]: e.target.value || null })}
                  className="mt-1 block w-full rounded-md border border-border bg-background px-2 py-1"
                >
                  <option value="">— not in file —</option>
                  {preview.headers.map((h) => (
                    <option key={h} value={h}>
                      {h}
                    </option>
                  ))}
                </select>
              </label>
            ))}
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr>
                  {preview.headers.map((h) => (
                    <th key={h} className="px-2 py-1 text-left">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((row, i) => (
                  <tr key={i} className="border-t border-border">
                    {row.map((cell, j) => (
                      <td key={j} className="px-2 py-1">
                        {cell}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <label className="text-sm">
              Category
              <select
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value)}
                className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
              >
                <option value="">— new category —</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </label>
            {!categoryId && (
              <label className="text-sm">
                New category
                <input
                  value={newCategory}
                  onChange={(e) => setNewCategory(e.target.value)}
                  className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
                />
              </label>
            )}
            <label className="text-sm">
              Date and phone format
              <select
                value={culture}
                onChange={(e) => setCulture(e.target.value)}
                className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
              >
                {cultures.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </label>
            <button
              type="button"
              disabled={busy}
              onClick={start}
              className="rounded-md bg-primary px-4 py-1.5 text-on-primary disabled:opacity-50"
            >
              Start import
            </button>
          </div>
        </>
      )}
      {error && (
        <p role="alert" className="text-sm text-danger">
          {error}
        </p>
      )}
    </section>
  );
}
```

If the template's token for error text is not `text-danger`, use the token that `ProductsPage.tsx` used for errors (check `web/src/app/globals.css`).

- [ ] **Step 4: Run the test**

Run: `cd web && bun run codegen && bun run test src/components/ImportWizard.test.tsx`
Expected: PASS.

- [ ] **Step 5: Implement the imports page and the contact detail page**

```tsx
// web/src/components/ImportsPage.tsx
"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { ImportBatchesQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { ImportWizard } from "./ImportWizard";

const ImportBatchesDocument = graphql(`
  query ImportBatches {
    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {
      nodes {
        id
        fileName
        documentId
        status
        culture
        rowCount
        createdCount
        updatedCount
        unchangedCount
        skippedCount
        errorCount
        createdAtUtc
        createdBy
        category {
          name
        }
      }
    }
  }
`);

const ImportErrorsDocument = graphql(`
  query ImportErrors($id: UUID!) {
    importBatches(where: { id: { eq: $id } }) {
      nodes {
        errors {
          row
          message
        }
      }
    }
  }
`);

type Batch = NonNullable<NonNullable<ImportBatchesQuery["importBatches"]>["nodes"]>[number];

const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export function ImportsPage() {
  const [batches, setBatches] = useState<Batch[] | null>(null);
  const [openErrors, setOpenErrors] = useState<{ id: string; errors: { row: number; message: string }[] } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    () =>
      execute(ImportBatchesDocument).then(
        (d) => setBatches(d.importBatches?.nodes ?? []),
        (e: Error) => setError(e.message),
      ),
    [],
  );

  useEffect(() => {
    load();
  }, [load]);

  // Poll while an import is queued or running.
  const busy = batches?.some((b) => b.status === "PENDING" || b.status === "RUNNING");
  useEffect(() => {
    if (!busy) return;
    const timer = setInterval(load, 3000);
    return () => clearInterval(timer);
  }, [busy, load]);

  async function showErrors(id: string) {
    const d = await execute(ImportErrorsDocument, { id });
    setOpenErrors({ id, errors: d.importBatches?.nodes?.[0]?.errors ?? [] });
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Imports</h1>
      <ImportWizard onStarted={load} />
      {error && <p role="alert">{error}</p>}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">File</th>
            <th className="px-2 py-1">Category</th>
            <th className="px-2 py-1">Status</th>
            <th className="px-2 py-1">Rows</th>
            <th className="px-2 py-1">New / updated / unchanged / skipped</th>
            <th className="px-2 py-1">Imported</th>
            <th className="px-2 py-1" />
          </tr>
        </thead>
        <tbody>
          {batches?.map((b) => (
            <tr key={b.id} className="border-t border-border align-top">
              <td className="px-2 py-1">
                <a href={`/api/documents/${b.documentId}/content`} className="underline">
                  {b.fileName}
                </a>
              </td>
              <td className="px-2 py-1">{b.category?.name}</td>
              <td className="px-2 py-1">{b.status.toLowerCase()}</td>
              <td className="px-2 py-1">{b.rowCount}</td>
              <td className="px-2 py-1">
                {b.createdCount} / {b.updatedCount} / {b.unchangedCount} / {b.skippedCount}
              </td>
              <td className="px-2 py-1">
                {dateTime.format(new Date(b.createdAtUtc))} · {b.createdBy}
              </td>
              <td className="space-x-2 px-2 py-1 whitespace-nowrap">
                <Link href={`/app/contacts?batch=${b.id}`} className="underline">
                  Contacts
                </Link>
                {b.errorCount > 0 && (
                  <button type="button" onClick={() => showErrors(b.id)} className="underline">
                    {b.errorCount} warnings
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {openErrors && (
        <section aria-label="Import warnings" className="rounded-lg border border-border p-4">
          <button type="button" onClick={() => setOpenErrors(null)} className="float-right underline">
            Close
          </button>
          <ul className="space-y-1 text-sm">
            {openErrors.errors.map((e, i) => (
              <li key={i}>
                {e.row > 0 ? `Row ${e.row}: ` : ""}
                {e.message}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
```

```tsx
// web/src/components/ContactDetailPage.tsx
"use client";

import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { ContactDetailQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";

const ContactDetailDocument = graphql(`
  query ContactDetail($id: UUID!) {
    contact(id: $id) {
      id
      name
      email
      phone
      phoneE164
      street
      postalCode
      city
      country
      obtainedOn
      emailOptOut
      smsOptOut
      createdAtUtc
    }
    contactHistory(contactId: $id, first: 100) {
      nodes {
        id
        rowNumber
        change
        changes {
          field
          old
          new
        }
        importBatch {
          id
          fileName
          documentId
          createdAtUtc
          createdBy
          category {
            name
          }
        }
      }
    }
  }
`);

const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export function ContactDetailPage({ id }: { id: string }) {
  const [data, setData] = useState<ContactDetailQuery | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    execute(ContactDetailDocument, { id }).then(setData, (e: Error) => setError(e.message));
  }, [id]);

  if (error) return <p role="alert">{error}</p>;
  if (!data) return <p>Loading…</p>;
  const c = data.contact;
  if (!c) return <p>Contact not found.</p>;

  const rows: [string, string | null | undefined][] = [
    ["E-Mail", c.email + (c.emailOptOut ? " (unsubscribed)" : "")],
    ["Phone", c.phone ? `${c.phone} (${c.phoneE164})${c.smsOptOut ? " (unsubscribed)" : ""}` : null],
    ["Address", [c.street, [c.postalCode, c.city].filter(Boolean).join(" "), c.country].filter(Boolean).join(", ")],
    ["Obtained", c.obtainedOn],
  ];

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">{c.name ?? c.email ?? c.phone}</h1>
      <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted">{label}</dt>
            <dd>{value || "—"}</dd>
          </div>
        ))}
      </dl>
      <section>
        <h2 className="text-lg font-semibold">Import history</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left">
              <th className="px-2 py-1">File</th>
              <th className="px-2 py-1">Category</th>
              <th className="px-2 py-1">Imported</th>
              <th className="px-2 py-1">Result</th>
              <th className="px-2 py-1">Changes</th>
            </tr>
          </thead>
          <tbody>
            {data.contactHistory?.nodes?.map((h) => (
              <tr key={h.id} className="border-t border-border align-top">
                <td className="px-2 py-1">
                  <a href={`/api/documents/${h.importBatch?.documentId}/content`} className="underline">
                    {h.importBatch?.fileName}
                  </a>{" "}
                  (row {h.rowNumber})
                </td>
                <td className="px-2 py-1">{h.importBatch?.category?.name}</td>
                <td className="px-2 py-1">
                  {h.importBatch && dateTime.format(new Date(h.importBatch.createdAtUtc))} · {h.importBatch?.createdBy}
                </td>
                <td className="px-2 py-1">{h.change.toLowerCase()}</td>
                <td className="px-2 py-1">
                  {h.changes.map((ch) => (
                    <div key={ch.field}>
                      {ch.field}: {ch.old ?? "—"} → {ch.new ?? "—"}
                    </div>
                  ))}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
```

Routes:

```tsx
// web/src/app/app/imports/page.tsx
import { ImportsPage } from "@/components/ImportsPage";

export const metadata = { title: "Imports" };

export default function Page() {
  return <ImportsPage />;
}
```

```tsx
// web/src/app/app/contacts/[id]/page.tsx
import { ContactDetailPage } from "@/components/ContactDetailPage";

export const metadata = { title: "Contact" };

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  return <ContactDetailPage id={(await params).id} />;
}
```

In `AppNav.tsx`, change `links` to:

```ts
const links = [
  { href: "/app/imports", label: "Imports", permission: "imports.write" },
  { href: "/app/documents", label: "Documents", permission: "documents.read" },
  { href: "/app/admin/queues", label: "Admin · Queues", permission: "queues.manage" },
] as const;
```

In `web/src/app/app/page.tsx`, change the redirect target to `/app/imports`. Task 11 changes it to `/app/contacts`.

- [ ] **Step 6: Verify**

Run: `cd web && bun run lint && bun run typecheck && bun run test`
Expected: all pass.

Then start the AppHost and use agent-browser:
1. Import a `;`-separated Windows-1252 CSV with 3 contacts into a new category "Fair 2026".
2. Import a second CSV that changes the city of 2 of them.
3. Open a changed contact via `/app/contacts/<id>`. Its history shows both files, with the City diff on the second.

Find the id in pgAdmin (`Contacts` table) until Task 11 adds the list page.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(web): import wizard, imports page and contact history

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

# Milestone 2 — Search & lists

### Task 9: Contact search

**Files:**
- Create: `src/ElectroMarketing.Application/Contacts/ContactFilter.cs`, `src/ElectroMarketing.Infrastructure/Persistence/ContactSearch.cs`
- Modify: `src/ElectroMarketing.Api/GraphQl/Contacts/ContactFeature.cs`
- Test: `tests/ElectroMarketing.Infrastructure.Tests/ContactSearchTests.cs`

**Interfaces:**
- Consumes: `Contact.SearchText` and `Contact.Imports` (Task 3).
- Produces:
  - `ContactFilter(string? Search = null, IReadOnlyList<Guid>? CategoryIds = null, IReadOnlyList<Guid>? BatchIds = null, DateOnly? ObtainedFrom = null, DateOnly? ObtainedTo = null)`
  - `ContactSearch.Apply(IQueryable<Contact>, ContactFilter) : IQueryable<Contact>`
  - `ContactSearch.Terms(string?) : IReadOnlyList<string>`
  - GraphQL `contacts(filter: ContactFilterInput)` (paging with totalCount, sorting).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/ContactSearchTests.cs
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class ContactSearchTests(PostgresFixture db) : IAsyncLifetime
{
    private static readonly Category Trade = new() { Name = "Trade", NormalizedName = "TRADE" };
    private static readonly Category Web = new() { Name = "Web", NormalizedName = "WEB" };
    private ImportBatch first = null!;
    private ImportBatch second = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        var document = new Document { OwnerId = Guid.CreateVersion7(), FileName = "f.csv", ContentType = "text/csv" };
        first = Batch(Trade.Id, document.Id);
        second = Batch(Web.Id, document.Id);
        await using var ctx = db.Create();
        ctx.AddRange(Trade, Web, document, first, second);
        Add(ctx, first, "Jörg Müller", "j@x.de", "0170 1234567", "+491701234567", "Köln", new DateOnly(2026, 3, 1));
        Add(ctx, second, "Eva Schmidt", "eva@y.com", null, null, "Bonn", new DateOnly(2026, 5, 1));
        Add(ctx, second, "Max 50% Mustermann", "max@x.de", null, null, "Köln", null);
        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ImportBatch Batch(Guid categoryId, Guid documentId) =>
        new() { CategoryId = categoryId, DocumentId = documentId, FileName = "f.csv", Culture = "de-DE", Mapping = new ColumnMapping() };

    private static void Add(AppDbContext ctx, ImportBatch batch, string name, string email, string? phone, string? e164, string city, DateOnly? on)
    {
        var contact = new Contact { Name = name, Email = email, Phone = phone, PhoneE164 = e164, City = city, ObtainedOn = on };
        contact.RefreshSearchText();
        ctx.Contacts.Add(contact);
        ctx.ContactImports.Add(new ContactImport { ContactId = contact.Id, ImportBatchId = batch.Id, RowNumber = 2, Change = ContactChange.Created });
    }

    private async Task<List<string?>> Names(ContactFilter filter)
    {
        await using var ctx = db.Create();
        return await ContactSearch.Apply(ctx.Contacts, filter).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync(Ct);
    }

    [Theory]
    [InlineData("köln", "Jörg Müller|Max 50% Mustermann")]
    [InlineData("KÖLN müller", "Jörg Müller")]
    [InlineData("1234567", "Jörg Müller")]
    [InlineData("0170-1234", "Jörg Müller")]
    [InlineData("+49170", "Jörg Müller")]
    [InlineData("x.de", "Jörg Müller|Max 50% Mustermann")]
    [InlineData("50%", "Max 50% Mustermann")]
    [InlineData("5_%", "")]
    [InlineData("nobody", "")]
    public async Task Every_term_must_match_some_field(string search, string expected) =>
        (await Names(new ContactFilter(Search: search))).ShouldBe(expected.Length == 0 ? [] : expected.Split('|'));

    [Fact]
    public async Task Filters_by_category_batch_and_date()
    {
        (await Names(new ContactFilter(CategoryIds: [Web.Id]))).ShouldBe(["Eva Schmidt", "Max 50% Mustermann"]);
        (await Names(new ContactFilter(BatchIds: [first.Id]))).ShouldBe(["Jörg Müller"]);
        (await Names(new ContactFilter(ObtainedFrom: new DateOnly(2026, 4, 1)))).ShouldBe(["Eva Schmidt"]);
        (await Names(new ContactFilter(Search: "köln", CategoryIds: [Web.Id]))).ShouldBe(["Max 50% Mustermann"]);
    }

    [Fact]
    public void Terms_are_lower_case_and_phone_like_terms_become_digits() =>
        ContactSearch.Terms("  Köln  0170-1234 (030) KÖLN ").ShouldBe(["köln", "01701234", "030"]);
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ContactSearchTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement**

```csharp
// src/ElectroMarketing.Application/Contacts/ContactFilter.cs
namespace ElectroMarketing.Application.Contacts;

/// <summary>
/// <see cref="Search"/>: whitespace-separated terms; every term must appear in some field (substring, case-insensitive).
/// Category and batch filters match contacts touched by any import of those.
/// </summary>
public sealed record ContactFilter(
    string? Search = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    IReadOnlyList<Guid>? BatchIds = null,
    DateOnly? ObtainedFrom = null,
    DateOnly? ObtainedTo = null);
```

```csharp
// src/ElectroMarketing.Infrastructure/Persistence/ContactSearch.cs
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

/// <summary>LIKE on the lower-case <see cref="Contact.SearchText"/>; the pg_trgm GIN index serves terms of 3+ characters.</summary>
public static class ContactSearch
{
    private const int MaxTerms = 8;

    public static IQueryable<Contact> Apply(IQueryable<Contact> query, ContactFilter filter)
    {
        foreach (var term in Terms(filter.Search))
        {
            var pattern = "%" + Escape(term) + "%";
            query = query.Where(c => EF.Functions.Like(c.SearchText, pattern, "\\"));
        }

        if (filter.CategoryIds is { Count: > 0 } categories)
        {
            query = query.Where(c => c.Imports.Any(i => categories.Contains(i.ImportBatch!.CategoryId)));
        }

        if (filter.BatchIds is { Count: > 0 } batches)
        {
            query = query.Where(c => c.Imports.Any(i => batches.Contains(i.ImportBatchId)));
        }

        if (filter.ObtainedFrom is { } from)
        {
            query = query.Where(c => c.ObtainedOn >= from);
        }

        if (filter.ObtainedTo is { } to)
        {
            query = query.Where(c => c.ObtainedOn <= to);
        }

        return query;
    }

    /// <summary>Lower-case distinct terms; a phone-like term ("0170-1234", "(030)") keeps only its digits to match the stored phone digits.</summary>
    public static IReadOnlyList<string> Terms(string? search) =>
        (search ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Select(t => IsPhoneLike(t) ? new string(t.Where(char.IsAsciiDigit).ToArray()) : t)
            .Where(t => t.Length > 0)
            .Distinct()
            .Take(MaxTerms)
            .ToList();

    private static bool IsPhoneLike(string term) =>
        term.Any(char.IsAsciiDigit) && term.All(ch => char.IsAsciiDigit(ch) || ch is '+' or '-' or '/' or '(' or ')');

    private static string Escape(string term) => term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
```

Add to `ContactQueries` in `ContactFeature.cs`. It needs `using ElectroMarketing.Application.Contacts;`, `using ElectroMarketing.Infrastructure.Persistence;` and `using Microsoft.EntityFrameworkCore;`.

```csharp
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseSorting]
    public IQueryable<Contact> GetContacts(ContactFilter? filter, [Service] AppDbContext db) =>
        ContactSearch.Apply(db.Contacts.AsNoTracking(), filter ?? new ContactFilter()).OrderBy(c => c.Name).ThenBy(c => c.Id);
```

The `"+49170"` case works this way: `IsPhoneLike` turns the term into `49170`, which is a substring of the stored `+491701234567`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ContactSearchTests"`
Expected: PASS. Then run `dotnet test --project tests/ElectroMarketing.Api.Tests` (schema still valid) and regenerate the contract.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(contacts): multi-term search over every field with category, batch and date filters

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 10: Contact lists

**Files:**
- Create: `src/ElectroMarketing.Domain/Lists/ContactList.cs`, `src/ElectroMarketing.Application/Lists/ListService.cs`, `src/ElectroMarketing.Infrastructure/Persistence/ListStore.cs`, `src/ElectroMarketing.Api/GraphQl/Lists/ListFeature.cs`
- Modify: `AppDbContext.cs`, `Configurations.cs`, both `DependencyInjection.cs` (Application and Infrastructure), `Permissions.cs`, `auth-roles.json`
- Test: `tests/ElectroMarketing.Infrastructure.Tests/ListServiceTests.cs`

**Interfaces:**
- Consumes: `ContactFilter`, `ContactSearch` (Task 9).
- Produces:
  - Domain: `ContactList { Name, SourceBatchId, Members }` and `ContactListMember { ContactListId, ContactId, Contact, AddedAtUtc }`.
  - `ListNameDto(string Name)`.
  - `ListService` methods: `CreateAsync`, `UpdateAsync`, `DeleteAsync` (from `CrudService`), plus `CreateFromBatchAsync(Guid, string, ct)`, `AddAsync(Guid listId, IReadOnlyCollection<Guid>? contactIds, ContactFilter? filter, ct) : Task<int>` and `RemoveAsync(Guid, IReadOnlyCollection<Guid>, ct) : Task<int>`.
  - `Permissions.Lists.Write = "lists.write"`.
  - GraphQL queries: `contactLists` (with `memberCount`) and `contactListMembers(listId)`.
  - GraphQL mutations (payload field after the arrow):
    - `createContactList(name)` → `contactList`
    - `createContactListFromBatch(batchId, name)` → `contactList`
    - `renameContactList(id, name)` → `contactList`
    - `deleteContactList(id)` → `uuid`
    - `addToContactList(listId, contactIds?, filter?)` → `count`
    - `removeFromContactList(listId, contactIds)` → `count`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/ListServiceTests.cs
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Lists;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Domain.Exceptions;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class ListServiceTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly List<Contact> contacts = [];
    private ImportBatch batch = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        var category = new Category { Name = "C", NormalizedName = "C" };
        var document = new Document { OwnerId = Guid.CreateVersion7(), FileName = "f.csv", ContentType = "text/csv" };
        batch = new ImportBatch { CategoryId = category.Id, DocumentId = document.Id, FileName = "f.csv", Culture = "de-DE", Mapping = new ColumnMapping() };
        await using var ctx = db.Create();
        ctx.AddRange(category, document, batch);
        foreach (var (name, city, inBatch) in new[] { ("Anna", "Köln", true), ("Ben", "Köln", true), ("Cem", "Bonn", false) })
        {
            var c = new Contact { Name = name, Email = $"{name.ToLowerInvariant()}@x.de", City = city };
            c.RefreshSearchText();
            contacts.Add(c);
            ctx.Contacts.Add(c);
            if (inBatch)
            {
                ctx.ContactImports.Add(new ContactImport { ContactId = c.Id, ImportBatchId = batch.Id, RowNumber = 2, Change = ContactChange.Created });
            }
        }

        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ListService Service(AppDbContext ctx) =>
        new(new Repository<ContactList, Guid>(ctx), new Repository<ImportBatch, Guid>(ctx), new ListStore(ctx, TimeProvider.System));

    private async Task<string[]> Members(Guid listId)
    {
        await using var ctx = db.Create();
        return await ctx.ContactListMembers.Where(m => m.ContactListId == listId).Select(m => m.Contact!.Name!).OrderBy(n => n).ToArrayAsync(Ct);
    }

    [Fact]
    public async Task From_batch_then_add_by_filter_and_ids_without_duplicates()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);

        var list = await service.CreateFromBatchAsync(batch.Id, "Fair leads", Ct);
        (await Members(list.Id)).ShouldBe(["Anna", "Ben"]);

        (await service.AddAsync(list.Id, null, new ContactFilter(Search: "köln"), Ct)).ShouldBe(0);
        (await service.AddAsync(list.Id, [contacts[2].Id, contacts[0].Id], null, Ct)).ShouldBe(1);
        (await Members(list.Id)).ShouldBe(["Anna", "Ben", "Cem"]);

        (await service.RemoveAsync(list.Id, [contacts[1].Id], Ct)).ShouldBe(1);
        (await Members(list.Id)).ShouldBe(["Anna", "Cem"]);
    }

    [Fact]
    public async Task Add_needs_exactly_one_source_and_an_existing_list()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var list = await service.CreateAsync(new ListNameDto("Manual"), Ct);

        await Should.ThrowAsync<ValidationException>(() => service.AddAsync(list.Id, null, null, Ct));
        await Should.ThrowAsync<NotFoundException>(() => service.AddAsync(Guid.CreateVersion7(), [contacts[0].Id], null, Ct));
    }

    [Fact]
    public async Task Deleting_a_list_removes_members_not_contacts()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var list = await service.CreateFromBatchAsync(batch.Id, "Temp", Ct);

        await service.DeleteAsync(list.Id, Ct);

        await using var read = db.Create();
        (await read.ContactListMembers.CountAsync(Ct)).ShouldBe(0);
        (await read.Contacts.CountAsync(Ct)).ShouldBe(3);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ListServiceTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement domain, persistence and service**

```csharp
// src/ElectroMarketing.Domain/Lists/ContactList.cs
using ElectroMarketing.Domain.Abstractions;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Domain.Lists;

/// <summary>A named working set of contacts, built from an import lot and/or picked by hand.</summary>
public class ContactList : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>The import lot the list was created from, if any.</summary>
    public Guid? SourceBatchId { get; set; }

    public ICollection<ContactListMember> Members { get; set; } = [];
}

public class ContactListMember
{
    public Guid ContactListId { get; set; }

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; }
}
```

Add `public DbSet<ContactList> ContactLists => Set<ContactList>();` and `public DbSet<ContactListMember> ContactListMembers => Set<ContactListMember>();` to `AppDbContext`, and append to `Configurations.cs`:

```csharp
internal sealed class ContactListConfiguration : IEntityTypeConfiguration<ContactList>
{
    public void Configure(EntityTypeBuilder<ContactList> b)
    {
        b.Property(l => l.Name).HasMaxLength(200);
        b.HasOne<ImportBatch>().WithMany().HasForeignKey(l => l.SourceBatchId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(l => l.Members).WithOne().HasForeignKey(m => m.ContactListId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ContactListMemberConfiguration : IEntityTypeConfiguration<ContactListMember>
{
    public void Configure(EntityTypeBuilder<ContactListMember> b)
    {
        b.HasKey(m => new { m.ContactListId, m.ContactId });
        b.HasOne(m => m.Contact).WithMany().HasForeignKey(m => m.ContactId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(m => m.ContactId);
    }
}
```

```bash
dotnet ef migrations add ContactLists --context AppDbContext --project src/ElectroMarketing.Infrastructure --startup-project src/ElectroMarketing.DbUtils --output-dir Persistence/Migrations
```

```csharp
// src/ElectroMarketing.Application/Lists/ListService.cs
using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Exceptions;
using ElectroMarketing.Domain.Lists;
using DomainValidationException = ElectroMarketing.Domain.Exceptions.ValidationException;

namespace ElectroMarketing.Application.Lists;

public sealed record ListNameDto([property: Required, StringLength(200, MinimumLength = 1)] string Name);

public interface IListStore
{
    /// <summary>Adds the contacts matching <paramref name="filter"/> that are not members yet; returns how many were added.</summary>
    Task<int> AddMembersAsync(Guid listId, ContactFilter filter, CancellationToken ct);

    /// <summary>Adds the existing contacts among <paramref name="contactIds"/> that are not members yet.</summary>
    Task<int> AddMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct);

    Task<int> RemoveMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct);
}

public sealed class ListService(IRepository<ContactList, Guid> lists, IRepository<ImportBatch, Guid> batches, IListStore store)
    : CrudService<ContactList, Guid, ListNameDto, ListNameDto>(lists)
{
    public async Task<ContactList> CreateFromBatchAsync(Guid batchId, string name, CancellationToken ct = default)
    {
        var dto = new ListNameDto(name);
        DtoValidator.Validate(dto);
        _ = await batches.GetByIdAsync(batchId, ct) ?? throw NotFoundException.For<ImportBatch>(batchId);
        var list = Map(dto);
        list.SourceBatchId = batchId;
        list = await Repository.AddAsync(list, ct);
        await store.AddMembersAsync(list.Id, new ContactFilter(BatchIds: [batchId]), ct);
        return list;
    }

    /// <summary>Pass either <paramref name="contactIds"/> (hand-picked) or <paramref name="filter"/> ("add all matching").</summary>
    public async Task<int> AddAsync(Guid listId, IReadOnlyCollection<Guid>? contactIds, ContactFilter? filter, CancellationToken ct = default)
    {
        if ((contactIds is null) == (filter is null))
        {
            throw new DomainValidationException(new Dictionary<string, string[]> { ["contactIds"] = ["Pass either contactIds or filter."] });
        }

        await RequireAsync(listId, ct);
        return filter is not null ? await store.AddMembersAsync(listId, filter, ct) : await store.AddMembersAsync(listId, contactIds!, ct);
    }

    public async Task<int> RemoveAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default)
    {
        await RequireAsync(listId, ct);
        return await store.RemoveMembersAsync(listId, contactIds, ct);
    }

    protected override ContactList Map(ListNameDto dto) => new() { Name = dto.Name.Trim() };

    protected override void Apply(ListNameDto dto, ContactList entity) => entity.Name = dto.Name.Trim();

    private async Task RequireAsync(Guid listId, CancellationToken ct)
    {
        if (!await Repository.ExistsAsync(listId, ct))
        {
            throw NotFoundException.For<ContactList>(listId);
        }
    }
}
```

```csharp
// src/ElectroMarketing.Infrastructure/Persistence/ListStore.cs
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Lists;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Lists;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

public sealed class ListStore(AppDbContext db, TimeProvider time) : IListStore
{
    public Task<int> AddMembersAsync(Guid listId, ContactFilter filter, CancellationToken ct) =>
        AddAsync(listId, ContactSearch.Apply(db.Contacts, filter), ct);

    public Task<int> AddMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct) =>
        AddAsync(listId, db.Contacts.Where(c => contactIds.Contains(c.Id)), ct);

    public Task<int> RemoveMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct) =>
        db.ContactListMembers.Where(m => m.ContactListId == listId && contactIds.Contains(m.ContactId)).ExecuteDeleteAsync(ct);

    // ponytail: loads the new member ids into memory (fine for 100k contacts); switch to INSERT ... SELECT if lists get bigger.
    private async Task<int> AddAsync(Guid listId, IQueryable<Contact> contacts, CancellationToken ct)
    {
        var ids = await contacts
            .Select(c => c.Id)
            .Where(id => !db.ContactListMembers.Any(m => m.ContactListId == listId && m.ContactId == id))
            .ToListAsync(ct);
        var now = time.GetUtcNow();
        db.ContactListMembers.AddRange(ids.Select(id => new ContactListMember { ContactListId = listId, ContactId = id, AddedAtUtc = now }));
        await db.SaveChangesAsync(ct);
        return ids.Count;
    }
}
```

DI:
- Application: `services.AddScoped<ListService>();`
- Infrastructure: `services.AddScoped<IListStore, ListStore>();`
- Permissions: `public static class Lists { public const string Write = "lists.write"; }`. Grant it to `user` and `platform-admin` in `auth-roles.json`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*ListServiceTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: GraphQL feature**

```csharp
// src/ElectroMarketing.Api/GraphQl/Lists/ListFeature.cs
using ElectroMarketing.Api.GraphQl.Errors;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Lists;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using GreenDonut;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.GraphQl.Lists;

[ExtendObjectType(typeof(RootQuery))]
public class ListQueries : RootQuery
{
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseSorting]
    public IQueryable<ContactList> GetContactLists([Service] IRepository<ContactList, Guid> lists) =>
        lists.Query().OrderByDescending(l => l.CreatedAtUtc);

    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    public IQueryable<ContactListMember> GetContactListMembers(Guid listId, [Service] AppDbContext db) =>
        db.ContactListMembers.AsNoTracking().Where(m => m.ContactListId == listId).OrderBy(m => m.Contact!.Name).ThenBy(m => m.ContactId);
}

[ExtendObjectType(typeof(RootMutation))]
public class ListMutations : RootMutation
{
    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<ValidationError>]
    public Task<ContactList> CreateContactList(string name, [Service] ListService lists, CancellationToken ct) =>
        lists.CreateAsync(new ListNameDto(name), ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<ContactList> CreateContactListFromBatch(Guid batchId, string name, [Service] ListService lists, CancellationToken ct) =>
        lists.CreateFromBatchAsync(batchId, name, ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<ContactList> RenameContactList(Guid id, string name, [Service] ListService lists, CancellationToken ct) =>
        lists.UpdateAsync(id, new ListNameDto(name), ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<NotFoundError>]
    public async Task<Guid> DeleteContactList(Guid id, [Service] ListService lists, CancellationToken ct)
    {
        await lists.DeleteAsync(id, ct);
        return id;
    }

    /// <summary>Pass <c>contactIds</c> (selection) or <c>filter</c> (all matching); returns how many were added.</summary>
    [Authorize(Policy = Permissions.Lists.Write)]
    [UseMutationConvention(PayloadFieldName = "count")]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<int> AddToContactList(Guid listId, IReadOnlyList<Guid>? contactIds, ContactFilter? filter, [Service] ListService lists, CancellationToken ct) =>
        lists.AddAsync(listId, contactIds, filter, ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [UseMutationConvention(PayloadFieldName = "count")]
    [Error<NotFoundError>]
    public Task<int> RemoveFromContactList(Guid listId, IReadOnlyList<Guid> contactIds, [Service] ListService lists, CancellationToken ct) =>
        lists.RemoveAsync(listId, contactIds, ct);
}

public sealed class ContactListType : ObjectType<ContactList>
{
    protected override void Configure(IObjectTypeDescriptor<ContactList> descriptor)
    {
        descriptor.Ignore(l => l.Members);
        descriptor.Field(l => l.RowVersion).Type<NonNullType<UnsignedIntType>>();
        descriptor.Field(l => l.Id).IsProjected(true);
        descriptor.Field("memberCount")
            .Type<NonNullType<IntType>>()
            .Resolve(ctx => ctx.DataLoader<IMemberCountByListIdDataLoader>().LoadAsync(ctx.Parent<ContactList>().Id, ctx.RequestAborted));
    }
}

public static class ListDataLoaders
{
    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, int>> GetMemberCountByListIdAsync(
        IReadOnlyList<Guid> ids,
        IServiceScopeFactory scopes,
        CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var counts = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ContactListMembers.AsNoTracking()
            .Where(m => ids.Contains(m.ContactListId))
            .GroupBy(m => m.ContactListId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return ids.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }
}
```

Run: `dotnet test --project tests/ElectroMarketing.Api.Tests` and regenerate the contract.
Expected: PASS, and the schema contains `addToContactList`.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(lists): contact lists from lots, selections and search filters

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 11: Contacts search page and lists page (web)

**Files:**
- Create: `web/src/components/ContactsPage.tsx`, `web/src/components/ListsPage.tsx`, `web/src/app/app/contacts/page.tsx`, `web/src/app/app/lists/page.tsx`
- Modify: `web/src/components/ImportsPage.tsx` (add a "New list" action), `web/src/components/AppNav.tsx`, `web/src/app/app/page.tsx`
- Test: `web/src/components/ContactsPage.test.tsx`

**Interfaces:**
- Consumes: GraphQL from Tasks 9 and 10, and `messageOf` from `ImportWizard.tsx`.
- Produces: routes `/app/contacts` (honours `?batch=<id>`) and `/app/lists`.

- [ ] **Step 1: Write the failing test**

```tsx
// web/src/components/ContactsPage.test.tsx
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { graphql, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { server } from "@/test/server";
import { ContactsPage } from "./ContactsPage";

vi.mock("next/navigation", () => ({ useSearchParams: () => new URLSearchParams("") }));

const contacts = [
  { id: "c1", name: "Jörg Müller", email: "j@x.de", phone: "0170 1234567", city: "Köln", obtainedOn: "2026-03-01", emailOptOut: false, smsOptOut: false },
  { id: "c2", name: "Max Mustermann", email: "max@x.de", phone: null, city: "Köln", obtainedOn: null, emailOptOut: false, smsOptOut: false },
];

describe("ContactsPage", () => {
  it("searches with a debounce and adds the selection to an existing list", async () => {
    const searches: unknown[] = [];
    let added: Record<string, unknown> | null = null;
    server.use(
      graphql.query("ContactsPageData", () =>
        HttpResponse.json({ data: { categories: [], contactLists: { nodes: [{ id: "l1", name: "Fair" }] } } }),
      ),
      graphql.query("Contacts", ({ variables }) => {
        searches.push(variables.filter);
        return HttpResponse.json({
          data: { contacts: { totalCount: 2, pageInfo: { hasNextPage: false, endCursor: null }, nodes: contacts } },
        });
      }),
      graphql.mutation("AddToList", ({ variables }) => {
        added = variables.input as Record<string, unknown>;
        return HttpResponse.json({ data: { addToContactList: { count: 1, errors: null } } });
      }),
    );
    render(<ContactsPage />);

    await screen.findByText("Jörg Müller");
    fireEvent.change(screen.getByLabelText("Search contacts"), { target: { value: "köln" } });
    await waitFor(() => expect(searches.at(-1)).toMatchObject({ search: "köln" }));

    fireEvent.click(screen.getByLabelText("Select Jörg Müller"));
    fireEvent.change(screen.getByLabelText("Target list"), { target: { value: "l1" } });
    fireEvent.click(screen.getByRole("button", { name: "Add selected (1)" }));

    await waitFor(() => expect(added).toEqual({ listId: "l1", contactIds: ["c1"] }));
    expect(await screen.findByText("1 contact added to Fair.")).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && bun run test src/components/ContactsPage.test.tsx`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement the contacts page**

```tsx
// web/src/components/ContactsPage.tsx
"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { graphql } from "@/gql";
import type { ContactsQuery, ContactFilterInput } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";

const ContactsDocument = graphql(`
  query Contacts($filter: ContactFilterInput, $after: String) {
    contacts(filter: $filter, first: 50, after: $after) {
      totalCount
      pageInfo {
        hasNextPage
        endCursor
      }
      nodes {
        id
        name
        email
        phone
        city
        obtainedOn
        emailOptOut
        smsOptOut
      }
    }
  }
`);

const ContactsPageDataDocument = graphql(`
  query ContactsPageData {
    categories {
      id
      name
    }
    contactLists(first: 100) {
      nodes {
        id
        name
      }
    }
  }
`);

const CreateListForSelectionDocument = graphql(`
  mutation CreateListForSelection($input: CreateContactListInput!) {
    createContactList(input: $input) {
      contactList {
        id
        name
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const AddToListDocument = graphql(`
  mutation AddToList($input: AddToContactListInput!) {
    addToContactList(input: $input) {
      count
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

type Contact = NonNullable<NonNullable<ContactsQuery["contacts"]>["nodes"]>[number];

export function ContactsPage() {
  const batch = useSearchParams().get("batch");
  const [search, setSearch] = useState("");
  const [debounced, setDebounced] = useState("");
  const [categoryIds, setCategoryIds] = useState<string[]>([]);
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);
  const [lists, setLists] = useState<{ id: string; name: string }[]>([]);
  const [rows, setRows] = useState<Contact[]>([]);
  const [total, setTotal] = useState(0);
  const [cursor, setCursor] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [listId, setListId] = useState("");
  const [newList, setNewList] = useState("");
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    execute(ContactsPageDataDocument).then(
      (d) => {
        setCategories(d.categories);
        setLists(d.contactLists?.nodes ?? []);
      },
      (e: Error) => setMessage(e.message),
    );
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const filter: ContactFilterInput = useMemo(
    () => ({ search: debounced || null, categoryIds, batchIds: batch ? [batch] : [] }),
    [debounced, categoryIds, batch],
  );

  useEffect(() => {
    let current = true;
    execute(ContactsDocument, { filter, after: null }).then(
      (d) => {
        if (!current) return;
        setRows(d.contacts?.nodes ?? []);
        setTotal(d.contacts?.totalCount ?? 0);
        // The selection survives new searches, so contacts from several searches can be added at once.
        setCursor(d.contacts?.pageInfo.hasNextPage ? (d.contacts.pageInfo.endCursor ?? null) : null);
      },
      (e: Error) => setMessage(e.message),
    );
    return () => {
      current = false;
    };
  }, [filter]);

  async function loadMore() {
    const d = await execute(ContactsDocument, { filter, after: cursor });
    setRows((r) => [...r, ...(d.contacts?.nodes ?? [])]);
    setCursor(d.contacts?.pageInfo.hasNextPage ? (d.contacts.pageInfo.endCursor ?? null) : null);
  }

  function toggle(id: string) {
    setSelected((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function addTo(source: { contactIds: string[] } | { filter: ContactFilterInput }) {
    setMessage(null);
    try {
      let target = lists.find((l) => l.id === listId);
      if (!target) {
        if (!newList.trim()) return setMessage("Pick a list or enter a name for a new one.");
        const { createContactList } = await execute(CreateListForSelectionDocument, { input: { name: newList } });
        if (!createContactList.contactList) return setMessage(messageOf(createContactList.errors));
        target = createContactList.contactList;
        setLists((l) => [target!, ...l]);
        setListId(target.id);
        setNewList("");
      }
      const { addToContactList } = await execute(AddToListDocument, { input: { listId: target.id, ...source } });
      if (addToContactList.errors?.length) return setMessage(messageOf(addToContactList.errors));
      const n = addToContactList.count ?? 0;
      setMessage(`${n} contact${n === 1 ? "" : "s"} added to ${target.name}.`);
      setSelected(new Set());
    } catch (e) {
      setMessage((e as Error).message);
    }
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Contacts</h1>
      <input
        type="search"
        aria-label="Search contacts"
        placeholder="Name, e-mail, phone, street, city…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="w-full rounded-md border border-border bg-background px-3 py-2"
      />
      <fieldset className="flex flex-wrap gap-3 text-sm">
        <legend className="sr-only">Categories</legend>
        {categories.map((c) => (
          <label key={c.id} className="flex items-center gap-1">
            <input
              type="checkbox"
              checked={categoryIds.includes(c.id)}
              onChange={(e) => setCategoryIds((ids) => (e.target.checked ? [...ids, c.id] : ids.filter((i) => i !== c.id)))}
            />
            {c.name}
          </label>
        ))}
        {batch && (
          <Link href="/app/contacts" className="underline">
            Only one import lot — show all
          </Link>
        )}
      </fieldset>
      <div className="flex flex-wrap items-end gap-2 text-sm">
        <label>
          Target list
          <select
            aria-label="Target list"
            value={listId}
            onChange={(e) => setListId(e.target.value)}
            className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
          >
            <option value="">— new list —</option>
            {lists.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </label>
        {!listId && (
          <label>
            New list name
            <input
              value={newList}
              onChange={(e) => setNewList(e.target.value)}
              className="mt-1 block rounded-md border border-border bg-background px-2 py-1"
            />
          </label>
        )}
        <button
          type="button"
          disabled={selected.size === 0}
          onClick={() => addTo({ contactIds: [...selected] })}
          className="rounded-md border border-border px-3 py-1 disabled:opacity-50"
        >
          Add selected ({selected.size})
        </button>
        <button
          type="button"
          disabled={total === 0}
          onClick={() => addTo({ filter })}
          className="rounded-md border border-border px-3 py-1 disabled:opacity-50"
        >
          Add all {total} matching
        </button>
      </div>
      {message && <p role="status">{message}</p>}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1" />
            <th className="px-2 py-1">Name</th>
            <th className="px-2 py-1">E-Mail</th>
            <th className="px-2 py-1">Phone</th>
            <th className="px-2 py-1">City</th>
            <th className="px-2 py-1">Obtained</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((c) => (
            <tr key={c.id} className="border-t border-border">
              <td className="px-2 py-1">
                <input
                  type="checkbox"
                  aria-label={`Select ${c.name ?? c.email ?? c.phone}`}
                  checked={selected.has(c.id)}
                  onChange={() => toggle(c.id)}
                />
              </td>
              <td className="px-2 py-1">
                <Link href={`/app/contacts/${c.id}`} className="underline">
                  {c.name ?? "—"}
                </Link>
              </td>
              <td className="px-2 py-1">{c.email}</td>
              <td className="px-2 py-1">{c.phone}</td>
              <td className="px-2 py-1">{c.city}</td>
              <td className="px-2 py-1">{c.obtainedOn}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-sm text-muted">
        {rows.length} of {total}
      </p>
      {cursor && (
        <button type="button" onClick={loadMore} className="underline">
          Load more
        </button>
      )}
    </div>
  );
}
```

```tsx
// web/src/app/app/contacts/page.tsx
import { Suspense } from "react";
import { ContactsPage } from "@/components/ContactsPage";

export const metadata = { title: "Contacts" };

export default function Page() {
  return (
    <Suspense>
      <ContactsPage />
    </Suspense>
  );
}
```

- [ ] **Step 4: Run the test**

Run: `cd web && bun run codegen && bun run test src/components/ContactsPage.test.tsx`
Expected: PASS.

- [ ] **Step 5: Lists page, imports action, navigation**

```tsx
// web/src/components/ListsPage.tsx
"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { ContactListMembersQuery, ContactListsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";

const ContactListsDocument = graphql(`
  query ContactLists {
    contactLists(first: 100) {
      nodes {
        id
        name
        memberCount
        createdAtUtc
        createdBy
      }
    }
  }
`);

const ContactListMembersDocument = graphql(`
  query ContactListMembers($listId: UUID!, $after: String) {
    contactListMembers(listId: $listId, first: 100, after: $after) {
      totalCount
      pageInfo {
        hasNextPage
        endCursor
      }
      nodes {
        addedAtUtc
        contact {
          id
          name
          email
          phone
          city
        }
      }
    }
  }
`);

const RenameListDocument = graphql(`
  mutation RenameList($input: RenameContactListInput!) {
    renameContactList(input: $input) {
      contactList {
        id
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const DeleteListDocument = graphql(`
  mutation DeleteList($input: DeleteContactListInput!) {
    deleteContactList(input: $input) {
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

const RemoveFromListDocument = graphql(`
  mutation RemoveFromList($input: RemoveFromContactListInput!) {
    removeFromContactList(input: $input) {
      count
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

type List = NonNullable<NonNullable<ContactListsQuery["contactLists"]>["nodes"]>[number];
type Member = NonNullable<NonNullable<ContactListMembersQuery["contactListMembers"]>["nodes"]>[number];

export function ListsPage() {
  const [lists, setLists] = useState<List[]>([]);
  const [open, setOpen] = useState<List | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(
    () => execute(ContactListsDocument).then((d) => setLists(d.contactLists?.nodes ?? []), (e: Error) => setMessage(e.message)),
    [],
  );

  useEffect(() => {
    load();
  }, [load]);

  async function openList(list: List, after: string | null = null) {
    const d = await execute(ContactListMembersDocument, { listId: list.id, after });
    setOpen(list);
    setMembers((m) => [...(after ? m : []), ...(d.contactListMembers?.nodes ?? [])]);
    setCursor(d.contactListMembers?.pageInfo.hasNextPage ? (d.contactListMembers.pageInfo.endCursor ?? null) : null);
  }

  async function rename(list: List) {
    const name = window.prompt("List name", list.name);
    if (!name) return;
    const { renameContactList } = await execute(RenameListDocument, { input: { id: list.id, name } });
    setMessage(messageOf(renameContactList.errors));
    await load();
  }

  async function remove(list: List) {
    if (!window.confirm(`Delete list "${list.name}"? Contacts stay.`)) return;
    const { deleteContactList } = await execute(DeleteListDocument, { input: { id: list.id } });
    setMessage(messageOf(deleteContactList.errors));
    if (open?.id === list.id) setOpen(null);
    await load();
  }

  async function removeMember(contactId: string) {
    if (!open) return;
    await execute(RemoveFromListDocument, { input: { listId: open.id, contactIds: [contactId] } });
    setMembers((m) => m.filter((x) => x.contact?.id !== contactId));
    await load();
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Lists</h1>
      {message && <p role="status">{message}</p>}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Name</th>
            <th className="px-2 py-1">Contacts</th>
            <th className="px-2 py-1">Created</th>
            <th className="px-2 py-1" />
          </tr>
        </thead>
        <tbody>
          {lists.map((l) => (
            <tr key={l.id} className="border-t border-border">
              <td className="px-2 py-1">
                <button type="button" onClick={() => openList(l)} className="underline">
                  {l.name}
                </button>
              </td>
              <td className="px-2 py-1">{l.memberCount}</td>
              <td className="px-2 py-1">
                {new Date(l.createdAtUtc).toLocaleDateString()} · {l.createdBy}
              </td>
              <td className="space-x-2 px-2 py-1">
                <button type="button" onClick={() => rename(l)} className="underline">
                  Rename
                </button>
                <button type="button" onClick={() => remove(l)} className="underline">
                  Delete
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {open && (
        <section aria-label={`Members of ${open.name}`} className="space-y-2">
          <h2 className="text-lg font-semibold">{open.name}</h2>
          <p className="text-sm text-muted">
            Add contacts from the <Link href="/app/contacts" className="underline">contacts search</Link>.
          </p>
          <table className="w-full text-sm">
            <tbody>
              {members.map((m) => (
                <tr key={m.contact?.id} className="border-t border-border">
                  <td className="px-2 py-1">
                    <Link href={`/app/contacts/${m.contact?.id}`} className="underline">
                      {m.contact?.name ?? m.contact?.email}
                    </Link>
                  </td>
                  <td className="px-2 py-1">{m.contact?.email}</td>
                  <td className="px-2 py-1">{m.contact?.phone}</td>
                  <td className="px-2 py-1">{m.contact?.city}</td>
                  <td className="px-2 py-1">
                    <button type="button" onClick={() => removeMember(m.contact!.id)} className="underline">
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {cursor && (
            <button type="button" onClick={() => openList(open, cursor)} className="underline">
              Load more
            </button>
          )}
        </section>
      )}
    </div>
  );
}
```

```tsx
// web/src/app/app/lists/page.tsx
import { ListsPage } from "@/components/ListsPage";

export const metadata = { title: "Lists" };

export default function Page() {
  return <ListsPage />;
}
```

In `ImportsPage.tsx`:

1. Add this mutation:

```tsx
const CreateListFromBatchDocument = graphql(`
  mutation CreateListFromBatch($input: CreateContactListFromBatchInput!) {
    createContactListFromBatch(input: $input) {
      contactList {
        id
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);
```

2. Add this handler inside `ImportsPage`, with `useRouter` from `next/navigation` and `messageOf` from `./ImportWizard`:

```tsx
const router = useRouter();
async function createList(b: Batch) {
  const name = window.prompt("Name of the new list", b.fileName);
  if (!name) return;
  const { createContactListFromBatch } = await execute(CreateListFromBatchDocument, { input: { batchId: b.id, name } });
  if (createContactListFromBatch.contactList) router.push("/app/lists");
  else setError(messageOf(createContactListFromBatch.errors));
}
```

3. In the actions cell, next to "Contacts", add this button. Show it only when `b.status === "DONE"`:

```tsx
<button type="button" onClick={() => createList(b)} className="underline">New list</button>
```

`AppNav.tsx`: make the links Contacts (`/app/contacts`, `contacts.read`), Imports (`/app/imports`, `imports.write`), Lists (`/app/lists`, `contacts.read`), Documents and Admin · Queues. In `web/src/app/app/page.tsx`, redirect to `/app/contacts`.

- [ ] **Step 6: Verify**

Run: `cd web && bun run lint && bun run typecheck && bun run test`
Expected: all pass.

In the running app (agent-browser):
1. Search a partial phone (`0170 12`) and a city.
2. Add 2 selected contacts to a new list "Manual".
3. Add "all matching" for a city.
4. On Imports, create a list from a lot.
5. Lists shows the member counts, and removing a member works.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(web): contact search with selection, lists page, list from import lot

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

# Milestone 3 — Campaigns & tracking

Sending runs in a hosted `CampaignDispatcher` that polls for campaigns in `Sending`. It does not use a RabbitMQ consumer. A campaign can send for hours (for example 10k SMS at 1/s), and RabbitMQ closes channels whose delivery stays unacknowledged for longer than 30 minutes. Polling `Queued` rows also makes restarts resume where they stopped.

### Task 12: Campaign domain, short tokens, templates, recipient rules

**Files:**
- Create: `src/ElectroMarketing.Domain/Campaigns/Campaign.cs`, `src/ElectroMarketing.Application/Campaigns/Senders.cs`, `src/ElectroMarketing.Application/Campaigns/CampaignContent.cs`
- Test: `tests/ElectroMarketing.Domain.Tests/ShortTokenTests.cs`, `tests/ElectroMarketing.Application.Tests/CampaignContentTests.cs`

**Interfaces:**
- Consumes: `Contact` (Task 3).
- Produces:
  - Domain: `Campaign`, `CampaignStatus`, `Channel`, `MessageStatus`, `CampaignMessage`, `TrackingEventType`, `TrackingEvent`, `ShortToken.New()`, `ShortToken.IsValid(string)`, `Campaign.LettersKey(Guid)`, `Campaign.Channels()`.
  - Sender contracts:
    - `EmailMessage(string To, string? ToName, string Subject, string HtmlBody, string UnsubscribeUrl)`
    - `IEmailSender.SendAsync(EmailMessage, CancellationToken)`
    - `ISmsSender.SendAsync(string toE164, string text, CancellationToken)`
  - Letter contracts:
    - `Letter(string? Name, string? Street, string? PostalCode, string? City, string? Country, string Body, string Url)`
    - `ILetterRenderer.Render(IReadOnlyList<Letter>) : byte[]`
  - Options and content:
    - `TrackingOptions { BaseUrl, LandingBaseUrl }`
    - `CampaignOptions { SenderLine, EmailsPerSecond, SmsPerSecond, TimeZone }`
    - `MessageTemplate.Render(string, IReadOnlyDictionary<string,string>, bool html)`
    - `CampaignContent(TrackingOptions)` with `Link`, `Pixel`, `Unsubscribe`, `Landing`, `Email`, `Sms` and `Letter`
    - `RecipientRules.SkipReason(Channel, Contact) : string?`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Domain.Tests/ShortTokenTests.cs
using ElectroMarketing.Domain.Campaigns;

namespace ElectroMarketing.Domain.Tests;

public class ShortTokenTests
{
    [Fact]
    public void Tokens_are_7_base62_characters_and_do_not_repeat()
    {
        var tokens = Enumerable.Range(0, 10_000).Select(_ => ShortToken.New()).ToList();

        tokens.ShouldAllBe(t => t.Length == 7 && t.All(char.IsAsciiLetterOrDigit));
        tokens.Distinct().Count().ShouldBe(10_000);
    }

    [Theory]
    [InlineData("aZ09xYq", true)]
    [InlineData("aZ09xY", false)]
    [InlineData("aZ09xY!", false)]
    [InlineData("graphq1", true)]
    public void Validates_shape(string token, bool valid) => ShortToken.IsValid(token).ShouldBe(valid);
}
```

```csharp
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
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Domain.Tests && dotnet test --project tests/ElectroMarketing.Application.Tests`
Expected: FAIL to compile.

- [ ] **Step 3: Implement**

```csharp
// src/ElectroMarketing.Domain/Campaigns/Campaign.cs
using System.Security.Cryptography;
using ElectroMarketing.Domain.Abstractions;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Domain.Campaigns;

public enum CampaignStatus
{
    Draft,
    Sending,
    Sent,
}

public enum Channel
{
    Email,
    Sms,
    Post,
}

public enum MessageStatus
{
    Queued,
    Sent,
    Failed,
    Skipped,
}

public enum TrackingEventType
{
    EmailOpen,
    LinkClick,
    LandingView,
    Unsubscribe,
}

/// <summary>Editable while Draft. Starting it snapshots the list into <see cref="CampaignMessage"/> rows.</summary>
public class Campaign : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>Null after the list was deleted; sent campaigns keep their messages.</summary>
    public Guid? ContactListId { get; set; }

    public bool SendEmail { get; set; }

    public bool SendSms { get; set; }

    public bool SendPost { get; set; }

    public string? EmailSubject { get; set; }

    /// <summary>HTML with placeholders <c>{{Name}}</c>, <c>{{Link}}</c>, <c>{{UnsubscribeLink}}</c>.</summary>
    public string? EmailBody { get; set; }

    public string? SmsText { get; set; }

    public string? LetterBody { get; set; }

    public string? LandingTitle { get; set; }

    /// <summary>Plain text shown on the public landing page.</summary>
    public string? LandingBody { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>The merged letters PDF exists at <see cref="LettersKey"/>.</summary>
    public bool LettersReady { get; set; }

    public static string LettersKey(Guid campaignId) => $"campaigns/{campaignId}/letters.pdf";

    public IEnumerable<Channel> Channels()
    {
        if (SendEmail)
        {
            yield return Channel.Email;
        }

        if (SendSms)
        {
            yield return Channel.Sms;
        }

        if (SendPost)
        {
            yield return Channel.Post;
        }
    }
}

/// <summary>One contact on one channel. <see cref="Token"/> is the random id in every link of this message.</summary>
public class CampaignMessage : IEntity<Guid>
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CampaignId { get; set; }

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public Channel Channel { get; set; }

    public required string Token { get; set; }

    public MessageStatus Status { get; set; } = MessageStatus.Queued;

    public DateTimeOffset? SentAtUtc { get; set; }

    /// <summary>Failure message or skip reason.</summary>
    public string? Error { get; set; }
}

/// <summary>Open, click, landing view or unsubscribe of one message. No IP address is stored.</summary>
public class TrackingEvent : IEntity<Guid>
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MessageId { get; set; }

    public CampaignMessage? Message { get; set; }

    public TrackingEventType Type { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? UserAgent { get; set; }
}

/// <summary>7 random base62 characters: 62^7 ≈ 3.5·10^12 values, short enough for SMS and QR codes, not guessable in bulk.</summary>
public static class ShortToken
{
    public const int Length = 7;
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static bool IsValid(string token) => token.Length == Length && token.All(char.IsAsciiLetterOrDigit);
}
```

```csharp
// src/ElectroMarketing.Application/Campaigns/Senders.cs
namespace ElectroMarketing.Application.Campaigns;

public sealed record EmailMessage(string To, string? ToName, string Subject, string HtmlBody, string UnsubscribeUrl);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public interface ISmsSender
{
    Task SendAsync(string toE164, string text, CancellationToken ct);
}

public sealed record Letter(string? Name, string? Street, string? PostalCode, string? City, string? Country, string Body, string Url);

public interface ILetterRenderer
{
    /// <summary>One PDF, one letter per page (more if the body is long).</summary>
    byte[] Render(IReadOnlyList<Letter> letters);
}
```

```csharp
// src/ElectroMarketing.Application/Campaigns/CampaignContent.cs
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Campaigns;

public sealed class TrackingOptions
{
    public const string Section = "Tracking";

    /// <summary>Public origin of the short links: the Api, or a short domain routed to it.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Origin of the web app that serves <c>/l/{token}</c>.</summary>
    [Required, Url]
    public string LandingBaseUrl { get; set; } = string.Empty;
}

public sealed class CampaignOptions
{
    public const string Section = "Campaigns";

    /// <summary>Small sender line above the address window of letters.</summary>
    [Required]
    public string SenderLine { get; set; } = string.Empty;

    [Range(0.01, 1000)]
    public double EmailsPerSecond { get; set; } = 10;

    [Range(0.01, 100)]
    public double SmsPerSecond { get; set; } = 1;

    /// <summary>IANA zone used to bucket insights by local hour and day.</summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Berlin";
}

public static partial class MessageTemplate
{
    /// <summary>Replaces <c>{{Key}}</c>; values are HTML-encoded when <paramref name="html"/> (contact data comes from CSV files). Unknown keys stay.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> values, bool html) =>
        Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? (html ? WebUtility.HtmlEncode(v) : v) : m.Value);

    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();
}

public static class RecipientRules
{
    /// <summary>Why the contact cannot get a message on the channel, or null when it can.</summary>
    public static string? SkipReason(Channel channel, Contact contact) => channel switch
    {
        Channel.Email when contact.Email is null => "No e-mail address.",
        Channel.Email when contact.EmailOptOut => "Unsubscribed from e-mail.",
        Channel.Sms when contact.PhoneE164 is null => "No phone number.",
        Channel.Sms when contact.SmsOptOut => "Unsubscribed from SMS.",
        Channel.Post when contact.Street is null || contact.City is null => "No postal address.",
        _ => null,
    };
}

/// <summary>Builds the per-contact message for each channel. Every link carries the message token.</summary>
public sealed class CampaignContent(TrackingOptions tracking)
{
    private string Base => tracking.BaseUrl.TrimEnd('/');

    public string Link(string token) => $"{Base}/{token}";

    public string Pixel(string token) => $"{Base}/o/{token}";

    public string Unsubscribe(string token) => $"{Base}/u/{token}";

    public string Landing(string token) => $"{tracking.LandingBaseUrl.TrimEnd('/')}/l/{token}";

    public EmailMessage Email(Campaign campaign, Contact contact, string token)
    {
        var values = Values(contact, token);
        var body = MessageTemplate.Render(campaign.EmailBody ?? string.Empty, values, html: true);
        if (campaign.EmailBody?.Contains("{{Link}}", StringComparison.Ordinal) != true)
        {
            body += $"""<p><a href="{Link(token)}">{Link(token)}</a></p>""";
        }

        body += $"""<p style="font-size:12px;color:#666"><a href="{Unsubscribe(token)}">Unsubscribe</a></p><img src="{Pixel(token)}" width="1" height="1" alt="">""";
        return new EmailMessage(contact.Email!, contact.Name, MessageTemplate.Render(campaign.EmailSubject ?? string.Empty, values, html: false), body, Unsubscribe(token));
    }

    public string Sms(Campaign campaign, Contact contact, string token)
    {
        var text = MessageTemplate.Render(campaign.SmsText ?? string.Empty, Values(contact, token), html: false);
        return text.Contains(Link(token), StringComparison.Ordinal) ? text : $"{text} {Link(token)}";
    }

    public Letter Letter(Campaign campaign, Contact contact, string token) =>
        new(contact.Name, contact.Street, contact.PostalCode, contact.City, contact.Country,
            MessageTemplate.Render(campaign.LetterBody ?? string.Empty, Values(contact, token), html: false), Link(token));

    private Dictionary<string, string> Values(Contact contact, string token) => new()
    {
        ["Name"] = contact.Name ?? string.Empty,
        ["Link"] = Link(token),
        ["UnsubscribeLink"] = Unsubscribe(token),
    };
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Domain.Tests && dotnet test --project tests/ElectroMarketing.Application.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(campaigns): campaign model, short tokens, message templates and recipient rules

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 13: Campaign persistence, drafts and start

**Files:**
- Create: `src/ElectroMarketing.Application/Campaigns/CampaignService.cs`, `src/ElectroMarketing.Infrastructure/Persistence/CampaignStore.cs`, `src/ElectroMarketing.Api/GraphQl/Campaigns/CampaignFeature.cs`
- Modify: `AppDbContext.cs`, `Configurations.cs`, both `DependencyInjection.cs`, `Permissions.cs`, `auth-roles.json`
- Test: `tests/ElectroMarketing.Infrastructure.Tests/CampaignServiceTests.cs`

**Interfaces:**
- Consumes: Task 12 types; `ContactListMember` (Task 10).
- Produces:
  - `CampaignDto(string Name, Guid? ContactListId, bool SendEmail, bool SendSms, bool SendPost, string? EmailSubject, string? EmailBody, string? SmsText, string? LetterBody, string? LandingTitle, string? LandingBody)`
  - `CampaignService`: CRUD (Draft only), `StartAsync(Guid, ct) : Task<Campaign>`
  - `ICampaignStore`: `AddRecipientsAsync`, `NextQueuedAsync(Guid campaignId, Channel, int take, ct)`, `SaveAsync`
  - DbSets `Campaigns`, `CampaignMessages`, `TrackingEvents`
  - Permissions `Campaigns.Read`, `Campaigns.Write` and `Campaigns.Send`
  - GraphQL queries: `campaigns`, `campaign(id)`, `campaignMessages(campaignId)`
  - GraphQL mutations (payload field after the arrow):
    - `createCampaign(campaign)` → `campaign`
    - `updateCampaign(id, campaign)` → `campaign`
    - `deleteCampaign(id)` → `uuid`
    - `startCampaign(id)` → `campaign`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/CampaignServiceTests.cs
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Exceptions;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class CampaignServiceTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly ContactList list = new() { Name = "L" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        await using var ctx = db.Create();
        ctx.ContactLists.Add(list);
        Contact[] contacts =
        [
            new() { Name = "Full", Email = "full@x.de", PhoneE164 = "+491701111111", Street = "A 1", City = "Köln" },
            new() { Name = "MailOnly", Email = "mail@x.de" },
            new() { Name = "OptOut", Email = "out@x.de", EmailOptOut = true, PhoneE164 = "+491702222222" },
        ];
        ctx.Contacts.AddRange(contacts);
        ctx.ContactListMembers.AddRange(contacts.Select(c => new ContactListMember { ContactListId = list.Id, ContactId = c.Id }));
        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CampaignService Service(AppDbContext ctx) =>
        new(new Repository<Campaign, Guid>(ctx), new CampaignStore(ctx), TimeProvider.System);

    private CampaignDto Dto(bool email = true, bool sms = true, bool post = true, string? smsText = "Hi {{Name}}") =>
        new("Spring", list.Id, email, sms, post, "Subject", "<p>Body</p>", smsText, "Letter", "Title", "Body");

    [Fact]
    public async Task Start_snapshots_one_message_per_contact_and_channel_with_skip_reasons()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var campaign = await service.CreateAsync(Dto(), Ct);

        var started = await service.StartAsync(campaign.Id, Ct);

        started.Status.ShouldBe(CampaignStatus.Sending);
        await using var read = db.Create();
        var messages = await read.CampaignMessages.Include(m => m.Contact).ToListAsync(Ct);
        messages.Count.ShouldBe(9);
        messages.Select(m => m.Token).Distinct().Count().ShouldBe(9);
        messages.Where(m => m.Status == MessageStatus.Queued).Select(m => $"{m.Contact!.Name}:{m.Channel}")
            .ShouldBe(["Full:Email", "Full:Sms", "Full:Post", "MailOnly:Email", "OptOut:Sms"], ignoreOrder: true);
        messages.Single(m => m.Contact!.Name == "OptOut" && m.Channel == Channel.Email).Error.ShouldBe("Unsubscribed from e-mail.");
    }

    [Fact]
    public async Task Started_campaign_cannot_be_started_edited_or_deleted()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var campaign = await service.CreateAsync(Dto(), Ct);
        await service.StartAsync(campaign.Id, Ct);

        await Should.ThrowAsync<ConflictException>(() => service.StartAsync(campaign.Id, Ct));
        await Should.ThrowAsync<ConflictException>(() => service.UpdateAsync(campaign.Id, Dto(), Ct));
        await Should.ThrowAsync<ConflictException>(() => service.DeleteAsync(campaign.Id, Ct));
    }

    [Fact]
    public async Task Start_validates_channel_content_and_reachability()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);

        var noText = await service.CreateAsync(Dto(smsText: null), Ct);
        (await Should.ThrowAsync<ValidationException>(() => service.StartAsync(noText.Id, Ct))).Errors.Keys.ShouldBe(["smsText"]);

        var nothing = await service.CreateAsync(Dto(email: false, sms: false, post: false), Ct);
        (await Should.ThrowAsync<ValidationException>(() => service.StartAsync(nothing.Id, Ct))).Errors.Keys.ShouldBe(["channels"]);
    }

    [Fact]
    public async Task Deleting_the_list_keeps_the_sent_messages()
    {
        await using (var ctx = db.Create())
        {
            var service = Service(ctx);
            await service.StartAsync((await service.CreateAsync(Dto(), Ct)).Id, Ct);
            ctx.ContactLists.Remove(await ctx.ContactLists.SingleAsync(Ct));
            await ctx.SaveChangesAsync(Ct);
        }

        await using var read = db.Create();
        (await read.Campaigns.SingleAsync(Ct)).ContactListId.ShouldBeNull();
        (await read.CampaignMessages.CountAsync(Ct)).ShouldBe(9);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*CampaignServiceTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Persistence**

Add `Campaigns`, `CampaignMessages` and `TrackingEvents` DbSets to `AppDbContext`, then append to `Configurations.cs`:

```csharp
internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> b)
    {
        b.Property(c => c.Name).HasMaxLength(200);
        b.Property(c => c.EmailSubject).HasMaxLength(300);
        b.Property(c => c.SmsText).HasMaxLength(1000);
        b.Property(c => c.LandingTitle).HasMaxLength(200);
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
        b.HasOne<ContactList>().WithMany().HasForeignKey(c => c.ContactListId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(c => c.Status);
    }
}

internal sealed class CampaignMessageConfiguration : IEntityTypeConfiguration<CampaignMessage>
{
    public void Configure(EntityTypeBuilder<CampaignMessage> b)
    {
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.Token).HasMaxLength(ShortToken.Length).IsFixedLength();
        b.Property(m => m.Channel).HasConversion<string>().HasMaxLength(16);
        b.Property(m => m.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(m => m.Error).HasMaxLength(500);
        b.HasIndex(m => m.Token).IsUnique();
        b.HasIndex(m => new { m.CampaignId, m.Channel, m.Status });
        b.HasOne<Campaign>().WithMany().HasForeignKey(m => m.CampaignId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.Contact).WithMany().HasForeignKey(m => m.ContactId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TrackingEventConfiguration : IEntityTypeConfiguration<TrackingEvent>
{
    public void Configure(EntityTypeBuilder<TrackingEvent> b)
    {
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Type).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.UserAgent).HasMaxLength(512);
        b.HasOne(e => e.Message).WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(e => new { e.MessageId, e.Type });
    }
}
```

```bash
dotnet ef migrations add Campaigns --context AppDbContext --project src/ElectroMarketing.Infrastructure --startup-project src/ElectroMarketing.DbUtils --output-dir Persistence/Migrations
```

- [ ] **Step 4: Service and store**

```csharp
// src/ElectroMarketing.Application/Campaigns/CampaignService.cs
using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Exceptions;
using DomainValidationException = ElectroMarketing.Domain.Exceptions.ValidationException;

namespace ElectroMarketing.Application.Campaigns;

public sealed record CampaignDto(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    Guid? ContactListId,
    bool SendEmail,
    bool SendSms,
    bool SendPost,
    [property: StringLength(300)] string? EmailSubject,
    [property: StringLength(100_000)] string? EmailBody,
    [property: StringLength(1000)] string? SmsText,
    [property: StringLength(20_000)] string? LetterBody,
    [property: StringLength(200)] string? LandingTitle,
    [property: StringLength(20_000)] string? LandingBody);

public interface ICampaignStore
{
    /// <summary>Adds (does not save) one message per list member and selected channel; returns how many are Queued.</summary>
    Task<int> AddRecipientsAsync(Campaign campaign, CancellationToken ct);

    /// <summary>Tracked Queued messages of one channel, with their contact, oldest first.</summary>
    Task<IReadOnlyList<CampaignMessage>> NextQueuedAsync(Guid campaignId, Channel channel, int take, CancellationToken ct);

    /// <summary>Saves, then detaches messages and contacts; the campaign stays tracked.</summary>
    Task SaveAsync(CancellationToken ct);
}

public sealed class CampaignService(IRepository<Campaign, Guid> campaigns, ICampaignStore store, TimeProvider time)
    : CrudService<Campaign, Guid, CampaignDto, CampaignDto>(campaigns)
{
    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await RequireDraftAsync(id, "Only draft campaigns can be deleted.", ct);
        await base.DeleteAsync(id, ct);
    }

    public async Task<Campaign> StartAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await RequireDraftAsync(id, "The campaign was already started.", ct);
        Validate(campaign);

        // The store adds the messages to the same scoped DbContext; UpdateAsync commits them with the status change.
        // ponytail: a token collision (≈ never at 62^7) fails the start with a unique-index ConflictError; just start again.
        if (await store.AddRecipientsAsync(campaign, ct) == 0)
        {
            throw new ConflictException("No contact in the list can be reached on the selected channels.");
        }

        campaign.Status = CampaignStatus.Sending;
        campaign.StartedAtUtc = time.GetUtcNow();
        return await Repository.UpdateAsync(campaign, ct);
    }

    protected override Campaign Map(CampaignDto dto)
    {
        var campaign = new Campaign { Name = dto.Name.Trim() };
        Copy(dto, campaign);
        return campaign;
    }

    protected override void Apply(CampaignDto dto, Campaign entity)
    {
        if (entity.Status != CampaignStatus.Draft)
        {
            throw new ConflictException("Only draft campaigns can be edited.");
        }

        entity.Name = dto.Name.Trim();
        Copy(dto, entity);
    }

    private static void Copy(CampaignDto dto, Campaign c)
    {
        (c.ContactListId, c.SendEmail, c.SendSms, c.SendPost) = (dto.ContactListId, dto.SendEmail, dto.SendSms, dto.SendPost);
        (c.EmailSubject, c.EmailBody, c.SmsText, c.LetterBody) = (dto.EmailSubject, dto.EmailBody, dto.SmsText, dto.LetterBody);
        (c.LandingTitle, c.LandingBody) = (dto.LandingTitle, dto.LandingBody);
    }

    private static void Validate(Campaign c)
    {
        var errors = new Dictionary<string, string[]>();
        if (c.ContactListId is null)
        {
            errors["contactListId"] = ["Pick a contact list."];
        }

        if (!c.Channels().Any())
        {
            errors["channels"] = ["Pick at least one channel."];
        }

        if (c.SendEmail && (string.IsNullOrWhiteSpace(c.EmailSubject) || string.IsNullOrWhiteSpace(c.EmailBody)))
        {
            errors["emailBody"] = ["E-mail needs a subject and a body."];
        }

        if (c.SendSms && string.IsNullOrWhiteSpace(c.SmsText))
        {
            errors["smsText"] = ["SMS needs a text."];
        }

        if (c.SendPost && string.IsNullOrWhiteSpace(c.LetterBody))
        {
            errors["letterBody"] = ["Post needs a letter text."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }

    private async Task<Campaign> RequireDraftAsync(Guid id, string message, CancellationToken ct)
    {
        var campaign = await Repository.GetByIdAsync(id, ct) ?? throw NotFoundException.For<Campaign>(id);
        return campaign.Status == CampaignStatus.Draft ? campaign : throw new ConflictException(message);
    }
}
```

```csharp
// src/ElectroMarketing.Infrastructure/Persistence/CampaignStore.cs
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

public sealed class CampaignStore(AppDbContext db) : ICampaignStore
{
    public async Task<int> AddRecipientsAsync(Campaign campaign, CancellationToken ct)
    {
        var contacts = await db.ContactListMembers.AsNoTracking()
            .Where(m => m.ContactListId == campaign.ContactListId)
            .Select(m => m.Contact!)
            .ToListAsync(ct);
        var queued = 0;
        foreach (var contact in contacts)
        {
            foreach (var channel in campaign.Channels())
            {
                var reason = RecipientRules.SkipReason(channel, contact);
                queued += reason is null ? 1 : 0;
                db.CampaignMessages.Add(new CampaignMessage
                {
                    CampaignId = campaign.Id,
                    ContactId = contact.Id,
                    Channel = channel,
                    Token = ShortToken.New(),
                    Status = reason is null ? MessageStatus.Queued : MessageStatus.Skipped,
                    Error = reason,
                });
            }
        }

        return queued;
    }

    public async Task<IReadOnlyList<CampaignMessage>> NextQueuedAsync(Guid campaignId, Channel channel, int take, CancellationToken ct) =>
        await db.CampaignMessages
            .Include(m => m.Contact)
            .Where(m => m.CampaignId == campaignId && m.Channel == channel && m.Status == MessageStatus.Queued)
            .OrderBy(m => m.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is CampaignMessage or Contact).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
```

DI:
- Application: `services.AddScoped<CampaignService>();`
- Infrastructure: `services.AddScoped<ICampaignStore, CampaignStore>();`
- Permissions:

```csharp
public static class Campaigns
{
    public const string Read = "campaigns.read";
    public const string Write = "campaigns.write";
    public const string Send = "campaigns.send";
}
```

Grant all three to `user` and `platform-admin`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*CampaignServiceTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: GraphQL feature**

```csharp
// src/ElectroMarketing.Api/GraphQl/Campaigns/CampaignFeature.cs
using ElectroMarketing.Api.GraphQl.Errors;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Campaigns;
using HotChocolate.Authorization;

namespace ElectroMarketing.Api.GraphQl.Campaigns;

[ExtendObjectType(typeof(RootQuery))]
public class CampaignQueries : RootQuery
{
    [Authorize(Policy = Permissions.Campaigns.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Campaign> GetCampaigns([Service] IRepository<Campaign, Guid> campaigns) => campaigns.Query().OrderByDescending(c => c.CreatedAtUtc);

    [Authorize(Policy = Permissions.Campaigns.Read)]
    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<Campaign> GetCampaign(Guid id, [Service] IRepository<Campaign, Guid> campaigns) => campaigns.Query().Where(c => c.Id == id);

    [Authorize(Policy = Permissions.Campaigns.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<CampaignMessage> GetCampaignMessages(Guid campaignId, [Service] IRepository<CampaignMessage, Guid> messages) =>
        messages.Query().Where(m => m.CampaignId == campaignId).OrderBy(m => m.Id);
}

[ExtendObjectType(typeof(RootMutation))]
public class CampaignMutations : RootMutation
{
    [Authorize(Policy = Permissions.Campaigns.Write)]
    [Error<ValidationError>]
    public Task<Campaign> CreateCampaign(CampaignDto campaign, [Service] CampaignService campaigns, CancellationToken ct) =>
        campaigns.CreateAsync(campaign, ct);

    [Authorize(Policy = Permissions.Campaigns.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    [Error<ConflictError>]
    [Error<ConcurrencyError>]
    public Task<Campaign> UpdateCampaign(Guid id, CampaignDto campaign, [Service] CampaignService campaigns, CancellationToken ct) =>
        campaigns.UpdateAsync(id, campaign, ct);

    [Authorize(Policy = Permissions.Campaigns.Write)]
    [Error<NotFoundError>]
    [Error<ConflictError>]
    public async Task<Guid> DeleteCampaign(Guid id, [Service] CampaignService campaigns, CancellationToken ct)
    {
        await campaigns.DeleteAsync(id, ct);
        return id;
    }

    /// <summary>Snapshots the list and queues the messages; the dispatcher sends them in the background.</summary>
    [Authorize(Policy = Permissions.Campaigns.Send)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    [Error<ConflictError>]
    public Task<Campaign> StartCampaign(Guid id, [Service] CampaignService campaigns, CancellationToken ct) => campaigns.StartAsync(id, ct);
}

public sealed class CampaignType : ObjectType<Campaign>
{
    protected override void Configure(IObjectTypeDescriptor<Campaign> descriptor)
    {
        descriptor.Ignore(c => c.Channels());
        descriptor.Field(c => c.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}
```

Run: `dotnet test --project tests/ElectroMarketing.Api.Tests`, then regenerate the contract.
Expected: PASS, and the schema contains `startCampaign`.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(campaigns): drafts, validation and recipient snapshot on start

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 14: E-Mail and SMS senders, options, Mailpit

**Files:**
- Create: `src/ElectroMarketing.Infrastructure/Campaigns/SmtpEmailSender.cs`, `src/ElectroMarketing.Infrastructure/Campaigns/SmsSenders.cs`, `src/ElectroMarketing.Infrastructure/Campaigns/CampaignSetup.cs`
- Modify: `Directory.Packages.props` (`MailKit` under a new `<!-- Campaigns -->` label), Infrastructure csproj, `src/ElectroMarketing.Api/Program.cs`, `src/ElectroMarketing.Api/appsettings.json`, `aspire/ElectroMarketing.AppHost/AppHost.cs`, `compose.prod.yaml`, `.env.example`
- Test: `tests/ElectroMarketing.Infrastructure.Tests/SenderTests.cs`

**Interfaces:**
- Consumes: `IEmailSender`, `ISmsSender`, `EmailMessage`, `TrackingOptions`, `CampaignOptions` (Task 12).
- Produces: `SmtpOptions`, `TwilioOptions`, `SmtpEmailSender`, `TwilioSmsSender`, `LoggingSmsSender`, and `AddCampaignSending(this IServiceCollection, IConfiguration)`. The last one registers the options, the senders, `CampaignContent`, `TrackingOptions` and `CampaignOptions` as singletons. Task 15 adds the letter renderer to it.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/SenderTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Infrastructure.Campaigns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class SenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Smtp_sender_delivers_html_with_one_click_unsubscribe_headers()
    {
        // Use the same pinned tag as the AppHost Mailpit container.
        await using var mailpit = new ContainerBuilder()
            .WithImage("axllent/mailpit:v1.27")
            .WithPortBinding(1025, true)
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/info")))
            .Build();
        await mailpit.StartAsync(Ct);
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions { Host = mailpit.Hostname, Port = mailpit.GetMappedPublicPort(1025), From = "news@em.test" }));

        await sender.SendAsync(new EmailMessage("eve@x.de", "Eve", "Hello", "<p>Hi</p>", "https://em.test/u/Ab3dE5g"), Ct);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{mailpit.Hostname}:{mailpit.GetMappedPublicPort(8025)}") };
        var list = await http.GetFromJsonAsync<JsonNode>("/api/v1/messages", Ct);
        var id = list!["messages"]![0]!["ID"]!.GetValue<string>();
        list["messages"]![0]!["Subject"]!.GetValue<string>().ShouldBe("Hello");
        var headers = await http.GetFromJsonAsync<JsonNode>($"/api/v1/message/{id}/headers", Ct);
        headers!["List-Unsubscribe"]![0]!.GetValue<string>().ShouldBe("<https://em.test/u/Ab3dE5g>");
        headers["List-Unsubscribe-Post"]![0]!.GetValue<string>().ShouldBe("List-Unsubscribe=One-Click");
    }

    [Fact]
    public async Task Twilio_sender_posts_form_with_basic_auth_and_throws_on_error()
    {
        HttpRequestMessage? seen = null;
        string? body = null;
        var handler = new StubHandler(async request =>
        {
            seen = request;
            body = await request.Content!.ReadAsStringAsync(Ct);
            return new HttpResponseMessage(body.Contains("%2B490", StringComparison.Ordinal) ? HttpStatusCode.BadRequest : HttpStatusCode.Created)
            {
                Content = new StringContent("{\"message\":\"invalid To\"}"),
            };
        });
        var sender = new TwilioSmsSender(new HttpClient(handler), Options.Create(new TwilioOptions { AccountSid = "AC1", AuthToken = "secret", From = "+4915100000" }));

        await sender.SendAsync("+491701234567", "Hi https://em.test/Ab3dE5g", Ct);

        seen!.RequestUri!.ToString().ShouldBe("https://api.twilio.com/2010-04-01/Accounts/AC1/Messages.json");
        seen.Headers.Authorization!.ToString().ShouldBe("Basic " + Convert.ToBase64String("AC1:secret"u8.ToArray()));
        body.ShouldBe("To=%2B491701234567&From=%2B4915100000&Body=Hi+https%3A%2F%2Fem.test%2FAb3dE5g");
        (await Should.ThrowAsync<HttpRequestException>(() => sender.SendAsync("+490", "x", Ct))).Message.ShouldContain("invalid To");
    }

    [Fact]
    public void Without_twilio_settings_sms_are_only_logged()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "news@em.test",
            ["Tracking:BaseUrl"] = "https://em.test",
            ["Tracking:LandingBaseUrl"] = "https://app.test",
            ["Campaigns:SenderLine"] = "EM · Street 1 · 12345 Town",
        }).Build();
        var services = new ServiceCollection().AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)).AddCampaignSending(configuration).BuildServiceProvider();

        services.GetRequiredService<ISmsSender>().ShouldBeOfType<LoggingSmsSender>();
        services.GetRequiredService<CampaignContent>().Link("Ab3dE5g").ShouldBe("https://em.test/Ab3dE5g");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
```

Pin the Mailpit tag to the newest release that is at least 2 weeks old (hub.docker.com/r/axllent/mailpit/tags). Use the same tag in the test and in the AppHost.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*SenderTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement**

```csharp
// src/ElectroMarketing.Infrastructure/Campaigns/SmtpEmailSender.cs
using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Campaigns;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ElectroMarketing.Infrastructure.Campaigns;

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 25;

    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required, EmailAddress]
    public string From { get; set; } = string.Empty;

    public string? FromName { get; set; }
}

/// <summary>HTML mail with RFC 8058 one-click unsubscribe headers (required by Gmail/Yahoo for bulk senders).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        var mime = new MimeMessage { Subject = message.Subject };
        mime.From.Add(new MailboxAddress(o.FromName, o.From));
        mime.To.Add(new MailboxAddress(message.ToName, message.To));
        mime.Headers.Add("List-Unsubscribe", $"<{message.UnsubscribeUrl}>");
        mime.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        // ponytail: one SMTP connection per message; keep the connection open per chunk if the relay throttles connects.
        using var client = new SmtpClient();
        await client.ConnectAsync(o.Host, o.Port, o.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(o.Username))
        {
            await client.AuthenticateAsync(o.Username, o.Password, ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
```

```csharp
// src/ElectroMarketing.Infrastructure/Campaigns/SmsSenders.cs
using System.Net.Http.Headers;
using System.Text;
using ElectroMarketing.Application.Campaigns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElectroMarketing.Infrastructure.Campaigns;

public sealed class TwilioOptions
{
    public const string Section = "Twilio";

    public string AccountSid { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Sender number (E.164) or alphanumeric sender id.</summary>
    public string From { get; set; } = string.Empty;
}

/// <summary>Twilio Messages REST API; any non-2xx answer throws with Twilio's message.</summary>
public sealed class TwilioSmsSender(HttpClient http, IOptions<TwilioOptions> options) : ISmsSender
{
    public async Task SendAsync(string toE164, string text, CancellationToken ct)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{o.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = toE164, ["From"] = o.From, ["Body"] = text }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.AccountSid}:{o.AuthToken}")));
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Twilio {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }
    }
}

/// <summary>Used when no Twilio account is configured (development): the SMS only goes to the log.</summary>
public sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string toE164, string text, CancellationToken ct)
    {
        logger.LogInformation("SMS (not sent, Twilio not configured) to {To}: {Text}", toE164, text);
        return Task.CompletedTask;
    }
}
```

```csharp
// src/ElectroMarketing.Infrastructure/Campaigns/CampaignSetup.cs
using ElectroMarketing.Application.Campaigns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElectroMarketing.Infrastructure.Campaigns;

public static class CampaignSetup
{
    /// <summary>Senders and campaign options. Api only: DbUtils never sends and has no SMTP settings.</summary>
    public static IServiceCollection AddCampaignSending(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TrackingOptions>().Bind(configuration.GetSection(TrackingOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<CampaignOptions>().Bind(configuration.GetSection(CampaignOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TwilioOptions>().Bind(configuration.GetSection(TwilioOptions.Section));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<TrackingOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CampaignOptions>>().Value);
        services.AddSingleton<CampaignContent>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        if (string.IsNullOrEmpty(configuration[$"{TwilioOptions.Section}:{nameof(TwilioOptions.AccountSid)}"]))
        {
            services.AddSingleton<ISmsSender, LoggingSmsSender>();
        }
        else
        {
            services.AddHttpClient<ISmsSender, TwilioSmsSender>();
        }

        return services;
    }
}
```

`Program.cs`: add `builder.Services.AddCampaignSending(builder.Configuration);` after `builder.AddInfrastructure();`.

`src/ElectroMarketing.Api/appsettings.json`: add the dev defaults below. AppHost and production override them.

```json
"Smtp": { "Host": "localhost", "Port": 1025, "From": "marketing@electromarketing.local", "FromName": "ElectroMarketing" },
"Tracking": { "BaseUrl": "https://localhost:7001", "LandingBaseUrl": "https://localhost:3000" },
"Campaigns": { "SenderLine": "ElectroMarketing · Musterstraße 1 · 12345 Musterstadt", "TimeZone": "Europe/Berlin" }
```

`AppHost.cs`: add Mailpit before `api` and wire it in. Keep the pinned tag.

```csharp
// Dev SMTP: every campaign e-mail lands in the Mailpit inbox (UI on the "ui" endpoint).
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "ui")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithLifetime(ContainerLifetime.Persistent);
var smtp = mailpit.GetEndpoint("smtp");
```

On the `api` builder chain, add:

```csharp
    .WithEnvironment("Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WaitFor(mailpit)
```

After the `api` chain, add:

```csharp
api.WithEnvironment("Tracking__BaseUrl", api.GetEndpoint("https"));
```

`compose.prod.yaml`: add these Api environment variables, with matching placeholders in `.env.example`:
- `Smtp__Host`, `Smtp__Port`, `Smtp__UseStartTls`, `Smtp__Username`, `Smtp__Password`, `Smtp__From`
- `Twilio__AccountSid`, `Twilio__AuthToken`, `Twilio__From`
- `Tracking__BaseUrl` (the short public domain), `Tracking__LandingBaseUrl`, `Campaigns__SenderLine`

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*SenderTests"`
Expected: PASS (3 tests). Then run `dotnet test --project tests/ElectroMarketing.Api.Tests`. Expected: still PASS, because the options validate from `appsettings.json`.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(campaigns): SMTP and Twilio senders, Mailpit in dev

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 15: Letter PDF with QR code

**Files:**
- Create: `src/ElectroMarketing.Infrastructure/Campaigns/QuestLetterRenderer.cs`
- Modify: `Directory.Packages.props` (`QuestPDF` and `QRCoder` in Campaigns; `UglyToad.PdfPig` in Tests), Infrastructure csproj, Infrastructure.Tests csproj, `CampaignSetup.cs`
- Test: `tests/ElectroMarketing.Infrastructure.Tests/LetterRendererTests.cs`

**Interfaces:**
- Consumes: `ILetterRenderer`, `Letter`, `CampaignOptions` (Task 12).
- Produces: `QuestLetterRenderer(CampaignOptions)`, registered as `ILetterRenderer` in `AddCampaignSending`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/LetterRendererTests.cs
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
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*LetterRendererTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement**

```csharp
// src/ElectroMarketing.Infrastructure/Campaigns/QuestLetterRenderer.cs
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
```

In `CampaignSetup.AddCampaignSending`, add `services.AddSingleton<ILetterRenderer, QuestLetterRenderer>();`.

- [ ] **Step 4: Run the test**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*LetterRendererTests"`
Expected: PASS. If PdfPig splits "50667 Köln" into separate words, it still matches, because the words are joined with spaces.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(campaigns): DIN 5008 letter PDF with QR short link

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 16: Campaign dispatcher and letters download

**Files:**
- Create: `src/ElectroMarketing.Application/Campaigns/CampaignSender.cs`, `src/ElectroMarketing.Api/CampaignDispatcher.cs`
- Modify: `src/ElectroMarketing.Application/DependencyInjection.cs`, `src/ElectroMarketing.Api/DependencyInjection.cs`, `src/ElectroMarketing.Api/FileEndpoints.cs`
- Test: `tests/ElectroMarketing.Infrastructure.Tests/CampaignSenderTests.cs`, `tests/ElectroMarketing.Api.Tests/CampaignGraphQlTests.cs`

**Interfaces:**
- Consumes: Tasks 12–15; `IObjectStorage`, `Document.Bucket`.
- Produces:
  - `CampaignSender.SendPendingAsync(CancellationToken)`
  - `CampaignSender.RunAsync(Campaign, CancellationToken)`
  - Hosted `CampaignDispatcher` (every 5 s)
  - `GET /campaigns/{id}/letters` (`campaigns.read`)

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Infrastructure.Tests/CampaignSenderTests.cs
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class CampaignSenderTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly IEmailSender email = Substitute.For<IEmailSender>();
    private readonly ISmsSender sms = Substitute.For<ISmsSender>();
    private readonly ILetterRenderer letters = Substitute.For<ILetterRenderer>();
    private readonly IObjectStorage storage = Substitute.For<IObjectStorage>();
    private Guid campaignId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        letters.Render(Arg.Any<IReadOnlyList<Letter>>()).Returns([1, 2, 3]);
        await using var ctx = db.Create();
        var list = new ContactList { Name = "L" };
        Contact[] contacts =
        [
            new() { Name = "Ann", Email = "ann@x.de", PhoneE164 = "+491701111111", Street = "A 1", City = "Köln" },
            new() { Name = "Bob", Email = "bob@x.de", PhoneE164 = "+491702222222", Street = "B 2", City = "Bonn" },
        ];
        ctx.AddRange(list);
        ctx.Contacts.AddRange(contacts);
        ctx.ContactListMembers.AddRange(contacts.Select(c => new ContactListMember { ContactListId = list.Id, ContactId = c.Id }));
        await ctx.SaveChangesAsync(Ct);

        var service = new CampaignService(new Repository<Campaign, Guid>(ctx), new CampaignStore(ctx), TimeProvider.System);
        var campaign = await service.CreateAsync(
            new CampaignDto("C", list.Id, true, true, true, "Hi {{Name}}", "<p>{{Link}}</p>", "Hi {{Name}}", "Dear {{Name}}", null, null), Ct);
        campaignId = (await service.StartAsync(campaign.Id, Ct)).Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private CampaignSender Sender(AppDbContext ctx) => new(
        new Repository<Campaign, Guid>(ctx),
        new CampaignStore(ctx),
        new CampaignContent(new TrackingOptions { BaseUrl = "https://em.test", LandingBaseUrl = "https://app.test" }),
        email,
        sms,
        letters,
        storage,
        new CampaignOptions { SenderLine = "S", EmailsPerSecond = 1000, SmsPerSecond = 100 },
        TimeProvider.System,
        NullLogger<CampaignSender>.Instance);

    [Fact]
    public async Task Sends_every_channel_marks_messages_and_finishes()
    {
        sms.SendAsync("+491702222222", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new HttpRequestException("Twilio 400: bad")));

        await using (var ctx = db.Create())
        {
            await Sender(ctx).SendPendingAsync(Ct);
        }

        await email.Received(2).SendAsync(Arg.Is<EmailMessage>(m => m.HtmlBody.Contains("https://em.test/o/")), Arg.Any<CancellationToken>());
        await sms.Received(1).SendAsync("+491701111111", Arg.Is<string>(t => t.StartsWith("Hi Ann https://em.test/")), Arg.Any<CancellationToken>());
        letters.Received(1).Render(Arg.Is<IReadOnlyList<Letter>>(l => l.Count == 2));
        await storage.Received(1).PutAsync("documents", Campaign.LettersKey(campaignId), Arg.Any<Stream>(), "application/pdf", Arg.Any<CancellationToken>());

        await using var read = db.Create();
        var campaign = await read.Campaigns.SingleAsync(Ct);
        (campaign.Status, campaign.LettersReady).ShouldBe((CampaignStatus.Sent, true));
        var failed = await read.CampaignMessages.SingleAsync(m => m.Status == MessageStatus.Failed, Ct);
        failed.Error.ShouldBe("Twilio 400: bad");
        (await read.CampaignMessages.CountAsync(m => m.Status == MessageStatus.Sent && m.SentAtUtc != null, Ct)).ShouldBe(5);
    }

    [Fact]
    public async Task Restart_only_sends_what_is_still_queued()
    {
        await using (var ctx = db.Create())
        {
            var first = await ctx.CampaignMessages.Where(m => m.Channel == Channel.Email).OrderBy(m => m.Id).FirstAsync(Ct);
            first.Status = MessageStatus.Sent;
            await ctx.SaveChangesAsync(Ct);
        }

        await using (var ctx = db.Create())
        {
            await Sender(ctx).SendPendingAsync(Ct);
            await Sender(ctx).SendPendingAsync(Ct);
        }

        await email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }
}
```

```csharp
// tests/ElectroMarketing.Api.Tests/CampaignGraphQlTests.cs
using System.Net;
using System.Text.Json.Nodes;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.Tests;

/// <summary>Draft → validation → start → dispatcher (logging SMS sender, no Twilio in tests) → Sent.</summary>
public sealed class CampaignGraphQlTests(ApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sms_campaign_is_sent_by_the_dispatcher()
    {
        var listId = await SeedListAsync();
        using var client = api.ClientFor("campaigner@local.test", "user");
        const string fields = "name: \"Spring\", sendEmail: false, sendSms: true, sendPost: false";

        var id = (await client.Gql($$"""
            mutation { createCampaign(input: { campaign: { {{fields}}, contactListId: "{{listId}}" } }) { campaign { id status } errors { __typename } } }
            """)).Data()["createCampaign"]!["campaign"]!["id"]!.GetValue<string>();

        var invalid = (await client.Gql($$"""mutation { startCampaign(input: { id: "{{id}}" }) { errors { __typename ... on ValidationError { fields { field } } } } }""")).Data()["startCampaign"]!;
        invalid["errors"]![0]!["fields"]![0]!["field"]!.GetValue<string>().ShouldBe("smsText");

        await client.Gql(
            "mutation($id: UUID!, $c: CampaignDtoInput!) { updateCampaign(input: { id: $id, campaign: $c }) { campaign { id } } }",
            new { id, c = new { name = "Spring", sendEmail = false, sendSms = true, sendPost = false, contactListId = listId, smsText = "Hi {{Name}}" } });
        var started = (await client.Gql($$"""mutation { startCampaign(input: { id: "{{id}}" }) { campaign { status } errors { __typename } } }""")).Data()["startCampaign"]!;
        started["campaign"]!["status"]!.GetValue<string>().ShouldBe("SENDING");

        JsonNode? campaign = null;
        for (var i = 0; i < 40 && campaign?["status"]?.GetValue<string>() != "SENT"; i++)
        {
            await Task.Delay(500, Ct);
            campaign = (await client.Gql($$"""{ campaign(id: "{{id}}") { status } campaignMessages(campaignId: "{{id}}") { nodes { status token } } }""")).Data();
            campaign = campaign["campaign"];
        }

        campaign!["status"]!.GetValue<string>().ShouldBe("SENT");
        (await client.GetAsync($"/campaigns/{id}/letters", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SeedListAsync()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(api.AppConnectionString).Options);
        var contact = new Contact { Name = "Ann", PhoneE164 = $"+49170{Random.Shared.Next(1_000_000, 9_999_999)}" };
        contact.RefreshSearchText();
        var list = new ContactList { Name = "Campaign test" };
        db.AddRange(contact, list, new ContactListMember { ContactListId = list.Id, ContactId = contact.Id });
        await db.SaveChangesAsync(Ct);
        return list.Id;
    }
}
```

The update uses GraphQL variables, because `{{Name}}` cannot be written inside a C# `$$"""` raw string. If HotChocolate names the input type differently from `CampaignDtoInput`, use the name in `web/schema.graphql`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*CampaignSenderTests"`
Expected: FAIL to compile (`CampaignSender` not found).

- [ ] **Step 3: Implement the sender, dispatcher and endpoint**

```csharp
// src/ElectroMarketing.Application/Campaigns/CampaignSender.cs
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Documents;
using Microsoft.Extensions.Logging;

namespace ElectroMarketing.Application.Campaigns;

/// <summary>
/// Sends the Queued messages of every Sending campaign. Only Queued rows are picked, so a restart resumes where it
/// stopped. Failures are stored per message and never stop the campaign.
/// </summary>
public sealed class CampaignSender(
    IRepository<Campaign, Guid> campaigns,
    ICampaignStore store,
    CampaignContent content,
    IEmailSender email,
    ISmsSender sms,
    ILetterRenderer letters,
    IObjectStorage storage,
    CampaignOptions options,
    TimeProvider time,
    ILogger<CampaignSender> logger)
{
    private const int ChunkSize = 100;

    public async Task SendPendingAsync(CancellationToken ct)
    {
        foreach (var campaign in await campaigns.ListAsync(c => c.Status == CampaignStatus.Sending, ct))
        {
            await RunAsync(campaign, ct);
        }
    }

    public async Task RunAsync(Campaign campaign, CancellationToken ct)
    {
        await SendAsync(campaign.Id, Channel.Email, options.EmailsPerSecond, (m, c) => email.SendAsync(content.Email(campaign, m.Contact!, m.Token), c), ct);
        await SendAsync(campaign.Id, Channel.Sms, options.SmsPerSecond, (m, c) => sms.SendAsync(m.Contact!.PhoneE164!, content.Sms(campaign, m.Contact!, m.Token), c), ct);
        await RenderLettersAsync(campaign, ct);

        campaign.Status = CampaignStatus.Sent;
        campaign.FinishedAtUtc = time.GetUtcNow();
        await campaigns.UpdateAsync(campaign, ct);
    }

    // ponytail: a crash between sending and saving a chunk re-sends up to ChunkSize messages; save per message if that matters.
    private async Task SendAsync(Guid campaignId, Channel channel, double perSecond, Func<CampaignMessage, CancellationToken, Task> send, CancellationToken ct)
    {
        var pause = TimeSpan.FromSeconds(1 / perSecond);
        while (await store.NextQueuedAsync(campaignId, channel, ChunkSize, ct) is { Count: > 0 } chunk)
        {
            foreach (var message in chunk)
            {
                try
                {
                    await send(message, ct);
                    message.Status = MessageStatus.Sent;
                    message.SentAtUtc = time.GetUtcNow();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.Status = MessageStatus.Failed;
                    message.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    logger.LogWarning(ex, "{Channel} message {MessageId} failed", channel, message.Id);
                }

                await Task.Delay(pause, time, ct);
            }

            await store.SaveAsync(ct);
        }
    }

    /// <summary>One merged PDF for all queued letters; "sent" means printed-ready.</summary>
    private async Task RenderLettersAsync(Campaign campaign, CancellationToken ct)
    {
        var queued = await store.NextQueuedAsync(campaign.Id, Channel.Post, int.MaxValue, ct);
        if (queued.Count == 0)
        {
            return;
        }

        var pdf = letters.Render(queued.Select(m => content.Letter(campaign, m.Contact!, m.Token)).ToList());
        using (var stream = new MemoryStream(pdf))
        {
            await storage.PutAsync(Document.Bucket, Campaign.LettersKey(campaign.Id), stream, "application/pdf", ct);
        }

        var now = time.GetUtcNow();
        foreach (var message in queued)
        {
            message.Status = MessageStatus.Sent;
            message.SentAtUtc = now;
        }

        campaign.LettersReady = true;
        await store.SaveAsync(ct);
    }
}
```

If the Application csproj does not reference `Microsoft.Extensions.Logging.Abstractions` yet, add the `PackageReference` (its version is already in `Directory.Packages.props` through the template; if not, add it under the Extensions label). Register `services.AddScoped<CampaignSender>();` in Application DI.

```csharp
// src/ElectroMarketing.Api/CampaignDispatcher.cs
using ElectroMarketing.Application.Campaigns;

namespace ElectroMarketing.Api;

/// <summary>Polls for Sending campaigns every 5 s. Not a RabbitMQ consumer: sending can outlast the broker's 30 min ack timeout.</summary>
public sealed class CampaignDispatcher(IServiceScopeFactory scopes, TimeProvider time, ILogger<CampaignDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ponytail: assumes one Api instance; lock campaign rows (FOR UPDATE SKIP LOCKED) before scaling out.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CampaignSender>().SendPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Campaign dispatch failed; retrying on the next tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
```

Register `services.AddHostedService<CampaignDispatcher>();` in `Api/DependencyInjection.cs`.

`FileEndpoints.cs`: add this to `MapFileEndpoints`, with usings `ElectroMarketing.Domain.Campaigns` and `ElectroMarketing.Domain.Documents`:

```csharp
        // Merged letters PDF of a campaign; the web BFF forwards /api/campaigns/{id}/letters here.
        app.MapGet("/campaigns/{id:guid}/letters", LettersAsync).RequireAuthorization(Permissions.Campaigns.Read);
```

Then add this method:

```csharp
    private static async Task<IResult> LettersAsync(Guid id, IObjectStorage storage, CancellationToken ct) =>
        await storage.GetAsync(Document.Bucket, Campaign.LettersKey(id), ct) is { } pdf
            ? Results.Stream(pdf.Content, "application/pdf", $"letters-{id}.pdf")
            : Results.NotFound();
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Infrastructure.Tests --filter-class "*CampaignSenderTests" && dotnet test --project tests/ElectroMarketing.Api.Tests --filter-class "*CampaignGraphQlTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(campaigns): background dispatcher sends e-mail, SMS and renders letters

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 17: Tracking endpoints

**Files:**
- Create: `src/ElectroMarketing.Application/Tracking/TrackingService.cs`, `src/ElectroMarketing.Api/TrackingEndpoints.cs`
- Modify: `src/ElectroMarketing.Application/DependencyInjection.cs`, `src/ElectroMarketing.Api/Program.cs`
- Test: `tests/ElectroMarketing.Api.Tests/TrackingTests.cs`

**Interfaces:**
- Consumes: `CampaignMessage`, `TrackingEvent`, `ShortToken`, `TrackingOptions`.
- Produces:
  - `TrackingService`: `RecordAsync(string token, TrackingEventType, string? userAgent, ct) : Task<bool>`, `LandingAsync(string token, string? userAgent, ct) : Task<LandingPage?>` and `UnsubscribeAsync(string token, string? userAgent, ct) : Task<bool>`.
  - `LandingPage(string Title, string Body)`.
  - Anonymous routes `GET /{token}`, `GET /o/{token}`, `GET` and `POST /u/{token}`, and `GET /landing/{token}`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Api.Tests/TrackingTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.Tests;

public sealed class TrackingTests(ApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(api.AppConnectionString).Options);

    private HttpClient Anonymous() => api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(string Token, Guid MessageId, Guid ContactId)> SeedAsync(Channel channel)
    {
        await using var db = Db();
        var contact = new Contact { Email = $"{Guid.NewGuid():N}@x.test" };
        contact.RefreshSearchText();
        var campaign = new Campaign { Name = "T", LandingTitle = "Solar panels", LandingBody = "Call us.", Status = CampaignStatus.Sent };
        var message = new CampaignMessage { CampaignId = campaign.Id, ContactId = contact.Id, Channel = channel, Token = ShortToken.New(), Status = MessageStatus.Sent };
        db.AddRange(contact, campaign, message);
        await db.SaveChangesAsync(Ct);
        return (message.Token, message.Id, contact.Id);
    }

    private async Task<string[]> EventsAsync(Guid messageId)
    {
        await using var db = Db();
        return await db.TrackingEvents.Where(e => e.MessageId == messageId).OrderBy(e => e.Id).Select(e => e.Type.ToString()).ToArrayAsync(Ct);
    }

    [Fact]
    public async Task Short_link_records_click_and_redirects_to_landing_page()
    {
        var (token, messageId, _) = await SeedAsync(Channel.Sms);
        using var client = Anonymous();

        using var response = await client.GetAsync($"/{token}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe($"https://localhost:3000/l/{token}");
        (await EventsAsync(messageId)).ShouldBe(["LinkClick"]);
    }

    [Fact]
    public async Task Unknown_or_malformed_tokens_are_404_and_graphql_route_still_wins()
    {
        using var client = Anonymous();

        (await client.GetAsync("/Zz9Zz9Z", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/landing/bad!", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/graphql", new { query = "{ __typename }" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Open_pixel_is_a_gif_even_for_unknown_tokens()
    {
        var (token, messageId, _) = await SeedAsync(Channel.Email);
        using var client = Anonymous();

        using var known = await client.GetAsync($"/o/{token}", Ct);
        using var unknown = await client.GetAsync("/o/Zz9Zz9Z", Ct);

        known.Content.Headers.ContentType!.MediaType.ShouldBe("image/gif");
        unknown.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await EventsAsync(messageId)).ShouldBe(["EmailOpen"]);
    }

    [Fact]
    public async Task Get_on_unsubscribe_only_asks_and_post_opts_out()
    {
        var (token, messageId, contactId) = await SeedAsync(Channel.Email);
        using var client = Anonymous();

        (await client.GetStringAsync($"/u/{token}", Ct)).ShouldContain("<form method=\"post\"");
        await using (var db = Db())
        {
            (await db.Contacts.SingleAsync(c => c.Id == contactId, Ct)).EmailOptOut.ShouldBeFalse();
        }

        using var post = await client.PostAsync($"/u/{token}", new FormUrlEncodedContent(new Dictionary<string, string> { ["List-Unsubscribe"] = "One-Click" }), Ct);

        post.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var read = Db();
        (await read.Contacts.SingleAsync(c => c.Id == contactId, Ct)).EmailOptOut.ShouldBeTrue();
        (await EventsAsync(messageId)).ShouldBe(["Unsubscribe"]);
    }

    [Fact]
    public async Task Landing_returns_campaign_text_and_records_a_view()
    {
        var (token, messageId, _) = await SeedAsync(Channel.Post);
        using var client = Anonymous();

        var page = await client.GetFromJsonAsync<JsonNode>($"/landing/{token}", Ct);

        (page!["title"]!.GetValue<string>(), page["body"]!.GetValue<string>()).ShouldBe(("Solar panels", "Call us."));
        (await EventsAsync(messageId)).ShouldBe(["LandingView"]);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Api.Tests --filter-class "*TrackingTests"`
Expected: FAIL. The click test gets 404 instead of 302.

- [ ] **Step 3: Implement**

```csharp
// src/ElectroMarketing.Application/Tracking/TrackingService.cs
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Tracking;

public sealed record LandingPage(string Title, string Body);

/// <summary>Anonymous tracking by message token. Only the user agent is stored, never the IP address.</summary>
public sealed class TrackingService(
    IRepository<CampaignMessage, Guid> messages,
    IRepository<TrackingEvent, Guid> events,
    IRepository<Contact, Guid> contacts,
    IRepository<Campaign, Guid> campaigns,
    TimeProvider time)
{
    public async Task<bool> RecordAsync(string token, TrackingEventType type, string? userAgent, CancellationToken ct = default)
    {
        var message = await FindAsync(token, ct);
        if (message is null)
        {
            return false;
        }

        await AddEventAsync(message, type, userAgent, ct);
        return true;
    }

    public async Task<LandingPage?> LandingAsync(string token, string? userAgent, CancellationToken ct = default)
    {
        var message = await FindAsync(token, ct);
        if (message is null || await campaigns.GetByIdAsync(message.CampaignId, ct) is not { } campaign)
        {
            return null;
        }

        await AddEventAsync(message, TrackingEventType.LandingView, userAgent, ct);
        return new LandingPage(campaign.LandingTitle ?? campaign.Name, campaign.LandingBody ?? string.Empty);
    }

    /// <summary>Opts the contact out of the message's channel (SMS → SMS, everything else → e-mail).</summary>
    public async Task<bool> UnsubscribeAsync(string token, string? userAgent, CancellationToken ct = default)
    {
        var message = await FindAsync(token, ct);
        if (message is null || await contacts.GetByIdAsync(message.ContactId, ct) is not { } contact)
        {
            return false;
        }

        if (message.Channel == Channel.Sms)
        {
            contact.SmsOptOut = true;
        }
        else
        {
            contact.EmailOptOut = true;
        }

        await contacts.UpdateAsync(contact, ct);
        await AddEventAsync(message, TrackingEventType.Unsubscribe, userAgent, ct);
        return true;
    }

    private async Task<CampaignMessage?> FindAsync(string token, CancellationToken ct) =>
        ShortToken.IsValid(token) ? (await messages.ListAsync(m => m.Token == token, ct)).SingleOrDefault() : null;

    private Task<TrackingEvent> AddEventAsync(CampaignMessage message, TrackingEventType type, string? userAgent, CancellationToken ct) =>
        events.AddAsync(
            new TrackingEvent { MessageId = message.Id, Type = type, OccurredAtUtc = time.GetUtcNow(), UserAgent = userAgent?.Length > 512 ? userAgent[..512] : userAgent },
            ct);
}
```

```csharp
// src/ElectroMarketing.Api/TrackingEndpoints.cs
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Application.Tracking;
using ElectroMarketing.Domain.Campaigns;

namespace ElectroMarketing.Api;

/// <summary>
/// Public short-link routes. <c>/{token}</c> sits at the root so links stay short; literal routes such as
/// <c>/graphql</c> and <c>/health</c> take precedence over the parameter route.
/// </summary>
public static class TrackingEndpoints
{
    // 1×1 transparent GIF.
    private static readonly byte[] Pixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    public static IEndpointRouteBuilder MapTrackingEndpoints(this IEndpointRouteBuilder app)
    {
        var tracking = app.MapGroup(string.Empty).AllowAnonymous();
        tracking.MapGet($"/{{token:length({ShortToken.Length})}}", ClickAsync);
        tracking.MapGet("/o/{token}", OpenAsync);
        tracking.MapGet("/u/{token}", AskUnsubscribe);
        tracking.MapPost("/u/{token}", UnsubscribeAsync).DisableAntiforgery();
        tracking.MapGet("/landing/{token}", LandingAsync);
        return app;
    }

    private static async Task<IResult> ClickAsync(string token, HttpRequest request, TrackingService tracking, TrackingOptions options, CancellationToken ct) =>
        await tracking.RecordAsync(token, TrackingEventType.LinkClick, UserAgent(request), ct)
            ? Results.Redirect($"{options.LandingBaseUrl.TrimEnd('/')}/l/{token}")
            : Results.NotFound();

    /// <summary>Always answers with the pixel so mail clients never show a broken image.</summary>
    private static async Task<IResult> OpenAsync(string token, HttpRequest request, HttpResponse response, TrackingService tracking, CancellationToken ct)
    {
        await tracking.RecordAsync(token, TrackingEventType.EmailOpen, UserAgent(request), ct);
        response.Headers.CacheControl = "no-store";
        return Results.File(Pixel, "image/gif");
    }

    /// <summary>GET only asks: mail security scanners fetch every link, so a GET must never unsubscribe.</summary>
    private static IResult AskUnsubscribe(string token) =>
        ShortToken.IsValid(token)
            ? Html("""<form method="post"><p>Do you want to stop receiving these messages?</p><button>Unsubscribe</button></form>""")
            : Results.NotFound();

    /// <summary>Form button and RFC 8058 one-click POST from mail clients.</summary>
    private static async Task<IResult> UnsubscribeAsync(string token, HttpRequest request, TrackingService tracking, CancellationToken ct) =>
        await tracking.UnsubscribeAsync(token, UserAgent(request), ct)
            ? Html("<p>You are unsubscribed. You will not receive these messages any more.</p>")
            : Results.NotFound();

    private static async Task<IResult> LandingAsync(string token, HttpRequest request, TrackingService tracking, CancellationToken ct) =>
        await tracking.LandingAsync(token, UserAgent(request), ct) is { } page ? Results.Ok(page) : Results.NotFound();

    private static string? UserAgent(HttpRequest request) => request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private static IResult Html(string body) => Results.Content(
        $"""<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Subscription</title><body style="font-family:system-ui;max-width:32rem;margin:3rem auto;padding:0 1rem">{body}</body></html>""",
        "text/html; charset=utf-8");
}
```

Register `services.AddScoped<TrackingService>();` in Application DI. In `Program.cs`, add `app.MapTrackingEndpoints();` after `app.MapFileEndpoints();`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Api.Tests --filter-class "*TrackingTests"`
Expected: PASS (5 tests). `LandingBaseUrl` comes from `appsettings.json` (`https://localhost:3000`).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(tracking): short-link click, open pixel, safe unsubscribe and landing data

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 18: Campaign pages, landing page, letters download (web)

**Files:**
- Create: `web/src/components/CampaignsPage.tsx`, `web/src/components/CampaignPage.tsx`, `web/src/app/app/campaigns/page.tsx`, `web/src/app/app/campaigns/[id]/page.tsx`, `web/src/app/l/[token]/page.tsx`, `web/src/app/api/campaigns/[id]/letters/route.ts`
- Modify: `web/src/components/AppNav.tsx`
- Test: `web/src/components/CampaignPage.test.tsx`

**Interfaces:**
- Consumes: GraphQL from Tasks 13 and 16; REST `GET /landing/{token}` and `GET /campaigns/{id}/letters`.
- Produces:
  - Routes `/app/campaigns` and `/app/campaigns/[id]`.
  - Public route `/l/[token]` (outside the `proxy.ts` matcher, so no login is needed).
  - The `CampaignPage({ id })` component. Task 20 adds `<CampaignInsightsView>` to it.

- [ ] **Step 1: Write the failing test**

```tsx
// web/src/components/CampaignPage.test.tsx
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { graphql, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "@/test/server";
import { CampaignPage } from "./CampaignPage";

const draft = {
  id: "k1",
  name: "Spring",
  status: "DRAFT",
  contactListId: "l1",
  sendEmail: false,
  sendSms: true,
  sendPost: false,
  emailSubject: null,
  emailBody: null,
  smsText: "Hi {{Name}}, see {{Link}}",
  letterBody: null,
  landingTitle: null,
  landingBody: null,
  startedAtUtc: null,
  finishedAtUtc: null,
  lettersReady: false,
};

describe("CampaignPage", () => {
  it("previews the SMS with sample values and shows start validation errors", async () => {
    server.use(
      graphql.query("CampaignEditor", () =>
        HttpResponse.json({ data: { campaign: draft, contactLists: { nodes: [{ id: "l1", name: "Fair", memberCount: 3 }] } } }),
      ),
      graphql.mutation("SaveCampaign", () => HttpResponse.json({ data: { updateCampaign: { campaign: { id: "k1" }, errors: null } } })),
      graphql.mutation("StartCampaign", () =>
        HttpResponse.json({
          data: {
            startCampaign: {
              campaign: null,
              errors: [{ __typename: "ConflictError", message: "No contact in the list can be reached on the selected channels." }],
            },
          },
        }),
      ),
    );
    render(<CampaignPage id="k1" />);

    expect(await screen.findByText("Hi Erika Mustermann, see https://short.link/Ab3dE5g")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Save and start" }));
    await waitFor(() => expect(screen.getByRole("alert").textContent).toContain("No contact in the list can be reached"));
  });
});
```

The `window.confirm` call in `CampaignPage` is stubbed in the test setup: add `vi.stubGlobal("confirm", () => true)` to `web/vitest.setup.ts` if it is not there.

- [ ] **Step 2: Run it to verify it fails**

Run: `cd web && bun run test src/components/CampaignPage.test.tsx`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement the pages**

```tsx
// web/src/components/CampaignPage.tsx
"use client";

import { useCallback, useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { CampaignDtoInput, CampaignEditorQuery, CampaignMessagesQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";

const CampaignEditorDocument = graphql(`
  query CampaignEditor($id: UUID!) {
    campaign(id: $id) {
      id
      name
      status
      contactListId
      sendEmail
      sendSms
      sendPost
      emailSubject
      emailBody
      smsText
      letterBody
      landingTitle
      landingBody
      startedAtUtc
      finishedAtUtc
      lettersReady
    }
    contactLists(first: 100) {
      nodes {
        id
        name
        memberCount
      }
    }
  }
`);

const CampaignMessagesDocument = graphql(`
  query CampaignMessages($id: UUID!, $after: String) {
    campaignMessages(campaignId: $id, first: 100, after: $after) {
      totalCount
      pageInfo {
        hasNextPage
        endCursor
      }
      nodes {
        id
        channel
        status
        token
        sentAtUtc
        error
        contact {
          id
          name
          email
          phone
        }
      }
    }
  }
`);

const SaveCampaignDocument = graphql(`
  mutation SaveCampaign($input: UpdateCampaignInput!) {
    updateCampaign(input: $input) {
      campaign {
        id
      }
      errors {
        __typename
        ... on Error {
          message
        }
        ... on ValidationError {
          fields {
            field
            messages
          }
        }
      }
    }
  }
`);

const StartCampaignDocument = graphql(`
  mutation StartCampaign($input: StartCampaignInput!) {
    startCampaign(input: $input) {
      campaign {
        id
      }
      errors {
        __typename
        ... on Error {
          message
        }
        ... on ValidationError {
          fields {
            field
            messages
          }
        }
      }
    }
  }
`);

type Campaign = NonNullable<CampaignEditorQuery["campaign"]>;
type Message = NonNullable<NonNullable<CampaignMessagesQuery["campaignMessages"]>["nodes"]>[number];

const sample = { Name: "Erika Mustermann", Link: "https://short.link/Ab3dE5g", UnsubscribeLink: "https://short.link/u/Ab3dE5g" };

/** Client-side preview with the same {{Key}} placeholders the Api renders. */
function preview(template: string | null | undefined) {
  return (template ?? "").replace(/\{\{(\w+)\}\}/g, (m, key: string) => sample[key as keyof typeof sample] ?? m);
}

const editable = [
  "name",
  "contactListId",
  "sendEmail",
  "sendSms",
  "sendPost",
  "emailSubject",
  "emailBody",
  "smsText",
  "letterBody",
  "landingTitle",
  "landingBody",
] as const;

export function CampaignPage({ id }: { id: string }) {
  const [campaign, setCampaign] = useState<Campaign | null>(null);
  const [lists, setLists] = useState<{ id: string; name: string; memberCount: number }[]>([]);
  const [messages, setMessages] = useState<Message[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    const d = await execute(CampaignEditorDocument, { id });
    setCampaign(d.campaign ?? null);
    setLists(d.contactLists?.nodes ?? []);
    if (d.campaign && d.campaign.status !== "DRAFT") {
      const m = await execute(CampaignMessagesDocument, { id, after: null });
      setMessages(m.campaignMessages?.nodes ?? []);
    }
  }, [id]);

  useEffect(() => {
    load().catch((e: Error) => setError(e.message));
  }, [load]);

  // Refresh while sending.
  useEffect(() => {
    if (campaign?.status !== "SENDING") return;
    const timer = setInterval(() => load().catch(() => {}), 5000);
    return () => clearInterval(timer);
  }, [campaign?.status, load]);

  if (!campaign) return error ? <p role="alert">{error}</p> : <p>Loading…</p>;

  const set = <K extends keyof Campaign>(key: K, value: Campaign[K]) => setCampaign({ ...campaign, [key]: value });
  const input = () => ({ id, campaign: Object.fromEntries(editable.map((k) => [k, campaign[k] ?? null])) as CampaignDtoInput });

  async function save(): Promise<boolean> {
    const { updateCampaign } = await execute(SaveCampaignDocument, { input: input() });
    setError(messageOf(updateCampaign.errors));
    return !updateCampaign.errors?.length;
  }

  async function start() {
    setError(null);
    try {
      if (!(await save())) return;
      if (!window.confirm("Send this campaign now? This cannot be undone.")) return;
      const { startCampaign } = await execute(StartCampaignDocument, { input: { id } });
      if (!startCampaign.campaign) return setError(messageOf(startCampaign.errors));
      await load();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  if (campaign.status !== "DRAFT") {
    const counts = messages.reduce<Record<string, number>>((acc, m) => {
      const key = `${m.channel.toLowerCase()} ${m.status.toLowerCase()}`;
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-semibold">{campaign.name}</h1>
        <p>
          Status: {campaign.status.toLowerCase()}
          {campaign.startedAtUtc && ` · started ${new Date(campaign.startedAtUtc).toLocaleString()}`}
        </p>
        <ul className="flex flex-wrap gap-3 text-sm">
          {Object.entries(counts).map(([k, n]) => (
            <li key={k} className="rounded-md border border-border px-2 py-1">
              {k}: {n}
            </li>
          ))}
        </ul>
        {campaign.lettersReady && (
          <a href={`/api/campaigns/${id}/letters`} className="underline">
            Download letters (PDF)
          </a>
        )}
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left">
              <th className="px-2 py-1">Contact</th>
              <th className="px-2 py-1">Channel</th>
              <th className="px-2 py-1">Status</th>
              <th className="px-2 py-1">Sent</th>
              <th className="px-2 py-1">Note</th>
            </tr>
          </thead>
          <tbody>
            {messages.map((m) => (
              <tr key={m.id} className="border-t border-border">
                <td className="px-2 py-1">{m.contact?.name ?? m.contact?.email ?? m.contact?.phone}</td>
                <td className="px-2 py-1">{m.channel.toLowerCase()}</td>
                <td className="px-2 py-1">{m.status.toLowerCase()}</td>
                <td className="px-2 py-1">{m.sentAtUtc && new Date(m.sentAtUtc).toLocaleString()}</td>
                <td className="px-2 py-1">{m.error}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  const field = "mt-1 block w-full rounded-md border border-border bg-background px-2 py-1";
  return (
    <div className="space-y-4">
      <label className="block text-sm">
        Name
        <input value={campaign.name} onChange={(e) => set("name", e.target.value)} className={field} />
      </label>
      <label className="block text-sm">
        Contact list
        <select value={campaign.contactListId ?? ""} onChange={(e) => set("contactListId", e.target.value || null)} className={field}>
          <option value="">— pick a list —</option>
          {lists.map((l) => (
            <option key={l.id} value={l.id}>
              {l.name} ({l.memberCount})
            </option>
          ))}
        </select>
      </label>
      <fieldset className="flex gap-4 text-sm">
        <legend>Channels</legend>
        <label>
          <input type="checkbox" checked={campaign.sendEmail} onChange={(e) => set("sendEmail", e.target.checked)} /> E-Mail
        </label>
        <label>
          <input type="checkbox" checked={campaign.sendSms} onChange={(e) => set("sendSms", e.target.checked)} /> SMS
        </label>
        <label>
          <input type="checkbox" checked={campaign.sendPost} onChange={(e) => set("sendPost", e.target.checked)} /> Post
        </label>
      </fieldset>
      <p className="text-sm text-muted">Placeholders: {"{{Name}}"}, {"{{Link}}"}, {"{{UnsubscribeLink}}"}. A link is added when missing.</p>
      {campaign.sendEmail && (
        <section className="space-y-2">
          <label className="block text-sm">
            E-Mail subject
            <input value={campaign.emailSubject ?? ""} onChange={(e) => set("emailSubject", e.target.value)} className={field} />
          </label>
          <label className="block text-sm">
            E-Mail body (HTML)
            <textarea rows={8} value={campaign.emailBody ?? ""} onChange={(e) => set("emailBody", e.target.value)} className={field} />
          </label>
          <iframe title="E-Mail preview" sandbox="" srcDoc={preview(campaign.emailBody)} className="h-48 w-full rounded-md border border-border bg-white" />
        </section>
      )}
      {campaign.sendSms && (
        <section className="space-y-2">
          <label className="block text-sm">
            SMS text
            <textarea rows={3} value={campaign.smsText ?? ""} onChange={(e) => set("smsText", e.target.value)} className={field} />
          </label>
          <p className="rounded-md bg-surface p-2 text-sm">{preview(campaign.smsText)}</p>
        </section>
      )}
      {campaign.sendPost && (
        <label className="block text-sm">
          Letter text (a QR code with the link is printed below it)
          <textarea rows={8} value={campaign.letterBody ?? ""} onChange={(e) => set("letterBody", e.target.value)} className={field} />
        </label>
      )}
      <section className="space-y-2">
        <h2 className="font-semibold">Landing page (opened from links and QR codes)</h2>
        <input aria-label="Landing title" value={campaign.landingTitle ?? ""} onChange={(e) => set("landingTitle", e.target.value)} className={field} />
        <textarea aria-label="Landing text" rows={5} value={campaign.landingBody ?? ""} onChange={(e) => set("landingBody", e.target.value)} className={field} />
      </section>
      {error && (
        <p role="alert" className="text-sm text-danger">
          {error}
        </p>
      )}
      <div className="flex gap-2">
        <button type="button" onClick={() => save().catch((e: Error) => setError(e.message))} className="rounded-md border border-border px-4 py-1.5">
          Save draft
        </button>
        <button type="button" onClick={start} className="rounded-md bg-primary px-4 py-1.5 text-on-primary">
          Save and start
        </button>
      </div>
    </div>
  );
}
```

```tsx
// web/src/components/CampaignsPage.tsx
"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { CampaignsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";
import { messageOf } from "./ImportWizard";

const CampaignsDocument = graphql(`
  query Campaigns {
    campaigns(first: 100) {
      nodes {
        id
        name
        status
        sendEmail
        sendSms
        sendPost
        createdAtUtc
        startedAtUtc
      }
    }
  }
`);

const CreateCampaignDocument = graphql(`
  mutation CreateCampaign($input: CreateCampaignInput!) {
    createCampaign(input: $input) {
      campaign {
        id
      }
      errors {
        __typename
        ... on Error {
          message
        }
      }
    }
  }
`);

type Row = NonNullable<NonNullable<CampaignsQuery["campaigns"]>["nodes"]>[number];

export function CampaignsPage() {
  const router = useRouter();
  const [rows, setRows] = useState<Row[]>([]);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    execute(CampaignsDocument).then((d) => setRows(d.campaigns?.nodes ?? []), (e: Error) => setError(e.message));
  }, []);

  async function create() {
    const { createCampaign } = await execute(CreateCampaignDocument, {
      input: { campaign: { name, sendEmail: true, sendSms: false, sendPost: false } },
    });
    if (createCampaign.campaign) router.push(`/app/campaigns/${createCampaign.campaign.id}`);
    else setError(messageOf(createCampaign.errors));
  }

  const channels = (c: Row) => [c.sendEmail && "E-Mail", c.sendSms && "SMS", c.sendPost && "Post"].filter(Boolean).join(", ");

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Campaigns</h1>
      <div className="flex gap-2">
        <input
          aria-label="New campaign name"
          placeholder="New campaign name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="rounded-md border border-border bg-background px-2 py-1"
        />
        <button type="button" disabled={!name.trim()} onClick={create} className="rounded-md bg-primary px-4 py-1.5 text-on-primary disabled:opacity-50">
          Create
        </button>
      </div>
      {error && <p role="alert">{error}</p>}
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Name</th>
            <th className="px-2 py-1">Channels</th>
            <th className="px-2 py-1">Status</th>
            <th className="px-2 py-1">Started</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((c) => (
            <tr key={c.id} className="border-t border-border">
              <td className="px-2 py-1">
                <Link href={`/app/campaigns/${c.id}`} className="underline">
                  {c.name}
                </Link>
              </td>
              <td className="px-2 py-1">{channels(c)}</td>
              <td className="px-2 py-1">{c.status.toLowerCase()}</td>
              <td className="px-2 py-1">{c.startedAtUtc && new Date(c.startedAtUtc).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

```tsx
// web/src/app/app/campaigns/page.tsx
import { CampaignsPage } from "@/components/CampaignsPage";

export const metadata = { title: "Campaigns" };

export default function Page() {
  return <CampaignsPage />;
}
```

```tsx
// web/src/app/app/campaigns/[id]/page.tsx
import { CampaignPage } from "@/components/CampaignPage";

export const metadata = { title: "Campaign" };

export default async function Page({ params }: { params: Promise<{ id: string }> }) {
  return <CampaignPage id={(await params).id} />;
}
```

```tsx
// web/src/app/l/[token]/page.tsx
import { notFound } from "next/navigation";

export const dynamic = "force-dynamic";
export const metadata = { title: "More information", robots: { index: false } };

/** Public page behind every short link and QR code. Content comes from the campaign; the layout gets customized later. */
export default async function Landing({ params }: { params: Promise<{ token: string }> }) {
  const { token } = await params;
  if (!/^[0-9A-Za-z]{7}$/.test(token)) notFound();
  const res = await fetch(`${process.env.API_URL}/landing/${token}`, {
    cache: "no-store",
    headers: { "user-agent": "electromarketing-web" },
  });
  if (!res.ok) notFound();
  const page = (await res.json()) as { title: string; body: string };
  return (
    <main className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="text-3xl font-semibold">{page.title}</h1>
      <p className="mt-6 whitespace-pre-line">{page.body}</p>
    </main>
  );
}
```

On the landing fetch, the server-side request sends a fixed user agent. The visitor's user agent is recorded on the click (`GET /{token}`). The landing view only counts that the page rendered.

```ts
// web/src/app/api/campaigns/[id]/letters/route.ts
import { auth } from "@/auth";

/** Letters PDF: browser → here → Api → S3, like document content. */
export const GET = auth(async (req, { params }) => {
  const session = req.auth;
  if (!session?.accessToken || session.error) return new Response("Not authenticated", { status: 401 });
  const id = ((await params) as { id: string }).id;
  const res = await fetch(`${process.env.API_URL}/campaigns/${encodeURIComponent(id)}/letters`, {
    headers: { authorization: `Bearer ${session.accessToken}` },
    cache: "no-store",
  });
  const headers = new Headers({ "cache-control": "private, no-store" });
  for (const name of ["content-type", "content-length", "content-disposition"]) {
    const value = res.headers.get(name);
    if (value) headers.set(name, value);
  }
  return new Response(res.body, { status: res.status, headers });
});
```

`AppNav.tsx`: add `{ href: "/app/campaigns", label: "Campaigns", permission: "campaigns.read" }` after Lists.

- [ ] **Step 4: Run the test and checks**

Run: `cd web && bun run codegen && bun run test && bun run lint && bun run typecheck`
Expected: all pass.

- [ ] **Step 5: Manual verification (agent-browser)**

1. Create a campaign with all 3 channels for a list of 3 contacts, then save and start.
2. In Mailpit (Aspire dashboard → mailpit ui), open the e-mail. The pixel loads, and the campaign's `campaignMessages` show the open event in the database.
3. Click the link. The browser goes to `/l/<token>` with the landing title.
4. The Api log shows `SMS (not sent, Twilio not configured)` with a 7-character short link.
5. Download the letters PDF, scan the QR with a phone or a QR decoder, and confirm it shows the printed link.
6. Open `/u/<token>`. It shows a button and does not unsubscribe. Pressing the button sets `emailOptOut` on the contact.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(web): campaign editor with previews, campaign status, letters download, public landing page

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

# Milestone 4 — Insights

### Task 19: Insights computation and API

**Files:**
- Create: `src/ElectroMarketing.Application/Insights/CampaignInsights.cs`, `src/ElectroMarketing.Api/GraphQl/Insights/InsightsFeature.cs`
- Modify: `src/ElectroMarketing.Application/DependencyInjection.cs`, `Permissions.cs`, `auth-roles.json`
- Test: `tests/ElectroMarketing.Application.Tests/CampaignInsightsTests.cs`

**Interfaces:**
- Consumes: `Campaign`, `CampaignMessage`, `TrackingEvent`, `CampaignOptions.TimeZone`.
- Produces:
  - Fact records: `MessageFact(Guid Id, Channel Channel, MessageStatus Status, DateTimeOffset? SentAtUtc)` and `EventFact(Guid MessageId, TrackingEventType Type, DateTimeOffset OccurredAtUtc)`.
  - Result records: `ChannelStats`, `LatencyBucket(string Label, int Count)`, `TimelinePoint(DateTimeOffset Start, int Opens, int Clicks)`, `HeatCell(int Weekday, int Hour, int Count)` and `CampaignInsightsResult`.
  - `CampaignInsights.Compute(Campaign, IReadOnlyList<MessageFact>, IReadOnlyList<EventFact>, TimeZoneInfo)`.
  - `InsightsService`: `ForCampaignAsync(Guid, ct)` and `RecentAsync(int take, ct)`.
  - GraphQL: `campaignInsights(campaignId)` and `recentCampaignInsights(take)`.
  - `Permissions.Insights.Read = "insights.read"`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ElectroMarketing.Application.Tests/CampaignInsightsTests.cs
using ElectroMarketing.Application.Insights;
using ElectroMarketing.Domain.Campaigns;

namespace ElectroMarketing.Application.Tests;

public class CampaignInsightsTests
{
    private static readonly DateTimeOffset Sent = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero); // Monday 09:00 Berlin
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly MessageFact mail1 = new(Guid.CreateVersion7(), Channel.Email, MessageStatus.Sent, Sent);
    private readonly MessageFact mail2 = new(Guid.CreateVersion7(), Channel.Email, MessageStatus.Sent, Sent);
    private readonly MessageFact mail3 = new(Guid.CreateVersion7(), Channel.Email, MessageStatus.Failed, null);
    private readonly MessageFact sms1 = new(Guid.CreateVersion7(), Channel.Sms, MessageStatus.Sent, Sent);
    private readonly MessageFact post1 = new(Guid.CreateVersion7(), Channel.Post, MessageStatus.Skipped, null);

    private CampaignInsightsResult Compute(params EventFact[] events) =>
        CampaignInsights.Compute(
            new Campaign { Name = "Spring", StartedAtUtc = Sent },
            [mail1, mail2, mail3, sms1, post1],
            events,
            Berlin);

    [Fact]
    public void Channel_stats_count_unique_messages_and_rates_against_sent()
    {
        var result = Compute(
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(5)),
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(9)),
            new(mail1.Id, TrackingEventType.LinkClick, Sent.AddMinutes(10)),
            new(mail2.Id, TrackingEventType.EmailOpen, Sent.AddHours(2)),
            new(sms1.Id, TrackingEventType.LinkClick, Sent.AddDays(2)),
            new(sms1.Id, TrackingEventType.Unsubscribe, Sent.AddDays(2)));

        var email = result.Channels.Single(c => c.Channel == Channel.Email);
        (email.Sent, email.Failed, email.Opened, email.Clicked, email.OpenRate, email.ClickRate).ShouldBe((2, 1, 2, 1, 1.0, 0.5));
        var sms = result.Channels.Single(c => c.Channel == Channel.Sms);
        (sms.Clicked, sms.Unsubscribed, sms.ClickRate).ShouldBe((1, 1, 1.0));
        result.Channels.Single(c => c.Channel == Channel.Post).Skipped.ShouldBe(1);
    }

    [Fact]
    public void Response_latency_uses_first_click_or_landing_view()
    {
        var result = Compute(
            new(mail1.Id, TrackingEventType.LandingView, Sent.AddMinutes(30)),
            new(mail1.Id, TrackingEventType.LinkClick, Sent.AddHours(5)),
            new(mail2.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(1)),
            new(sms1.Id, TrackingEventType.LinkClick, Sent.AddDays(2)));

        result.ResponseLatency.Select(b => (b.Label, b.Count)).ShouldBe([("< 1 h", 1), ("1–6 h", 0), ("6–24 h", 0), ("1–3 d", 1), ("> 3 d", 0)]);
        result.MedianResponseHours.ShouldBe(48);
    }

    [Fact]
    public void Timeline_is_hourly_in_local_time_and_heatmap_counts_responses()
    {
        var result = Compute(
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(5)),
            new(mail1.Id, TrackingEventType.LinkClick, Sent.AddMinutes(20)),
            new(mail2.Id, TrackingEventType.LinkClick, Sent.AddHours(1).AddMinutes(1)));

        result.Timeline.Select(p => (p.Start.Hour, p.Opens, p.Clicks)).ShouldBe([(9, 1, 1), (10, 0, 1)]);
        result.Timeline[0].Start.Offset.ShouldBe(TimeSpan.FromHours(1));
        result.ByWeekdayHour.ShouldBe([new HeatCell(1, 9, 1), new HeatCell(1, 10, 1)]);
    }

    [Fact]
    public void No_events_gives_zero_rates_and_no_median() =>
        Compute().MedianResponseHours.ShouldBeNull();
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/ElectroMarketing.Application.Tests --filter-class "*CampaignInsightsTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement**

```csharp
// src/ElectroMarketing.Application/Insights/CampaignInsights.cs
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Exceptions;

namespace ElectroMarketing.Application.Insights;

public sealed record MessageFact(Guid Id, Channel Channel, MessageStatus Status, DateTimeOffset? SentAtUtc);

public sealed record EventFact(Guid MessageId, TrackingEventType Type, DateTimeOffset OccurredAtUtc);

/// <summary>Counts are unique messages. Rates are per sent message. E-mail opens are approximate (Apple Mail Privacy Protection pre-loads pixels).</summary>
public sealed record ChannelStats(Channel Channel, int Sent, int Failed, int Skipped, int Opened, int Clicked, int LandingViews, int Unsubscribed, double OpenRate, double ClickRate);

public sealed record LatencyBucket(string Label, int Count);

/// <summary><see cref="Start"/> is in the configured time zone: hourly buckets for the first 3 days, daily after.</summary>
public sealed record TimelinePoint(DateTimeOffset Start, int Opens, int Clicks);

/// <summary><see cref="Weekday"/> 0 = Sunday (<see cref="DayOfWeek"/>), local time.</summary>
public sealed record HeatCell(int Weekday, int Hour, int Count);

public sealed record CampaignInsightsResult(
    Guid CampaignId,
    string CampaignName,
    DateTimeOffset? StartedAtUtc,
    IReadOnlyList<ChannelStats> Channels,
    IReadOnlyList<LatencyBucket> ResponseLatency,
    double? MedianResponseHours,
    IReadOnlyList<TimelinePoint> Timeline,
    IReadOnlyList<HeatCell> ByWeekdayHour);

public static class CampaignInsights
{
    private static readonly (string Label, TimeSpan UpTo)[] Buckets =
    [
        ("< 1 h", TimeSpan.FromHours(1)),
        ("1–6 h", TimeSpan.FromHours(6)),
        ("6–24 h", TimeSpan.FromHours(24)),
        ("1–3 d", TimeSpan.FromDays(3)),
        ("> 3 d", TimeSpan.MaxValue),
    ];

    /// <summary>A response is a click on the short link (or QR scan) or a landing page view.</summary>
    private static bool IsResponse(TrackingEventType type) => type is TrackingEventType.LinkClick or TrackingEventType.LandingView;

    // ponytail: in-memory over all messages and events of one campaign (fine to ~100k); move to SQL aggregates if campaigns grow.
    public static CampaignInsightsResult Compute(Campaign campaign, IReadOnlyList<MessageFact> messages, IReadOnlyList<EventFact> events, TimeZoneInfo zone)
    {
        var byMessage = events.ToLookup(e => e.MessageId);

        var channels = messages.GroupBy(m => m.Channel).OrderBy(g => g.Key).Select(g =>
        {
            int Count(Func<MessageFact, bool> predicate) => g.Count(predicate);
            int With(Func<TrackingEventType, bool> type) => g.Count(m => byMessage[m.Id].Any(e => type(e.Type)));
            var sent = Count(m => m.Status == MessageStatus.Sent);
            var opened = With(t => t == TrackingEventType.EmailOpen);
            var clicked = With(IsResponse);
            return new ChannelStats(
                g.Key,
                sent,
                Count(m => m.Status == MessageStatus.Failed),
                Count(m => m.Status == MessageStatus.Skipped),
                opened,
                clicked,
                With(t => t == TrackingEventType.LandingView),
                With(t => t == TrackingEventType.Unsubscribe),
                Rate(opened, sent),
                Rate(clicked, sent));
        }).ToList();

        var latencies = messages
            .Where(m => m.SentAtUtc is not null)
            .Select(m => byMessage[m.Id].Where(e => IsResponse(e.Type)).Select(e => (TimeSpan?)(e.OccurredAtUtc - m.SentAtUtc!.Value)).Min())
            .OfType<TimeSpan>()
            .Select(t => t < TimeSpan.Zero ? TimeSpan.Zero : t)
            .Order()
            .ToList();
        var buckets = Buckets.Select((b, i) => new LatencyBucket(b.Label, latencies.Count(l => l < b.UpTo && (i == 0 || l >= Buckets[i - 1].UpTo)))).ToList();
        double? median = latencies.Count == 0 ? null : Math.Round(latencies[latencies.Count / 2].TotalHours, 1);

        var relevant = events.Where(e => e.Type == TrackingEventType.EmailOpen || IsResponse(e.Type)).ToList();
        var hourly = relevant.Count == 0 || campaign.StartedAtUtc is null || relevant.Max(e => e.OccurredAtUtc) - campaign.StartedAtUtc <= TimeSpan.FromDays(3);
        var timeline = relevant
            .GroupBy(e => Truncate(TimeZoneInfo.ConvertTime(e.OccurredAtUtc, zone), hourly))
            .OrderBy(g => g.Key)
            .Select(g => new TimelinePoint(g.Key, g.Count(e => e.Type == TrackingEventType.EmailOpen), g.Count(e => e.Type == TrackingEventType.LinkClick)))
            .ToList();

        var heat = events
            .Where(e => e.Type == TrackingEventType.LinkClick)
            .Select(e => TimeZoneInfo.ConvertTime(e.OccurredAtUtc, zone))
            .GroupBy(t => (Weekday: (int)t.DayOfWeek, t.Hour))
            .OrderBy(g => g.Key.Weekday).ThenBy(g => g.Key.Hour)
            .Select(g => new HeatCell(g.Key.Weekday, g.Key.Hour, g.Count()))
            .ToList();

        return new CampaignInsightsResult(campaign.Id, campaign.Name, campaign.StartedAtUtc, channels, buckets, median, timeline, heat);
    }

    private static double Rate(int count, int sent) => sent == 0 ? 0 : Math.Round((double)count / sent, 4);

    private static DateTimeOffset Truncate(DateTimeOffset local, bool hourly) =>
        new(local.Year, local.Month, local.Day, hourly ? local.Hour : 0, 0, 0, local.Offset);
}

public sealed class InsightsService(
    IRepository<Campaign, Guid> campaigns,
    IRepository<CampaignMessage, Guid> messages,
    IRepository<TrackingEvent, Guid> events,
    CampaignOptions options)
{
    public async Task<CampaignInsightsResult> ForCampaignAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetByIdAsync(campaignId, ct) ?? throw NotFoundException.For<Campaign>(campaignId);
        return await ComputeAsync(campaign, ct);
    }

    /// <summary>The last <paramref name="take"/> (1–20) started campaigns, newest first.</summary>
    public async Task<IReadOnlyList<CampaignInsightsResult>> RecentAsync(int take, CancellationToken ct = default)
    {
        var started = (await campaigns.ListAsync(c => c.Status != CampaignStatus.Draft, ct))
            .OrderByDescending(c => c.StartedAtUtc)
            .Take(Math.Clamp(take, 1, 20));
        var result = new List<CampaignInsightsResult>();
        foreach (var campaign in started)
        {
            result.Add(await ComputeAsync(campaign, ct));
        }

        return result;
    }

    private async Task<CampaignInsightsResult> ComputeAsync(Campaign campaign, CancellationToken ct)
    {
        var m = await messages.ListAsync(x => x.CampaignId == campaign.Id, ct);
        var e = await events.ListAsync(x => x.Message!.CampaignId == campaign.Id, ct);
        return CampaignInsights.Compute(
            campaign,
            m.Select(x => new MessageFact(x.Id, x.Channel, x.Status, x.SentAtUtc)).ToList(),
            e.Select(x => new EventFact(x.MessageId, x.Type, x.OccurredAtUtc)).ToList(),
            TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone));
    }
}
```

`InsightsService` depends on `CampaignOptions`, which only `AddCampaignSending` registers. That is fine because only the Api resolves `InsightsService`. Register `services.AddScoped<InsightsService>();` in Application DI.

```csharp
// src/ElectroMarketing.Api/GraphQl/Insights/InsightsFeature.cs
using ElectroMarketing.Application.Insights;
using ElectroMarketing.Auth;
using HotChocolate.Authorization;

namespace ElectroMarketing.Api.GraphQl.Insights;

[ExtendObjectType(typeof(RootQuery))]
public class InsightsQueries : RootQuery
{
    [Authorize(Policy = Permissions.Insights.Read)]
    public Task<CampaignInsightsResult> GetCampaignInsights(Guid campaignId, [Service] InsightsService insights, CancellationToken ct) =>
        insights.ForCampaignAsync(campaignId, ct);

    [Authorize(Policy = Permissions.Insights.Read)]
    public Task<IReadOnlyList<CampaignInsightsResult>> GetRecentCampaignInsights([Service] InsightsService insights, CancellationToken ct, int take = 10) =>
        insights.RecentAsync(take, ct);
}
```

Permissions: add `public static class Insights { public const string Read = "insights.read"; }`. Grant it to `user` and `platform-admin`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/ElectroMarketing.Application.Tests --filter-class "*CampaignInsightsTests" && dotnet test --project tests/ElectroMarketing.Api.Tests`
Expected: PASS. Then regenerate the contract.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(insights): per-channel rates, response latency, local-time timeline and heatmap

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 20: Insights pages, docs, final verification

**Files:**
- Create: `web/src/components/CampaignInsightsView.tsx`, `web/src/components/InsightsPage.tsx`, `web/src/app/app/insights/page.tsx`
- Modify: `web/package.json` (add `recharts`), `web/src/components/CampaignPage.tsx`, `web/src/components/AppNav.tsx`, `README.md`

**Interfaces:**
- Consumes: GraphQL `campaignInsights` and `recentCampaignInsights` (Task 19).
- Produces: route `/app/insights`, and `CampaignInsightsView({ campaignId })` embedded on non-draft campaign pages.

- [ ] **Step 1: Load the dataviz skill**

Invoke the `dataviz` skill before writing chart code. Use its palette and its mark specs for the bar, line and heat grid below. Keep colors as CSS variables from `globals.css`.

- [ ] **Step 2: Add recharts and implement the views**

```bash
cd web && bun add recharts@<newest stable, ≥2 weeks old>
```

```tsx
// web/src/components/CampaignInsightsView.tsx
"use client";

import { useEffect, useState } from "react";
import { Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { graphql } from "@/gql";
import type { CampaignInsightsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";

const CampaignInsightsDocument = graphql(`
  query CampaignInsights($id: UUID!) {
    campaignInsights(campaignId: $id) {
      medianResponseHours
      channels {
        channel
        sent
        failed
        skipped
        opened
        clicked
        landingViews
        unsubscribed
        openRate
        clickRate
      }
      responseLatency {
        label
        count
      }
      timeline {
        start
        opens
        clicks
      }
      byWeekdayHour {
        weekday
        hour
        count
      }
    }
  }
`);

const percent = new Intl.NumberFormat(undefined, { style: "percent", maximumFractionDigits: 1 });
const weekdays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

export function CampaignInsightsView({ campaignId }: { campaignId: string }) {
  const [data, setData] = useState<CampaignInsightsQuery["campaignInsights"] | null>(null);

  useEffect(() => {
    execute(CampaignInsightsDocument, { id: campaignId }).then((d) => setData(d.campaignInsights), () => setData(null));
  }, [campaignId]);

  if (!data) return null;
  const max = Math.max(1, ...data.byWeekdayHour.map((c) => c.count));
  const timeline = data.timeline.map((p) => ({ ...p, label: new Date(p.start).toLocaleString(undefined, { weekday: "short", hour: "2-digit", day: "2-digit" }) }));

  return (
    <section aria-label="Insights" className="space-y-6">
      <h2 className="text-lg font-semibold">Insights</h2>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Channel</th>
            <th className="px-2 py-1">Sent</th>
            <th className="px-2 py-1">Failed / skipped</th>
            <th className="px-2 py-1">Opened*</th>
            <th className="px-2 py-1">Clicked / scanned</th>
            <th className="px-2 py-1">Unsubscribed</th>
          </tr>
        </thead>
        <tbody>
          {data.channels.map((c) => (
            <tr key={c.channel} className="border-t border-border">
              <td className="px-2 py-1">{c.channel.toLowerCase()}</td>
              <td className="px-2 py-1">{c.sent}</td>
              <td className="px-2 py-1">
                {c.failed} / {c.skipped}
              </td>
              <td className="px-2 py-1">{c.channel === "EMAIL" ? `${c.opened} (${percent.format(c.openRate)})` : "—"}</td>
              <td className="px-2 py-1">
                {c.clicked} ({percent.format(c.clickRate)})
              </td>
              <td className="px-2 py-1">{c.unsubscribed}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-xs text-muted">* E-mail opens are approximate: some mail apps load the tracking image automatically. Clicks are reliable.</p>

      <div>
        <h3 className="font-semibold">
          Time to first response{data.medianResponseHours != null && ` · median ${data.medianResponseHours} h`}
        </h3>
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={data.responseLatency}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            <XAxis dataKey="label" />
            <YAxis allowDecimals={false} />
            <Tooltip />
            <Bar dataKey="count" name="Responses" fill="var(--color-primary)" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div>
        <h3 className="font-semibold">Activity over time</h3>
        <ResponsiveContainer width="100%" height={240}>
          <LineChart data={timeline}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            <XAxis dataKey="label" minTickGap={24} />
            <YAxis allowDecimals={false} />
            <Tooltip />
            <Legend />
            <Line type="monotone" dataKey="opens" name="Opens" stroke="var(--color-muted)" dot={false} />
            <Line type="monotone" dataKey="clicks" name="Clicks" stroke="var(--color-primary)" dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div>
        <h3 className="font-semibold">Clicks by weekday and hour</h3>
        <div className="grid grid-cols-[3rem_repeat(24,minmax(0,1fr))] gap-px text-[10px]">
          <span />
          {Array.from({ length: 24 }, (_, h) => (
            <span key={h} className="text-center text-muted">
              {h}
            </span>
          ))}
          {weekdays.map((day, w) => (
            <div key={day} className="contents">
              <span>{day}</span>
              {Array.from({ length: 24 }, (_, h) => {
                const count = data.byWeekdayHour.find((c) => c.weekday === w && c.hour === h)?.count ?? 0;
                return (
                  <span
                    key={h}
                    title={`${day} ${h}:00 · ${count} clicks`}
                    className="aspect-square rounded-sm"
                    style={{ background: "var(--color-primary)", opacity: count === 0 ? 0.06 : 0.2 + (0.8 * count) / max }}
                  />
                );
              })}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
```

```tsx
// web/src/components/InsightsPage.tsx
"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { graphql } from "@/gql";
import type { RecentInsightsQuery } from "@/gql/graphql";
import { execute } from "@/lib/graphql";

const RecentInsightsDocument = graphql(`
  query RecentInsights {
    recentCampaignInsights(take: 10) {
      campaignId
      campaignName
      startedAtUtc
      medianResponseHours
      channels {
        channel
        sent
        openRate
        clickRate
        unsubscribed
      }
    }
  }
`);

const percent = new Intl.NumberFormat(undefined, { style: "percent", maximumFractionDigits: 1 });

export function InsightsPage() {
  const [rows, setRows] = useState<RecentInsightsQuery["recentCampaignInsights"]>([]);

  useEffect(() => {
    execute(RecentInsightsDocument).then((d) => setRows(d.recentCampaignInsights), () => setRows([]));
  }, []);

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Insights</h1>
      <p className="text-sm text-muted">Last 10 campaigns. Open a campaign for timing details.</p>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="px-2 py-1">Campaign</th>
            <th className="px-2 py-1">Started</th>
            <th className="px-2 py-1">Channel</th>
            <th className="px-2 py-1">Sent</th>
            <th className="px-2 py-1">Opens*</th>
            <th className="px-2 py-1">Clicks / scans</th>
            <th className="px-2 py-1">Unsubscribed</th>
            <th className="px-2 py-1">Median response</th>
          </tr>
        </thead>
        <tbody>
          {rows.flatMap((r) =>
            r.channels.map((c, i) => (
              <tr key={`${r.campaignId}-${c.channel}`} className={i === 0 ? "border-t border-border" : ""}>
                <td className="px-2 py-1">
                  {i === 0 && (
                    <Link href={`/app/campaigns/${r.campaignId}`} className="underline">
                      {r.campaignName}
                    </Link>
                  )}
                </td>
                <td className="px-2 py-1">{i === 0 && r.startedAtUtc && new Date(r.startedAtUtc).toLocaleDateString()}</td>
                <td className="px-2 py-1">{c.channel.toLowerCase()}</td>
                <td className="px-2 py-1">{c.sent}</td>
                <td className="px-2 py-1">{c.channel === "EMAIL" ? percent.format(c.openRate) : "—"}</td>
                <td className="px-2 py-1">{percent.format(c.clickRate)}</td>
                <td className="px-2 py-1">{c.unsubscribed}</td>
                <td className="px-2 py-1">{i === 0 && r.medianResponseHours != null && `${r.medianResponseHours} h`}</td>
              </tr>
            )),
          )}
        </tbody>
      </table>
      <p className="text-xs text-muted">* E-mail opens are approximate.</p>
    </div>
  );
}
```

```tsx
// web/src/app/app/insights/page.tsx
import { InsightsPage } from "@/components/InsightsPage";

export const metadata = { title: "Insights" };

export default function Page() {
  return <InsightsPage />;
}
```

In `CampaignPage.tsx`, inside the non-draft branch after the letters link, render `<CampaignInsightsView campaignId={id} />`, imported from `./CampaignInsightsView`. In `AppNav.tsx`, add `{ href: "/app/insights", label: "Insights", permission: "insights.read" }`. If the CSS variable names differ from `--color-primary`, `--color-border` and `--color-muted`, use the names defined in `web/src/app/globals.css`.

- [ ] **Step 3: Checks**

Run: `cd web && bun run codegen && bun run lint && bun run typecheck && bun run test`
Expected: all pass. The `CampaignPage` test still passes because insights render only for non-draft campaigns.

- [ ] **Step 4: README**

Add a "Campaigns & tracking" section to `README.md`. It covers:
- Short-link routes (`/{token}`, `/o/{token}`, `/u/{token}`, `/landing/{token}`). Production needs a short domain routed by Caddy to the Api, configured in `Tracking__BaseUrl`.
- Mailpit in dev. Twilio settings, and the fact that without them SMS are only logged.
- The QuestPDF Community license threshold.
- The single-Api-instance assumption of `CampaignDispatcher`.
- E-mail open accuracy.

- [ ] **Step 5: Full verification**

```bash
dotnet build ElectroMarketing.slnx -p:CI=true
for p in tests/*/; do dotnet test --project "$p" || break; done
cd web && bun run lint && bun run typecheck && bun run test && cd ..
docker compose -f compose.prod.yaml build api
```
Expected: 0 warnings and all tests green. The Api image builds.

Then generate letters once inside the built Api image to confirm QuestPDF's native library and the cultures work there:
1. Start the stack (`docker compose -f compose.prod.yaml up -d`) with `.env` from `.env.example`.
2. Run a Post campaign.
3. Download the PDF.

End-to-end in Aspire (agent-browser):
1. Import a German CSV and an update CSV.
2. Search and build a list.
3. Send a 3-channel campaign.
4. Open the e-mail in Mailpit and click its link.
5. Open the SMS link from the log.
6. Scan the letter QR.
7. Unsubscribe via POST.
8. `/app/insights` and the campaign page show the matching counts: 1 open, 3 clicks, latency bucket "< 1 h", and the heat cell for the current hour.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(insights): campaign insights charts and overview; docs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
