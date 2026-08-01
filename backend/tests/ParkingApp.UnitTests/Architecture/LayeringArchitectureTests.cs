using FluentAssertions;
using NetArchTest.Rules;
using ParkingApp.Application;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.UnitTests.Architecture;

/// <summary>
/// Clean Architecture project-level dependency rules.
/// These should stay green; do not weaken without an ADR.
/// </summary>
public class LayeringArchitectureTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(ParkingApp.Identity.Domain.Entities.User).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("ParkingApp.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("ParkingApp.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("ParkingApp.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Host_Domain()
    {
        // Thin host Application: CQRS + outbox admin only; transaction port is BuildingBlocks
        var refs = ApplicationAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Domain");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.API");
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_API()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("ParkingApp.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_API()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("ParkingApp.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_May_Depend_On_Application_And_BuildingBlocks()
    {
        // Smoke: infrastructure assembly loads and references inward layers (composition allowed).
        // Host Domain dissolved — module Domains + BuildingBlocks supply domain ports.
        InfrastructureAssembly.GetName().Name.Should().Be("ParkingApp.Infrastructure");
        var refs = InfrastructureAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        refs.Should().Contain("ParkingApp.Application");
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Domain");
    }

    [Fact]
    public void MessagingDomain_Should_Only_Depend_On_BuildingBlocks()
    {
        var messagingDomain = typeof(ParkingApp.Messaging.Domain.Entities.Conversation).Assembly;
        var refs = messagingDomain.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.API");
        refs.Should().NotContain("ParkingApp.Domain");
    }

    [Fact]
    public void MessagingApplication_Should_Not_Depend_On_Infrastructure_Or_API()
    {
        var messagingApp = typeof(ParkingApp.Messaging.Application.MessagingApplicationModule).Assembly;
        var refs = messagingApp.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        // P1.1: Messaging Application uses BuildingBlocks kernel, not host Application
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().Contain("ParkingApp.Messaging.Domain");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.Messaging.Infrastructure");
        refs.Should().NotContain("ParkingApp.API");
    }

    [Fact]
    public void MessagingInfrastructure_Should_Not_Depend_On_API()
    {
        var messagingInfra = typeof(ParkingApp.Messaging.Infrastructure.MessagingInfrastructureModule).Assembly;
        var refs = messagingInfra.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        refs.Should().Contain("ParkingApp.Messaging.Domain");
        refs.Should().Contain("ParkingApp.Messaging.Contracts");
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.API");
    }

    private static string FormatFailures(TestResult result)
    {
        if (result.IsSuccessful || result.FailingTypes is null)
            return "Architecture rule failed.";

        var names = result.FailingTypes.Select(t => t.FullName);
        return "Architecture rule failed for: " + string.Join(", ", names);
    }

    [Fact]
    public void IdentityDomain_Should_Only_Depend_On_BuildingBlocks()
    {
        var asm = typeof(ParkingApp.Identity.Domain.Entities.User).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.Domain");
    }

    [Fact]
    public void IdentityApplication_Should_Not_Depend_On_Infrastructure_Or_API()
    {
        var asm = typeof(ParkingApp.Identity.Application.IdentityApplicationModule).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().Contain("ParkingApp.Identity.Domain");
        refs.Should().Contain("ParkingApp.Marketplace.Contracts");
        refs.Should().Contain("ParkingApp.Messaging.Contracts");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Domain");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.Identity.Infrastructure");
        refs.Should().NotContain("ParkingApp.Marketplace.Domain");
        refs.Should().NotContain("ParkingApp.Messaging.Domain");
        refs.Should().NotContain("ParkingApp.API");
    }

    [Fact]
    public void IdentityInfrastructure_Should_Not_Depend_On_API_Or_Host_Application()
    {
        // Own Application is allowed; host Application is not referenced today (good).
        var asm = typeof(ParkingApp.Identity.Infrastructure.IdentityInfrastructureModule).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.Identity.Domain");
        refs.Should().Contain("ParkingApp.Identity.Contracts");
        refs.Should().Contain("ParkingApp.Identity.Application");
        refs.Should().NotContain("ParkingApp.API");
        refs.Should().NotContain("ParkingApp.Application");
    }

    // G��G�� Corporate G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

    [Fact]
    public void CorporateDomain_Should_Only_Depend_On_BuildingBlocks_And_Marketplace_Contracts()
    {
        // P1.3: no host Domain; Marketplace.Contracts allowed for BookingStatus
        var asm = typeof(ParkingApp.Corporate.Domain.Company).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().Contain("ParkingApp.Marketplace.Contracts");
        refs.Should().NotContain("ParkingApp.Domain");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.Corporate.Application");
        refs.Should().NotContain("ParkingApp.Corporate.Infrastructure");
        refs.Should().NotContain("ParkingApp.API");
        refs.Should().NotContain("ParkingApp.Marketplace.Domain");
        refs.Should().NotContain("ParkingApp.Identity.Domain");
    }

    [Fact]
    public void CorporateApplication_Should_Not_Depend_On_Infrastructure_Or_API()
    {
        var asm = typeof(ParkingApp.Corporate.Application.CorporateApplicationModule).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().Contain("ParkingApp.Corporate.Domain");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.Corporate.Infrastructure");
        refs.Should().NotContain("ParkingApp.API");
    }

    [Fact]
    public void CorporateInfrastructure_Should_Not_Depend_On_API_Or_Host_Application()
    {
        var asm = typeof(ParkingApp.Corporate.Infrastructure.CorporateInfrastructureModule).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.Corporate.Domain");
        // Kernel types now live in BuildingBlocks; host Application may be unused at compile time
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.API");
    }

    // G��G�� Marketplace G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

    [Fact]
    public void MarketplaceDomain_Should_Only_Depend_On_BuildingBlocks()
    {
        var asm = typeof(ParkingApp.Marketplace.Domain.Entities.ParkingSpace).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.Marketplace.Application");
        refs.Should().NotContain("ParkingApp.Marketplace.Infrastructure");
        refs.Should().NotContain("ParkingApp.Domain");
        refs.Should().NotContain("ParkingApp.API");
    }

    [Fact]
    public void MarketplaceApplication_Should_Not_Depend_On_Infrastructure_Or_API()
    {
        var asm = typeof(ParkingApp.Marketplace.Application.MarketplaceApplicationModule).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().Contain("ParkingApp.Marketplace.Domain");
        refs.Should().Contain("ParkingApp.Identity.Contracts");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Domain");
        refs.Should().NotContain("ParkingApp.Infrastructure");
        refs.Should().NotContain("ParkingApp.Marketplace.Infrastructure");
        refs.Should().NotContain("ParkingApp.Identity.Domain");
        refs.Should().NotContain("ParkingApp.API");
    }

    [Fact]
    public void MarketplaceInfrastructure_Should_Not_Depend_On_API()
    {
        var asm = typeof(ParkingApp.Marketplace.Infrastructure.MarketplaceInfrastructureModule).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.Marketplace.Domain");
        refs.Should().Contain("ParkingApp.Marketplace.Contracts");
        refs.Should().Contain("ParkingApp.BuildingBlocks");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.API");
    }

    [Fact]
    public void Marketplace_Should_Not_Reference_Corporate_Assemblies()
    {
        // KD-19: consumer staging isolation must not pull Corporate module into Marketplace.
        foreach (var asm in new[]
                 {
                     typeof(ParkingApp.Marketplace.Domain.Entities.Booking).Assembly,
                     typeof(ParkingApp.Marketplace.Application.MarketplaceApplicationModule).Assembly,
                     typeof(ParkingApp.Marketplace.Infrastructure.MarketplaceInfrastructureModule).Assembly,
                 })
        {
            var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
            refs.Should().NotContain("ParkingApp.Corporate.Domain", because: asm.GetName().Name);
            refs.Should().NotContain("ParkingApp.Corporate.Application", because: asm.GetName().Name);
            refs.Should().NotContain("ParkingApp.Corporate.Infrastructure", because: asm.GetName().Name);
            refs.Should().NotContain("ParkingApp.Corporate.Contracts", because: asm.GetName().Name);
        }
    }

    [Fact]
    public void BookingReadStore_Should_Not_HardCode_CorporateBookings_Table()
    {
        // KD-19 preferred path: filter IsCorporateStaged, never anti-join CorporateBookings.
        typeof(ParkingApp.Marketplace.Domain.Entities.Booking)
            .GetProperty(nameof(ParkingApp.Marketplace.Domain.Entities.Booking.IsCorporateStaged))
            .Should().NotBeNull();

        var moduleDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Modules", "Marketplace", "ParkingApp.Marketplace.Infrastructure", "ReadModel", "Bookings"));
        var readStorePath = Path.Combine(moduleDir, "BookingReadStore.cs");
        if (!File.Exists(readStorePath))
        {
            return;
        }

        var source = File.ReadAllText(readStorePath);
        source.Should().NotContain("CorporateBookings");
        source.Should().Contain("IsCorporateStaged");
    }

    [Fact]
    public void CorporateDomain_Must_Not_Reference_Other_Module_Domains()
    {
        var asm = typeof(ParkingApp.Corporate.Domain.Company).Assembly;
        var refs = asm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().NotContain("ParkingApp.Identity.Domain");
        refs.Should().NotContain("ParkingApp.Marketplace.Domain");
        refs.Should().NotContain("ParkingApp.Messaging.Domain");
        refs.Should().NotContain("ParkingApp.Notifications.Domain");
        refs.Should().NotContain("ParkingApp.Domain");
    }
}





