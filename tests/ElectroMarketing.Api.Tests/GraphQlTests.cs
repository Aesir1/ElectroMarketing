using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElectroMarketing.Api.Tests;

/// <summary>GraphQL end to end in process: auth, policies, queues, documents.</summary>
public sealed class GraphQlTests(ApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Anonymous_request_is_401()
    {
        using var client = api.CreateClient();
        using var response = await client.PostAsJsonAsync("/graphql", new { query = "{ viewer { email } }" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_for_another_audience_is_401()
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.Token("x@local.test", ["user"], audience: "other-api"));
        using var response = await client.PostAsJsonAsync("/graphql", new { query = "{ viewer { email } }" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Viewer_is_provisioned_with_role_permissions()
    {
        using var client = api.ClientFor("viewer@local.test", "user");

        var viewer = (await client.Gql("{ viewer { id email roles permissions } }")).Data()["viewer"]!;

        viewer["email"]!.GetValue<string>().ShouldBe("viewer@local.test");
        Guid.Parse(viewer["id"]!.GetValue<string>()).Version.ShouldBe(7);
        viewer["permissions"]!.AsArray().Select(p => p!.GetValue<string>()).ShouldContain("documents.write");
        viewer["permissions"]!.AsArray().Select(p => p!.GetValue<string>()).ShouldNotContain("queues.manage");
    }

    [Fact]
    public async Task Queue_admin_requires_platform_admin()
    {
        using var user = api.ClientFor("plain@local.test", "user");
        var denied = await user.Gql("{ queues { name } }");
        denied["errors"]![0]!["extensions"]!["code"]!.GetValue<string>().ShouldBe("AUTH_NOT_AUTHORIZED");

        using var admin = api.ClientFor("admin@local.test", "user", "platform-admin");
        var queue = $"test-{Guid.CreateVersion7():N}";
        (await admin.Gql($$"""mutation { declareQueue(input: { name: "{{queue}}", routingKey: "test.*" }) { string } }""")).Data();
        JsonArray? queues = null;
        for (var i = 0; i < 30 && queues?.Any(q => q!["name"]!.GetValue<string>() == queue) != true; i++)
        {
            await Task.Delay(500, Ct); // management API stats lag a few seconds behind declarations
            queues = (await admin.Gql("{ queues { name messages consumers } }"))["data"]?["queues"]?.AsArray();
        }

        queues!.Select(q => q!["name"]!.GetValue<string>()).ShouldContain(queue);
        var purged = await admin.Gql($$"""mutation { purgeQueue(input: { name: "{{queue}}.dlq" }) { unsignedInt } }""");
        purged["errors"].Json().ShouldBe("null", purged.Json());
    }

    [Fact]
    public async Task Document_content_goes_through_the_api_and_only_to_its_owner()
    {
        using var owner = api.ClientFor("docs@local.test", "user");
        var upload = (await owner.Gql("""
            mutation { requestDocumentUpload(input: { fileName: "a.txt", contentType: "text/plain", sizeBytes: 5 }) {
              documentUploadPayload { uploadUrl document { id status downloadUrl } } errors { __typename } } }
            """)).Data()["requestDocumentUpload"]!;
        var payload = upload["documentUploadPayload"]!;
        var id = payload["document"]!["id"]!.GetValue<string>();
        payload["uploadUrl"]!.GetValue<string>().ShouldBe($"/api/documents/{id}/content");
        payload["document"]!["status"]!.GetValue<string>().ShouldBe("PENDING");
        payload["document"]!["downloadUrl"].Json().ShouldBe("null");

        // The web BFF strips /api and forwards to the Api with the user's token.
        var content = $"/documents/{id}/content";
        using var other = api.ClientFor("other@local.test", "user");
        using var anonymous = api.CreateClient();
        (await anonymous.PutAsync(content, Text("hello"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await other.PutAsync(content, Text("hello"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await owner.PutAsync(content, Text("hello", "image/png"), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await owner.PutAsync(content, Text("hello"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var confirmed = (await owner.Gql($$"""mutation { confirmDocumentUpload(input: { id: "{{id}}" }) { document { downloadUrl } } }"""))
            .Data()["confirmDocumentUpload"]!["document"]!;
        confirmed["downloadUrl"]!.GetValue<string>().ShouldBe($"/api/documents/{id}/content");

        using var download = await owner.GetAsync(content, Ct);
        (await download.Content.ReadAsStringAsync(Ct)).ShouldBe("hello");
        download.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        download.Content.Headers.ContentDisposition!.FileName.ShouldBe("a.txt");
        (await other.GetAsync(content, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync(content, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await other.Gql("{ documents { totalCount } }")).Data()["documents"]!["totalCount"]!.GetValue<int>().ShouldBe(0);
        (await owner.Gql("{ documents { totalCount } }")).Data()["documents"]!["totalCount"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public async Task Branding_is_served_anonymously_but_only_from_the_branding_bucket()
    {
        var storage = api.Services.GetRequiredService<Application.Abstractions.IObjectStorage>();
        await storage.PutAsync("branding", "logo.svg", new MemoryStream("<svg/>"u8.ToArray()), "image/svg+xml", Ct);
        using var anonymous = api.CreateClient();

        using var logo = await anonymous.GetAsync("/branding/logo.svg", Ct);
        (await logo.Content.ReadAsStringAsync(Ct)).ShouldBe("<svg/>");
        logo.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
        (await anonymous.GetAsync("/branding/missing.png", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/branding/../documents/x", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static StringContent Text(string body, string contentType = "text/plain") => new(body, System.Text.Encoding.UTF8, contentType);
}
