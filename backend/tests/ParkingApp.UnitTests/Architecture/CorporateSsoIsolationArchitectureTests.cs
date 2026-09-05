using System.Reflection;
using FluentAssertions;
using ParkingApp.Identity.Application.Commands.Auth;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.UnitTests.Architecture;

/// <summary>
/// PR9 isolation guards: Corporate SSO must never share marketplace social paths or entities.
/// </summary>
public class CorporateSsoIsolationArchitectureTests
{
    private static readonly Assembly IdentityApplication =
        typeof(CorporateSsoStartCommand).Assembly;

    private static readonly Assembly IdentityDomain =
        typeof(User).Assembly;

    [Fact]
    public void ExternalLoginHandler_Source_Mints_Marketplace_Only()
    {
        var source = ReadApplicationSource("Commands/Auth/ExternalLoginCommand.cs");
        source.Should().Contain("ProductChannel.Marketplace");
        source.Should().NotContain("ProductChannel.Corporate");
        source.Should().NotContain("CorporateSsoIdentityLink");
        source.Should().NotContain("RegisterFromCorporateSso");
    }

    [Fact]
    public void CorporateSsoHandlers_Source_Does_Not_Use_Marketplace_ExternalLogin()
    {
        var source = ReadApplicationSource("Commands/Auth/CorporateSsoCommands.cs");
        source.Should().NotContain("UserExternalLogin");
        source.Should().NotContain("RegisterFromExternal");
        source.Should().NotContain("ProductChannel.Marketplace");
        source.Should().NotContain("/api/auth/external");
        source.Should().Contain("RegisterFromCorporateSso");
        source.Should().Contain("forbidBootstrap: true");
        source.Should().Contain("CorporateSsoIdentityLink");
    }

    [Fact]
    public void CorporateSsoIdentityLink_Entity_Is_Separate_From_UserExternalLogin()
    {
        typeof(CorporateSsoIdentityLink).FullName
            .Should().NotBe(typeof(UserExternalLogin).FullName);

        typeof(CorporateSsoIdentityLink).Assembly
            .Should().BeSameAs(typeof(UserExternalLogin).Assembly);

        // Distinct tables (EF config lives in Infrastructure; names are on types for clarity).
        typeof(CorporateSsoIdentityLink).Name.Should().Be("CorporateSsoIdentityLink");
        typeof(UserExternalLogin).Name.Should().Be("UserExternalLogin");
    }

    [Fact]
    public void CorporateSso_Uses_Dedicated_Protocol_Enum()
    {
        Enum.GetNames(typeof(SsoProtocol)).Should().Contain("Oidc");
        // Marketplace social uses ExternalAuthProvider — different type.
        typeof(SsoProtocol).Should().NotBe(typeof(ExternalAuthProvider));
    }

    [Fact]
    public void Identity_Application_Does_Not_Reference_Corporate_Domain()
    {
        var refs = IdentityApplication.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        refs.Should().Contain("ParkingApp.Corporate.Contracts");
        refs.Should().NotContain("ParkingApp.Corporate.Domain");
        refs.Should().NotContain("ParkingApp.Corporate.Application");
        refs.Should().NotContain("ParkingApp.Corporate.Infrastructure");
    }

    private static string ReadApplicationSource(string relativePathUnderApplication)
    {
        // tests/ParkingApp.UnitTests → ../../src/Modules/Identity/ParkingApp.Identity.Application/...
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(
                baseDir,
                "..", "..", "..", "..",
                "src", "Modules", "Identity", "ParkingApp.Identity.Application",
                relativePathUnderApplication)),
            Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "src", "Modules", "Identity", "ParkingApp.Identity.Application",
                relativePathUnderApplication)),
            Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..",
                "src", "Modules", "Identity", "ParkingApp.Identity.Application",
                relativePathUnderApplication)),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        // Fallback: locate by walking up from baseDir
        var dir = new DirectoryInfo(baseDir);
        while (dir is not null)
        {
            var probe = Path.Combine(
                dir.FullName,
                "src", "Modules", "Identity", "ParkingApp.Identity.Application",
                relativePathUnderApplication);
            if (File.Exists(probe))
                return File.ReadAllText(probe);

            // monorepo root may be ParkEase/
            probe = Path.Combine(
                dir.FullName,
                "backend", "src", "Modules", "Identity", "ParkingApp.Identity.Application",
                relativePathUnderApplication);
            if (File.Exists(probe))
                return File.ReadAllText(probe);

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate source file for isolation test: {relativePathUnderApplication}");
    }
}
