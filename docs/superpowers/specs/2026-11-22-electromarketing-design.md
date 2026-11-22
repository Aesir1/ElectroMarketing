# ElectroMarketing — design & build plan

## Context
New project in `~/RiderProjects/ElectroMarketing` (now only an empty `ElectroMarketing.slnx`, no git).
Use `~/RiderProjects/DataHub` as the template: same stack, layering, conventions, tests, CI.
Goal: a marketing tool in 4 milestones:
1. CSV contact import into categories, with per-file provenance and upsert.
2. Contact lists (from an import lot or hand-picked through a full-text search).
3. Campaigns over E-Mail / SMS / Post (PDF + QR) with short random tracking links.
4. Insights per campaign (rates, timing, response latency).

Decisions taken (user answers):
- Contact match key: normalized e-mail, fallback normalized phone.
- Senders: SMTP (MailKit) + Twilio, behind a small sender interface.
- Post: the app generates PDFs to print (one per contact + one merged). No letter API.
- One plan, 4 milestones, built and verified in order.

## Stack (copied from DataHub)
- .NET 10 (`global.json` SDK 10.0.401), central package management, same `Directory.Build.props`, `.editorconfig`, analyzers.
- Aspire 13.5.4 AppHost: Postgres 17, Keycloak, MinIO, RabbitMQ, **plus Mailpit** for dev SMTP.
- HotChocolate 16 GraphQL (paging/filtering/sorting/projections); minimal API only for files + tracking redirects.
- EF Core 10 + Npgsql, code-first migrations through DbUtils; outbox + RabbitMQ consumers for background work.
- Next.js 16 + Bun + Tailwind v4 + Auth.js (Keycloak) + GraphQL codegen; Vitest + MSW, Playwright.
- Tests: xunit.v3 on MTP, Shouldly, NSubstitute, Testcontainers, Respawn, NetArchTest.
- New packages only: `CsvHelper`, `libphonenumber-csharp`, `MailKit`, `QuestPDF`, `QRCoder`, `recharts` (web), `CommunityToolkit.Aspire.Hosting.MailPit`. Twilio over a plain `HttpClient` form POST (no SDK needed).
- Note: QuestPDF Community license is free below USD 1M annual revenue. Check this applies.

## M0 — Skeleton from template
1. `git init`. Copy DataHub with rsync. Exclude: `bin obj node_modules .next .git .env .idea graphify-out SqlScripts server-provision loadtest aspire/*/.certs appsettings.User.json backup-secrets.txt`.
2. Drop `src/DataHub.Webhook` and its tests, its AppHost resource and its compose service.
3. Rename `DataHub` → `ElectroMarketing` (files, folders, namespaces, Docker image names, Keycloak realm, `UserSecretsId`). Use the existing `ElectroMarketing.slnx` instead of `.sln` and add all projects to it.
4. Remove the DataHub sample features (Products, Containers, Temperatures/Ingestion), with their tests, GraphQL types, web pages and seed data. Keep Documents (upload to MinIO) as the base for the import upload. Keep Auth, outbox, messaging, DbUtils and ServiceDefaults.
5. Delete the old migrations. Create a fresh `Initial` migration per context.
6. Generate new dev secrets in AppHost `appsettings.json`. Do not reuse DataHub values.
7. Update `README.md` and `CLAUDE.md`, adapt `.github/workflows/ci.yml`.
8. Verify: `dotnet build -p:CI=true`, all test projects green, AppHost starts, login on web works.

## Domain model (all `AuditableEntity`, UUID v7, `...AtUtc`, enums as strings)
- `Category` — `Name` (unique, case-insensitive).
- `ImportBatch` (the "lot") — `CategoryId`, `FileName`, `ObjectKey` (raw CSV in MinIO), `Status` (Pending/Running/Done/Failed), counters `Rows/Created/Updated/Unchanged/Skipped`, `Errors` jsonb (row number + reason), `ColumnMapping` jsonb, `Culture`.
- `Contact` — `Name`, `Email`, `EmailNormalized` (unique where not null), `Phone`, `PhoneE164` (unique where not null), `Street`, `PostalCode`, `City`, `Country`, `ObtainedAtUtc`, `EmailOptOut`, `SmsOptOut`. `SearchText` is a generated, stored, lower-case concat of all fields, with a `pg_trgm` GIN index.
- `ContactImport` — `ContactId`, `ImportBatchId`, `RowNumber`, `Change` (Created/Updated/Unchanged), `Changes` jsonb `{field: {old, new}}`. This gives "which files created/changed this contact". A contact's categories derive from its batches.
- `ContactList` — `Name`, `SourceBatchId?`. `ContactListMember` (ListId, ContactId, PK on both).
- `Campaign` — `Name`, `ContactListId`, `Channels` (flags: Email | Sms | Post), `EmailSubject`, `EmailBody`, `SmsText`, `LetterBody`, `LandingTitle`, `LandingBody`, `Status` (Draft/Sending/Sent), `StartedAtUtc`.
- `CampaignMessage` — `CampaignId`, `ContactId`, `Channel`, `Token` (unique), `Status` (Queued/Sent/Failed/Skipped), `SentAtUtc`, `Error`. One row per contact per channel. Recipients are snapshotted at send time, so list edits or deletes do not change a sent campaign.
- `TrackingEvent` — `MessageId`, `Type` (EmailOpen/LinkClick/LandingView/Unsubscribe), `OccurredAtUtc`, `UserAgent`. Store no raw IP (GDPR).

