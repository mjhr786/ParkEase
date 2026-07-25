using System.Xml.Linq;
using FluentAssertions;

namespace ParkingApp.UnitTests.Architecture;

/// <summary>
/// Freezes the modular <c>ProjectReference</c> graph from .csproj files (not assembly metadata,
/// which includes compile-time transitive type refs and is too noisy).
/// New ProjectReferences that are not allowlisted fail — update the boundary audit intentionally.
/// Runtime behavior is unchanged.
/// </summary>
public class ModuleProjectReferenceRules
{
    private static readonly string SrcRoot = FindSrcRoot();

    /// <summary>
    /// Allowed ProjectReference target project names (filename without .csproj) per module project name.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> AllowedProjectReferences =
        new(StringComparer.Ordinal)
        {
            ["ParkingApp.Identity.Domain"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
            },
            ["ParkingApp.Identity.Contracts"] = new(StringComparer.Ordinal),
            ["ParkingApp.Identity.Application"] = new(StringComparer.Ordinal)
            {
                // Detached from host Domain — DeleteUser cascade via Contracts
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Identity.Domain",
                "ParkingApp.Identity.Contracts",
                "ParkingApp.Marketplace.Contracts",
                "ParkingApp.Messaging.Contracts",
            },
            ["ParkingApp.Identity.Infrastructure"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Identity.Domain",
                "ParkingApp.Identity.Contracts",
                "ParkingApp.Identity.Application",
            },

            ["ParkingApp.Marketplace.Domain"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Marketplace.Contracts",
            },
            ["ParkingApp.Marketplace.Contracts"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
            },
            ["ParkingApp.Marketplace.Application"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Marketplace.Domain",
                "ParkingApp.Marketplace.Contracts",
                "ParkingApp.Identity.Contracts",
                "ParkingApp.Messaging.Contracts",
            },
            ["ParkingApp.Marketplace.Infrastructure"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Marketplace.Domain",
                "ParkingApp.Marketplace.Contracts",
                "ParkingApp.Marketplace.Application",
            },

            ["ParkingApp.Corporate.Domain"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Marketplace.Contracts", // BookingStatus only (allowed Contracts)
            },
            ["ParkingApp.Corporate.Contracts"] = new(StringComparer.Ordinal),
            ["ParkingApp.Corporate.Application"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Corporate.Domain",
                "ParkingApp.Corporate.Contracts",
                "ParkingApp.Marketplace.Contracts",
                "ParkingApp.Identity.Contracts",
            },
            ["ParkingApp.Corporate.Infrastructure"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Corporate.Domain",
                "ParkingApp.Corporate.Contracts",
                "ParkingApp.Corporate.Application",
            },

            ["ParkingApp.Messaging.Domain"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Messaging.Contracts",
            },
            ["ParkingApp.Messaging.Contracts"] = new(StringComparer.Ordinal),
            ["ParkingApp.Messaging.Application"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Messaging.Domain",
                "ParkingApp.Messaging.Contracts",
                "ParkingApp.Identity.Contracts",
                "ParkingApp.Marketplace.Contracts",
                "ParkingApp.Notifications.Contracts",
            },
            ["ParkingApp.Messaging.Infrastructure"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Messaging.Domain",
                "ParkingApp.Messaging.Contracts",
                "ParkingApp.Messaging.Application",
            },

            ["ParkingApp.Notifications.Domain"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
            },
            ["ParkingApp.Notifications.Contracts"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
            },
            ["ParkingApp.Notifications.Application"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Notifications.Domain",
                "ParkingApp.Notifications.Contracts",
                "ParkingApp.Identity.Contracts",
                "ParkingApp.Marketplace.Contracts",
                "ParkingApp.Messaging.Contracts",
            },
            ["ParkingApp.Notifications.Infrastructure"] = new(StringComparer.Ordinal)
            {
                "ParkingApp.BuildingBlocks",
                "ParkingApp.Notifications.Application",
                "ParkingApp.Identity.Contracts",
            },
        };

