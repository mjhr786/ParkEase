using FluentAssertions;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class UnitOfWorkInterfacesTests
{
    [Fact]
    public void IUnitOfWork_ComposesAllContextPorts()
    {
        typeof(IUnitOfWork).Should().Implement<IMarketplaceUnitOfWork>();
        typeof(IUnitOfWork).Should().Implement<IIdentityUnitOfWork>();
        typeof(IUnitOfWork).Should().Implement<IMessagingUnitOfWork>();
        typeof(IUnitOfWork).Should().Implement<ICorporateUnitOfWork>();
        typeof(IUnitOfWork).Should().Implement<IDisposable>();
    }

    [Fact]
    public void ContextPorts_ExposeTransactionBoundary()
    {
        typeof(IMarketplaceUnitOfWork).Should().Implement<IUnitOfWorkTransaction>();
        typeof(IIdentityUnitOfWork).Should().Implement<IUnitOfWorkTransaction>();
        typeof(IMessagingUnitOfWork).Should().Implement<IUnitOfWorkTransaction>();
        typeof(ICorporateUnitOfWork).Should().Implement<IUnitOfWorkTransaction>();
    }

    [Fact]
    public void MarketplaceUnitOfWork_ExposesAggregateRootsOnly()
    {
        var names = typeof(IMarketplaceUnitOfWork).GetProperties().Select(p => p.Name).ToHashSet();
        names.Should().BeEquivalentTo(new[]
        {
            "ParkingSpaces", "Bookings", "ParkingPasses", "Payments", "Reviews", "Favorites"
        });
    }
}
