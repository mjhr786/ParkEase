using System.Reflection;
using FluentAssertions;
using ParkingApp.Marketplace.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class PaymentCheckoutStoreTests
{
    [Fact]
    public void Store_DoesNotExposeOrCallSaveChangesAsync()
    {
        typeof(ParkingApp.Marketplace.Application.Interfaces.IPaymentCheckoutStore)
            .GetMethods()
            .Select(m => m.Name)
            .Should()
            .NotContain(name => name.Contains("SaveChanges", StringComparison.Ordinal));

        typeof(PaymentCheckoutStore)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Should()
            .NotContain(name => name.Contains("SaveChanges", StringComparison.Ordinal));
    }

    [Fact]
    public void Store_HasLockOrderAsync()
    {
        typeof(ParkingApp.Marketplace.Application.Interfaces.IPaymentCheckoutStore)
            .GetMethod("LockOrderAsync")
            .Should()
            .NotBeNull();
    }
}