## M1 — CSV import
- **Upload:** reuse the Documents 3-step flow (`requestDocumentUpload` → PUT to MinIO → confirm). Allowed type `text/csv` already exists.
- **Preview:** mutation `previewImport(documentId)` returns the detected delimiter (`;` or `,`), the encoding (UTF-8 with BOM, fallback Windows-1252), headers, the first 10 rows and an auto column mapping.
  - Header aliases cover EN and DE, e.g. `E-Mail/Email/Mail`, `Telefon/Phone/Tel/Mobil`, `Name`, `Straße/Street/Adresse/Address`, `PLZ/Zip`, `Ort/City`, `Land/Country`, `Datum/Date`.
  - The UI shows the mapping as dropdowns the user can override.
- **Start:** mutation `startImport(documentId, categoryId | newCategoryName, mapping, culture = de-DE)` creates the `ImportBatch` and publishes `ImportRequested` through the outbox. The consumer processes the batch.
- **Per-row upsert** (`ContactImporter`, Application layer, pure logic plus repository):
  1. Normalize: e-mail trimmed and lower-case, validated by `MailAddress.TryCreate`. Phone to E.164 via libphonenumber, default region DE. Date via the culture formats + ISO.
  2. Reject rows with no e-mail and no valid phone: Skipped, with the reason stored in `Errors`.
  3. Match by `EmailNormalized`, else by `PhoneE164`. If e-mail and phone match different contacts, the e-mail match wins and the conflict is logged in `Errors`.
  4. Update rule: a non-empty incoming value overwrites a different stored value. An empty value never erases. `ObtainedAtUtc` keeps the newest date.
  5. Every row writes a `ContactImport` with the field diff. A duplicate row in the same file applies in order (last wins).
  6. Commit in chunks of 1000 rows. Batch counters update at the end; status Failed on exception, with the partial commits kept and reported.
- **UI:**
  - Import wizard: upload, pick or create a category, map columns, start.
  - Imports page: list of batches with status, counters, errors, CSV download.
  - Contact detail: fields plus a history table (batch, file, category, date, changed fields, file download link).
- **Tests:** importer unit tests (match/update/empty/conflict/duplicate/date cultures/phone normalize); Testcontainers test for the unique indexes and the upsert; Vitest for the wizard.

## M2 — Search & lists
- **Search:** GraphQL query `contacts(search, categoryIds, batchIds, obtainedFrom/To)` with cursor paging and sorting.
  - `search` splits on whitespace. Each term must match: `SearchText ILIKE %term%`, served by the trigram index.
  - The phone digits of a term are also matched against `PhoneE164`.
  - Every field is searchable, partial matches included.
- **Lists:**
  - `createListFromBatch(batchId, name)` copies the batch's contacts.
  - `createList(name)`, `addToList(listId, contactIds | currentSearchFilter)` ("add all N matches"), `removeFromList`, `renameList`, `deleteList`.
- **UI:** Contacts page with a debounced search bar, filter chips, a checkbox selection with "add selected / add all matching to list…" and a lists page with members.
- **Tests:** search SQL against Testcontainers (multi-term, umlauts, partial phone); list mutations.

## M3 — Campaigns & tracking
- **Short tokens:** 7 chars base62 from `RandomNumberGenerator` (62^7 ≈ 3.5·10^12 possibilities). Unique index, regenerate on collision.
  - Public URLs use `Tracking:BaseUrl` (a short domain in prod, routed by Caddy to the Api).
  - `GET /{token}` records a LinkClick and redirects 302 to the web landing page `/l/{token}`.
  - `GET /o/{token}.gif` records an EmailOpen and returns a 1×1 gif.
  - `GET /u/{token}` handles unsubscribe: it sets the opt-out flag and records the event.
  - All three are anonymous and rate-limited. An unknown token gives 404.