    private static readonly (string RelativePath, string ProjectName)[] ModuleProjects =
    {
        ("Modules/Identity/ParkingApp.Identity.Domain/ParkingApp.Identity.Domain.csproj", "ParkingApp.Identity.Domain"),
        ("Modules/Identity/ParkingApp.Identity.Contracts/ParkingApp.Identity.Contracts.csproj", "ParkingApp.Identity.Contracts"),
        ("Modules/Identity/ParkingApp.Identity.Application/ParkingApp.Identity.Application.csproj", "ParkingApp.Identity.Application"),
        ("Modules/Identity/ParkingApp.Identity.Infrastructure/ParkingApp.Identity.Infrastructure.csproj", "ParkingApp.Identity.Infrastructure"),

        ("Modules/Marketplace/ParkingApp.Marketplace.Domain/ParkingApp.Marketplace.Domain.csproj", "ParkingApp.Marketplace.Domain"),
        ("Modules/Marketplace/ParkingApp.Marketplace.Contracts/ParkingApp.Marketplace.Contracts.csproj", "ParkingApp.Marketplace.Contracts"),
        ("Modules/Marketplace/ParkingApp.Marketplace.Application/ParkingApp.Marketplace.Application.csproj", "ParkingApp.Marketplace.Application"),
        ("Modules/Marketplace/ParkingApp.Marketplace.Infrastructure/ParkingApp.Marketplace.Infrastructure.csproj", "ParkingApp.Marketplace.Infrastructure"),

        ("Modules/Corporate/ParkingApp.Corporate.Domain/ParkingApp.Corporate.Domain.csproj", "ParkingApp.Corporate.Domain"),
        ("Modules/Corporate/ParkingApp.Corporate.Contracts/ParkingApp.Corporate.Contracts.csproj", "ParkingApp.Corporate.Contracts"),
        ("Modules/Corporate/ParkingApp.Corporate.Application/ParkingApp.Corporate.Application.csproj", "ParkingApp.Corporate.Application"),
        ("Modules/Corporate/ParkingApp.Corporate.Infrastructure/ParkingApp.Corporate.Infrastructure.csproj", "ParkingApp.Corporate.Infrastructure"),

        ("Modules/Messaging/ParkingApp.Messaging.Domain/ParkingApp.Messaging.Domain.csproj", "ParkingApp.Messaging.Domain"),
        ("Modules/Messaging/ParkingApp.Messaging.Contracts/ParkingApp.Messaging.Contracts.csproj", "ParkingApp.Messaging.Contracts"),
        ("Modules/Messaging/ParkingApp.Messaging.Application/ParkingApp.Messaging.Application.csproj", "ParkingApp.Messaging.Application"),
        ("Modules/Messaging/ParkingApp.Messaging.Infrastructure/ParkingApp.Messaging.Infrastructure.csproj", "ParkingApp.Messaging.Infrastructure"),

        ("Modules/Notifications/ParkingApp.Notifications.Domain/ParkingApp.Notifications.Domain.csproj", "ParkingApp.Notifications.Domain"),
        ("Modules/Notifications/ParkingApp.Notifications.Contracts/ParkingApp.Notifications.Contracts.csproj", "ParkingApp.Notifications.Contracts"),
        ("Modules/Notifications/ParkingApp.Notifications.Application/ParkingApp.Notifications.Application.csproj", "ParkingApp.Notifications.Application"),
        ("Modules/Notifications/ParkingApp.Notifications.Infrastructure/ParkingApp.Notifications.Infrastructure.csproj", "ParkingApp.Notifications.Infrastructure"),
    };

    public static IEnumerable<object[]> ModuleProjectData() =>
        ModuleProjects.Select(p => new object[] { p.RelativePath, p.ProjectName });

    [Theory]
    [MemberData(nameof(ModuleProjectData))]
    public void Module_Csproj_ProjectReferences_Must_Be_Allowlisted(string relativeCsproj, string projectName)
    {
        var path = Path.Combine(SrcRoot, relativeCsproj.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"expected csproj at {path}");

        AllowedProjectReferences.Should().ContainKey(projectName);
        var allowed = AllowedProjectReferences[projectName];

        var refs = ReadProjectReferenceNames(path);
        var unexpected = refs.Where(r => !allowed.Contains(r)).Distinct().OrderBy(r => r).ToList();

        unexpected.Should().BeEmpty(
            $"{projectName} has new ProjectReference(s) not in the modular allowlist: {string.Join(", ", unexpected)}. "
            + "Update docs/modular-monolith-boundary-audit.md and ModuleProjectReferenceRules if intentional.");
    }

