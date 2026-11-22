using ElectroMarketing.Application.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElectroMarketing.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void Registers_use_case_services()
    {
        var services = new ServiceCollection().AddApplication(Substitute.For<IConfiguration>());

        services.ShouldContain(d => d.ServiceType == typeof(DocumentService));
        services.ShouldContain(d => d.ServiceType == typeof(TimeProvider));
    }
}
