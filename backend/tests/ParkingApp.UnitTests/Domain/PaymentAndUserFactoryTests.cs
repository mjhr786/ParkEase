using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class PaymentAndUserFactoryTests
{
    [Fact]
    public void Payment_CreatePending_Then_MarkSucceeded()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 500, PaymentMethod.CreditCard);

        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.MarkSucceeded("txn_1", "order_1", "Razorpay", amount: 500);

        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.PaidAt.Should().NotBeNull();
        payment.InvoiceNumber.Should().NotBeNullOrWhiteSpace();
        payment.TransactionId.Should().Be("txn_1");
    }

    [Fact]
    public void Payment_RecordRefund_PartialAndFull()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 100, PaymentMethod.CreditCard);
        payment.MarkSucceeded("t", "o", "Mock");

        payment.RecordRefund(40, "partial", "ref_1");
        payment.Status.Should().Be(PaymentStatus.PartialRefund);
        payment.RefundAmount.Should().Be(40);

        payment.RecordRefund(60, "rest", "ref_2");
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundAmount.Should().Be(100);
    }

    [Fact]
    public void Payment_RecordRefund_WhenPending_Throws()
    {
        var payment = Payment.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 100, PaymentMethod.CreditCard);
        var act = () => payment.RecordRefund(10, "x", "r");
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void User_Register_And_AuthLifecycle()
    {
        var user = User.Register("Test@Example.com", "hash", "Ada", "Lovelace", "999");

        user.Email.Value.Should().Be("test@example.com");
        user.Role.Should().Be(UserRole.User);
        user.IsActive.Should().BeTrue();

        user.RecordLogin("refresh-1", DateTime.UtcNow.AddDays(7));
        user.RefreshToken.Should().Be("refresh-1");
        user.LastLoginAt.Should().NotBeNull();

        user.ChangePassword("new-hash");
        user.PasswordHash.Should().Be("new-hash");
        user.RefreshToken.Should().BeNull();
    }

    [Fact]
    public void User_UpdateProfile_Partial()
    {
        var user = User.Register("a@b.com", "h", "A", "B", "1");
        user.UpdateProfile("Ann", null, "555");

        user.FirstName.Should().Be("Ann");
        user.LastName.Should().Be("B");
        user.PhoneNumber.Should().Be("555");
    }

    [Fact]
    public void User_RecordLogin_WhenInactive_Throws()
    {
        var user = User.Register("a@b.com", "h", "A", "B", "1");
        user.Deactivate();

        var act = () => user.RecordLogin("r", DateTime.UtcNow.AddDays(1));
        act.Should().Throw<BusinessRuleException>();
    }
}
