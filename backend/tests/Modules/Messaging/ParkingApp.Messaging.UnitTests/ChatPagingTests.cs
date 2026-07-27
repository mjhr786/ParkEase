using FluentAssertions;
using ParkingApp.Messaging.Application;

namespace ParkingApp.Messaging.UnitTests;

public class ChatPagingTests
{
    [Theory]
    [InlineData(1, 20, 1, 20)]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-3, 10, 1, 10)]
    [InlineData(2, 0, 2, 20)]
    [InlineData(1, 100, 1, 50)]
    [InlineData(1, 50, 1, 50)]
    public void ClampConversations_AppliesBounds(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var (p, s) = ChatPaging.ClampConversations(page, pageSize);
        p.Should().Be(expectedPage);
        s.Should().Be(expectedSize);
    }

    [Theory]
    [InlineData(1, 50, 1, 50)]
    [InlineData(0, 50, 1, 50)]
    [InlineData(1, 0, 1, 50)]
    [InlineData(3, 200, 3, 100)]
    [InlineData(1, 100, 1, 100)]
    public void ClampMessages_AppliesBounds(int page, int pageSize, int expectedPage, int expectedSize)
    {
        var (p, s) = ChatPaging.ClampMessages(page, pageSize);
        p.Should().Be(expectedPage);
        s.Should().Be(expectedSize);
    }
}
