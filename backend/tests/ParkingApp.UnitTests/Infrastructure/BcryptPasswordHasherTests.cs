using FluentAssertions;
using ParkingApp.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_Then_Verify_Succeeds_ForSamePassword()
    {
        var hash = _hasher.Hash("Secret123!");
        _hasher.Verify("Secret123!", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_Fails_ForWrongPassword()
    {
        var hash = _hasher.Hash("Secret123!");
        _hasher.Verify("WrongPassword", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_Throws_ForEmptyPassword()
    {
        var act = () => _hasher.Hash("");
        act.Should().Throw<ArgumentException>();
    }
}
