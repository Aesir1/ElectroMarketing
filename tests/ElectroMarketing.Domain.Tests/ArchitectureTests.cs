using System.Reflection;
using NetArchTest.Rules;

namespace ElectroMarketing.Domain.Tests;

/// <summary>Layer references point inward only (BE-4).</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Containers.Container).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Auth = typeof(Auth.DependencyInjection).Assembly;

    public static TheoryData<string, string[]> Rules => new()
    {
        { "Domain", ["ElectroMarketing.Application", "ElectroMarketing.Infrastructure", "ElectroMarketing.Auth", "ElectroMarketing.Api", "ElectroMarketing.DbUtils", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions", "RabbitMQ", "Amazon", "HotChocolate"] },
        { "Application", ["ElectroMarketing.Infrastructure", "ElectroMarketing.Auth", "ElectroMarketing.Api", "ElectroMarketing.DbUtils", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "Amazon.S3", "Microsoft.AspNetCore"] },
        { "Infrastructure", ["ElectroMarketing.Auth", "ElectroMarketing.Api", "ElectroMarketing.DbUtils"] },
        { "Auth", ["ElectroMarketing.Infrastructure", "ElectroMarketing.Api", "ElectroMarketing.DbUtils"] },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void Layer_does_not_depend_on_outer_layers(string layer, string[] forbidden)
    {
        var assembly = layer switch
        {
            "Domain" => Domain,
            "Application" => Application,
            "Infrastructure" => Infrastructure,
            _ => Auth,
        };

        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.ShouldBeTrue($"{layer} violates layering: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
