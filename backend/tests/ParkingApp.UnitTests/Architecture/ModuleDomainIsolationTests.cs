using FluentAssertions;
using NetArchTest.Rules;

namespace ParkingApp.UnitTests.Architecture;

/// <summary>
/// NetArchTest isolation for each module Domain assembly (A1 / X11).
/// Domains must not depend on host Application/Infrastructure/API or other modules' Application/Infrastructure.
/// </summary>
public class ModuleDomainIsolationTests
{
    private static readonly string[] ForbiddenDependencies =
    [
        "ParkingApp.Application",
        "ParkingApp.Infrastructure",
        "ParkingApp.API",
        "ParkingApp.Identity.Application",
        "ParkingApp.Identity.Infrastructure",
        "ParkingApp.Marketplace.Application",
        "ParkingApp.Marketplace.Infrastructure",
        "ParkingApp.Corporate.Application",
        "ParkingApp.Corporate.Infrastructure",
        "ParkingApp.Messaging.Application",
        "ParkingApp.Messaging.Infrastructure",
        "ParkingApp.Notifications.Application",
        "ParkingApp.Notifications.Infrastructure",
    ];

    public static IEnumerable<object[]> DomainAssemblies()
    {
        yield return [typeof(ParkingApp.Identity.Domain.Entities.User).Assembly, "Identity"];
        yield return [typeof(ParkingApp.Marketplace.Domain.Entities.ParkingSpace).Assembly, "Marketplace"];
        yield return [typeof(ParkingApp.Corporate.Domain.Company).Assembly, "Corporate"];
        yield return [typeof(ParkingApp.Messaging.Domain.Entities.Conversation).Assembly, "Messaging"];

        // Notifications.Domain may have no public types; load the assembly by name/path.
        var notificationsDomain = ResolveNotificationsDomainAssembly();
        if (notificationsDomain is not null)
            yield return [notificationsDomain, "Notifications"];
    }

    [Theory]
    [MemberData(nameof(DomainAssemblies))]
    public void ModuleDomain_Must_Not_Depend_On_Host_Or_Foreign_Layers(
        System.Reflection.Assembly domainAssembly,
        string module)
    {
        domainAssembly.Should().NotBeNull(module);

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{module}.Domain isolation failed: {FormatFailures(result)}");
    }

    private static System.Reflection.Assembly? ResolveNotificationsDomainAssembly()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ParkingApp.Notifications.Domain");
        if (loaded is not null)
            return loaded;

        // Force load via Application project reference chain (test project refs Modules transitively through API/Infra)
        try
        {
            return System.Reflection.Assembly.Load("ParkingApp.Notifications.Domain");
        }
        catch
        {
            // Fall through to disk probe next to BuildingBlocks
        }

        var bb = typeof(ParkingApp.BuildingBlocks.Domain.BaseEntity).Assembly.Location;
        var dir = Path.GetDirectoryName(bb)!;
        var path = Path.Combine(dir, "ParkingApp.Notifications.Domain.dll");
        return File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
    }

    private static string FormatFailures(TestResult result)
    {
        if (result.FailingTypeNames is null || !result.FailingTypeNames.Any())
            return result.IsSuccessful ? "ok" : "unknown failure";
        return string.Join(", ", result.FailingTypeNames);
    }
}