    [Theory]
    [MemberData(nameof(ModuleProjectData))]
    public void Module_Domain_Csproj_Must_Not_Reference_Forbidden_Projects(string relativeCsproj, string projectName)
    {
        if (!projectName.EndsWith(".Domain", StringComparison.Ordinal))
            return;

        var path = Path.Combine(SrcRoot, relativeCsproj.Replace('/', Path.DirectorySeparatorChar));
        var refs = ReadProjectReferenceNames(path);

        var forbidden = refs.Where(r =>
            r is "ParkingApp.Application" or "ParkingApp.Infrastructure" or "ParkingApp.API"
            || r.EndsWith(".Application", StringComparison.Ordinal)
            || r.EndsWith(".Infrastructure", StringComparison.Ordinal)).ToList();

        forbidden.Should().BeEmpty($"{projectName} Domain must not reference: {string.Join(", ", forbidden)}");

        var otherModuleDomains = refs
            .Where(r => r.EndsWith(".Domain", StringComparison.Ordinal)
                        && r.StartsWith("ParkingApp.", StringComparison.Ordinal)
                        && r is not "ParkingApp.Domain"
                        && r != projectName)
            .ToList();
        otherModuleDomains.Should().BeEmpty(
            $"{projectName} must not ProjectReference other module Domains: {string.Join(", ", otherModuleDomains)}");
    }

    /// <summary>
    /// Known transitional Application → other module Domain edges (event handlers until integration contracts land).
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> AllowedForeignDomainsForApplication =
        new(StringComparer.Ordinal)
        {
            // No Application → foreign Domain edges remain for Notifications (uses INotificationInbox contract).
        };

    [Theory]
    [MemberData(nameof(ModuleProjectData))]
    public void Module_Application_Csproj_Must_Not_Reference_Foreign_Infra_Or_Domain(string relativeCsproj, string projectName)
    {
        if (!projectName.EndsWith(".Application", StringComparison.Ordinal))
            return;

        var modulePrefix = projectName.Replace(".Application", "", StringComparison.Ordinal);
        var path = Path.Combine(SrcRoot, relativeCsproj.Replace('/', Path.DirectorySeparatorChar));
        var refs = ReadProjectReferenceNames(path);

        refs.Should().NotContain("ParkingApp.API");
        refs.Should().NotContain("ParkingApp.Infrastructure");

        var foreignInfra = refs
            .Where(r => r.EndsWith(".Infrastructure", StringComparison.Ordinal)
                        && r != $"{modulePrefix}.Infrastructure")
            .ToList();
        foreignInfra.Should().BeEmpty(
            $"{projectName} must not ProjectReference foreign Infrastructure: {string.Join(", ", foreignInfra)}");

        AllowedForeignDomainsForApplication.TryGetValue(projectName, out var allowedForeign);
        allowedForeign ??= new HashSet<string>(StringComparer.Ordinal);

        var foreignDomain = refs
            .Where(r => r.EndsWith(".Domain", StringComparison.Ordinal)
                        && r != $"{modulePrefix}.Domain"
                        && r is not "ParkingApp.Domain"
                        && !allowedForeign.Contains(r))
            .ToList();
        foreignDomain.Should().BeEmpty(
            $"{projectName} must not ProjectReference other module Domain (use Contracts): {string.Join(", ", foreignDomain)}");
    }

    [Fact]
    public void Host_Application_Csproj_Must_Not_Reference_Module_Infrastructure()
    {
        var path = Path.Combine(SrcRoot, "ParkingApp.Application", "ParkingApp.Application.csproj");
        File.Exists(path).Should().BeTrue();
        var refs = ReadProjectReferenceNames(path);
        var moduleInfra = refs
            .Where(r => r.EndsWith(".Infrastructure", StringComparison.Ordinal)
                        && r != "ParkingApp.Infrastructure")
            .ToList();
        moduleInfra.Should().BeEmpty(
            "Host Application must not ProjectReference module Infrastructure: " + string.Join(", ", moduleInfra));
    }

    [Fact]
    public void Host_Application_Csproj_Must_Only_Reference_BuildingBlocks()
    {
        var path = Path.Combine(SrcRoot, "ParkingApp.Application", "ParkingApp.Application.csproj");
        File.Exists(path).Should().BeTrue();
        var refs = ReadProjectReferenceNames(path);
        refs.Should().BeEquivalentTo(new[] { "ParkingApp.BuildingBlocks" });
        refs.Should().NotContain("ParkingApp.Domain");
    }

