using FluentAssertions;
using Xunit;
using ParkingApp.BuildingBlocks.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ParkingApp.UnitTests;

public class ExtensionTests
{
    [Fact]
    public void StringExtensions_Truncate_ShouldWorkCorrectly()
    {
        "Hello World".Truncate(8).Should().Be("Hello...");
        "Short".Truncate(10).Should().Be("Short");
    }

    [Fact]
    public void DateTimeExtensions_RelativeTime_ShouldReturnCorrectStrings()
    {
        var now = DateTime.UtcNow;
        now.AddMinutes(-5).ToRelativeTime().Should().Be("5m ago");
        now.AddHours(-2).ToRelativeTime().Should().Be("2h ago");
        now.AddDays(-3).ToRelativeTime().Should().Be("3d ago");
    }

    [Fact]
    public void CollectionExtensions_Batch_ShouldSplitCorrectly()
    {
        var list = Enumerable.Range(1, 10).ToList();
        var batches = list.Batch(3).ToList();

        batches.Should().HaveCount(4);
        batches[0].Should().HaveCount(3).And.ContainInOrder(1, 2, 3);
        batches[3].Should().HaveCount(1).And.Contain(10);
    }
}
