using System.Reflection;

namespace ElectroMarketing.Auth;

/// <summary>
/// Every permission is a constant; one authorization policy per constant is generated at startup (AU-13).
/// Resolvers use <c>[Authorize(Policy = Permissions.Documents.Write)]</c>, never roles.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    private static readonly Lazy<IReadOnlyList<string>> AllPermissions = new(() => typeof(Permissions)
        .GetNestedTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList());

    public static IReadOnlyList<string> All => AllPermissions.Value;

    public static class Documents
    {
        public const string Read = "documents.read";
        public const string Write = "documents.write";
    }

    public static class Queues
    {
        public const string Manage = "queues.manage";
    }
}

public static class Roles
{
    public const string User = "user";
    public const string PlatformAdmin = "platform-admin";
}

public static class ElectroMarketingClaims
{
    /// <summary>Local <c>AppUser.Id</c>, added by the claims transformation.</summary>
    public const string UserId = "electromarketing:user_id";
    public const string Subject = "sub";
    public const string Email = "email";
}