    [Theory]
    [MemberData(nameof(ModuleProjectData))]
    public void Module_Application_Csproj_Must_Not_Reference_Host_Application(string relativeCsproj, string projectName)
    {
        if (!projectName.EndsWith(".Application", StringComparison.Ordinal))
            return;

        var path = Path.Combine(SrcRoot, relativeCsproj.Replace('/', Path.DirectorySeparatorChar));
        var refs = ReadProjectReferenceNames(path);
        refs.Should().NotContain(
            "ParkingApp.Application",
            $"{projectName} must use BuildingBlocks kernel, not host Application");
    }

    [Theory]
    [MemberData(nameof(ModuleProjectData))]
    public void Module_Infrastructure_Csproj_Must_Not_Reference_Foreign_Domain_Or_Infra(
        string relativeCsproj,
        string projectName)
    {
        if (!projectName.EndsWith(".Infrastructure", StringComparison.Ordinal))
            return;

        var modulePrefix = projectName.Replace(".Infrastructure", "", StringComparison.Ordinal);
        var path = Path.Combine(SrcRoot, relativeCsproj.Replace('/', Path.DirectorySeparatorChar));
        var refs = ReadProjectReferenceNames(path);

        refs.Should().NotContain("ParkingApp.API");
        refs.Should().NotContain("ParkingApp.Application");
        refs.Should().NotContain("ParkingApp.Infrastructure");

        var foreignDomain = refs
            .Where(r => r.EndsWith(".Domain", StringComparison.Ordinal)
                        && r != $"{modulePrefix}.Domain"
                        && r is not "ParkingApp.Domain")
            .ToList();
        foreignDomain.Should().BeEmpty(
            $"{projectName} must not ProjectReference foreign Domain: {string.Join(", ", foreignDomain)}");

        var foreignInfra = refs
            .Where(r => r.EndsWith(".Infrastructure", StringComparison.Ordinal)
                        && r != projectName
                        && r is not "ParkingApp.Infrastructure")
            .ToList();
        foreignInfra.Should().BeEmpty(
            $"{projectName} must not ProjectReference foreign Infrastructure: {string.Join(", ", foreignInfra)}");
    }

    [Fact]
    public void Host_Infrastructure_Csproj_Must_Not_Reference_Host_Domain()
    {
        // Host Domain project dissolved (2026-07-20); composite UoW lives in Infrastructure.Persistence
        var path = Path.Combine(SrcRoot, "ParkingApp.Infrastructure", "ParkingApp.Infrastructure.csproj");
        File.Exists(path).Should().BeTrue();
        var refs = ReadProjectReferenceNames(path);
        refs.Should().NotContain("ParkingApp.Domain");
        refs.Should().NotContain("ParkingApp.API");
        File.Exists(Path.Combine(SrcRoot, "ParkingApp.Domain", "ParkingApp.Domain.csproj"))
            .Should().BeFalse("host ParkingApp.Domain project should be removed");
    }

    [Fact]
    public void Host_Infrastructure_Must_Not_Reference_Notifications_Application()
    {
        // Notifications composition is API → Notifications.Infrastructure only
        var path = Path.Combine(SrcRoot, "ParkingApp.Infrastructure", "ParkingApp.Infrastructure.csproj");
        var refs = ReadProjectReferenceNames(path);
        refs.Should().NotContain("ParkingApp.Notifications.Application");
        refs.Should().NotContain("ParkingApp.Notifications.Infrastructure");
    }

    private static HashSet<string> ReadProjectReferenceNames(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => Path.GetFileNameWithoutExtension(i!.Replace('\\', '/')))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindSrcRoot()
    {
        // tests/ParkingApp.UnitTests/bin/Debug/net9.0 → walk up to backend/src
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src");
            if (Directory.Exists(Path.Combine(candidate, "ParkingApp.Application")))
                return candidate;

            var backendSrc = Path.Combine(dir.FullName, "backend", "src");
            if (Directory.Exists(Path.Combine(backendSrc, "ParkingApp.Application")))
                return backendSrc;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate backend/src from " + AppContext.BaseDirectory);
    }
}
