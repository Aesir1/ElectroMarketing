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