- **Landing page:** public Next.js route `/l/[token]` (outside auth). It calls an anonymous GraphQL query `landingPage(token)`, which returns the campaign's `LandingTitle`/`LandingBody` and records a LandingView.
  - Plain layout for now. The content fields make later customization a data change.
- **Send:** mutation `startCampaign(id)`:
  - Snapshots the list members into one `CampaignMessage` per channel.
  - Skips contacts with no address for that channel or with opt-out (status Skipped).
  - Publishes `CampaignStarted`.
  - A consumer sends in chunks, with a configurable rate per channel.
- **Senders:** `IEmailSender` → MailKit SMTP (Mailpit in dev). `ISmsSender` → Twilio REST. If Twilio is not configured, a logging fake sender is used.
  - Templates use simple `{{Name}}`, `{{Link}}`, `{{UnsubscribeLink}}` placeholder replacement.
  - E-mail gets the open pixel, a tracked link and an unsubscribe link.
  - SMS text = message + short link.
- **Post:** `LetterPdf` (QuestPDF) builds a DIN 5008 letter: address window, letter body, a QR code (QRCoder PNG) of the short link and the printed short link below it.
  - Output: one PDF per contact plus one merged PDF, stored in MinIO and downloadable from the campaign page.
  - Messages are marked Sent when the PDF is generated.
- **UI:** campaign editor (pick list, channels, texts, preview per channel, test send to self) and a campaign detail page (message status table, PDF download).
- **Tests:** token generator; placeholder rendering; tracking endpoints (WebApplicationFactory: event recorded, redirect, 404, opt-out); consumer with fake senders; PDF smoke test (QR decodes to the link).

## M4 — Insights
- SQL views (embedded `Persistence/Sql/*.sql`, the DataHub pattern): per campaign × channel counts for sent/failed/skipped, unique opens, unique clicks, landing views, unsubscribes, and the rates.
- Response latency: first event − `SentAtUtc` per message, shown as buckets (<1h, 1–6h, 6–24h, 1–3d, >3d) and as the median.
- Timeline: events per hour/day since start. Breakdown by weekday × hour.
- Drill-down: the list of responding contacts with event times; and the per-contact timeline on the contact page.
- **UI:** an insights overview (last campaigns, compared) and a campaign insights page with recharts charts.
- E-mail opens are flagged as approximate. Apple Mail Privacy Protection prefetches the pixel, so clicks are the reliable metric.
- **Tests:** view queries against seeded events in Testcontainers.

## Permissions
New constants in `Auth/Permissions.cs` (`Contacts.Read/Write`, `Imports.Write`, `Lists.Write`, `Campaigns.Read/Send`, `Insights.Read`), with grants in `DbUtils/MasterData/auth-roles.json`.

## Skipped (add later when needed)
- Twilio delivery-status callbacks (SMS status stays "accepted by provider").
- Uploading a custom PDF letter template (v1 uses a fixed QuestPDF layout with editable body text).
- Scheduled sends, A/B tests, rich HTML e-mail editor.
- server-provision and loadtest folders (copy from DataHub when deploying to the same host).

## Verification per milestone
- `dotnet build -p:CI=true`; `dotnet test --project tests/ElectroMarketing.<X>.Tests` for every test project; `cd web && bun run lint && bun run typecheck && bun run test`.
- `dotnet run --project aspire/ElectroMarketing.AppHost`, then run seed from the dashboard and click through with agent-browser:
  - **M1:** import a `;`-separated German CSV, then a second CSV changing 2 contacts. The contact history shows both files and the diffs.
  - **M2:** search a partial phone or a city, add the results to a list, create a list from a batch.
  - **M3:** send a 3-channel campaign. The e-mail arrives in Mailpit; opening it records an open, and clicking the link redirects to the landing page and records the click. The fake SMS log has the short link. The PDF QR code scans to the link.
  - **M4:** the insights page shows counts, rates and latency buckets that match the recorded events.

## Process after approval
1. Save this design as `docs/superpowers/specs/2026-11-22-electromarketing-design.md` and commit it (after `git init` in M0).
2. Write the detailed implementation plan with the writing-plans skill.
3. Execute M0 → M4, with the verification above at the end of each milestone.
