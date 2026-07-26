using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class LprRegistryDomainTests
{
    [Fact]
    public void LprCameraKey_Create_HashesSecretAndReturnsPlaintextOnce()
    {
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var (key, secret) = LprCameraKey.Create(spaceId, "North gate", userId);

        secret.Should().StartWith("pk_");
        key.SecretHash.Should().Be(LprCameraKey.HashSecret(secret));
        key.SecretPrefix.Should().Be(secret[..8]);
        key.IsEnabled.Should().BeTrue();
        key.ParkingSpaceId.Should().Be(spaceId);
        LprCameraKey.SecretsMatch(secret, key.SecretHash).Should().BeTrue();
        LprCameraKey.SecretsMatch("wrong", key.SecretHash).Should().BeFalse();
    }

    [Fact]
    public void LprPlateRule_Create_NormalizesPlate()
    {
        var rule = LprPlateRule.Create(
            Guid.NewGuid(),
            " ka 01 xx 99 ",
            LprPlateRuleType.Deny,
            Guid.NewGuid(),
            "blocked");

        rule.LicensePlateNormalized.Should().Be("KA01XX99");
        rule.RuleType.Should().Be(LprPlateRuleType.Deny);
        rule.Note.Should().Be("blocked");
    }

    [Fact]
    public void LprPlateRule_Create_EmptyPlate_Throws()
    {
        var act = () => LprPlateRule.Create(Guid.NewGuid(), "  ", LprPlateRuleType.Allow, Guid.NewGuid());
        act.Should().Throw<ValidationException>();
    }
}
