using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class ValueObjectOnAggregatesTests
{
    [Fact]
    public void User_Register_RejectsInvalidEmail()
    {
        var act = () => User.Register("not-an-email", "hash", "A", "B", "1");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void User_Register_StoresEmailValueObject()
    {
        var user = User.Register("Ada@Example.COM", "hash", "Ada", "Lovelace", "1");
        user.Email.Should().BeOfType<Email>();
        user.Email.Value.Should().Be("ada@example.com");
        ((string)user.Email).Should().Be("ada@example.com");
    }

    [Fact]
    public void Payment_CreatePending_UsesMoneyCharge()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 250.5m, PaymentMethod.UPI, "inr");
        payment.Charge.Should().Be(new Money(250.5m, "INR"));
        payment.Amount.Should().Be(250.5m);
        payment.Currency.Should().Be("INR");
    }

    [Fact]
    public void Payment_CreatePending_RejectsNegativeMoney()
    {
        var act = () => Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), -1, PaymentMethod.CreditCard);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ParkingSpace_Create_RejectsInvalidAddress()
    {
        var act = () => ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "D", "", "City", "ST", "IN", "1",
            0, 0, ParkingType.Open, 1, 1, 1, 1, 1);
        act.Should().Throw<ValidationException>();
    }
}
