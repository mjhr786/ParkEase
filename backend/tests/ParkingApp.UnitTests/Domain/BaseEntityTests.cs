using FluentAssertions;
using Xunit;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Events;
using System.Linq;

namespace ParkingApp.UnitTests.Domain;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity { }
    private class TestDomainEvent : IDomainEvent { public DateTime OccurredOn { get; } = DateTime.UtcNow; }

    [Fact]
    public void AddDomainEvent_ShouldAddEventToList()
    {
        var entity = new TestEntity();
        var domainEvent = new TestDomainEvent();

        entity.AddDomainEvent(domainEvent);

        entity.DomainEvents.Should().ContainSingle();
        entity.DomainEvents.First().Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyList()
    {
        var entity = new TestEntity();
        entity.AddDomainEvent(new TestDomainEvent());
        
        entity.DomainEvents.Should().NotBeEmpty();

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }
}
