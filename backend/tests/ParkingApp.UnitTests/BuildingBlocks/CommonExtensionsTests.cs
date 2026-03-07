using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ParkingApp.BuildingBlocks.Extensions;
using Xunit;

namespace ParkingApp.UnitTests.BuildingBlocks;

public class CommonExtensionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void StringExtensions_IsNullOrEmpty_ShouldReturnTrue_WhenNullOrEmpty(string? val)
    {
        val.IsNullOrEmpty().Should().BeTrue();
    }

    [Fact]
    public void StringExtensions_IsNullOrEmpty_ShouldReturnFalse_WhenNotEmpty()
    {
        "test".IsNullOrEmpty().Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void StringExtensions_IsNullOrWhiteSpace_ShouldReturnTrue_WhenNullOrWhiteSpace(string? val)
    {
        val.IsNullOrWhiteSpace().Should().BeTrue();
    }

    [Fact]
    public void StringExtensions_IsNullOrWhiteSpace_ShouldReturnFalse_WhenNotWhiteSpace()
    {
        "test".IsNullOrWhiteSpace().Should().BeFalse();
    }

    [Fact]
    public void StringExtensions_ToNullIfEmpty_ShouldReturnNull_WhenEmpty()
    {
        "".ToNullIfEmpty().Should().BeNull();
        ((string?)null).ToNullIfEmpty().Should().BeNull();
    }

    [Fact]
    public void StringExtensions_ToNullIfEmpty_ShouldReturnValue_WhenNotEmpty()
    {
        "test".ToNullIfEmpty().Should().Be("test");
    }

    [Fact]
    public void StringExtensions_ToNullIfWhiteSpace_ShouldReturnNull_WhenWhiteSpace()
    {
        " ".ToNullIfWhiteSpace().Should().BeNull();
        "".ToNullIfWhiteSpace().Should().BeNull();
        ((string?)null).ToNullIfWhiteSpace().Should().BeNull();
    }

    [Fact]
    public void StringExtensions_ToNullIfWhiteSpace_ShouldReturnValue_WhenNotWhiteSpace()
    {
        "test".ToNullIfWhiteSpace().Should().Be("test");
    }

    [Fact]
    public void StringExtensions_Truncate_ShouldTruncateCorrectly()
    {
        "1234567890".Truncate(5).Should().Be("12...");
        "12".Truncate(5).Should().Be("12");
        "".Truncate(5).Should().Be("");
        ((string?)null)!.Truncate(5).Should().BeNull();
    }

    [Fact]
    public void StringExtensions_ToTitleCase_ShouldReturnCorrectly()
    {
        "hello world".ToTitleCase().Should().Be("Hello World");
        "HELLO WORLD".ToTitleCase().Should().Be("Hello World");
        "".ToTitleCase().Should().Be("");
        ((string?)null)!.ToTitleCase().Should().BeNull();
    }

    [Fact]
    public void StringExtensions_RemoveWhitespace_ShouldReturnCorrectly()
    {
        "  he l lo  ".RemoveWhitespace().Should().Be("hello");
    }

    [Fact]
    public void DateTimeExtensions_StartOfDay_ShouldReturnMidnight()
    {
        var dt = new DateTime(2023, 1, 1, 15, 30, 0);
        dt.StartOfDay().Should().Be(new DateTime(2023, 1, 1, 0, 0, 0));
    }

    [Fact]
    public void DateTimeExtensions_EndOfDay_ShouldReturnEndOfDay()
    {
        var dt = new DateTime(2023, 1, 1, 15, 30, 0);
        dt.EndOfDay().Should().Be(new DateTime(2023, 1, 1, 23, 59, 59).AddTicks(9999999));
    }

    [Fact]
    public void DateTimeExtensions_StartOfMonth_ShouldReturnFirstDay()
    {
        var dt = new DateTime(2023, 5, 15, 15, 30, 0);
        dt.StartOfMonth().Should().Be(new DateTime(2023, 5, 1, 0, 0, 0));
    }

    [Fact]
    public void DateTimeExtensions_EndOfMonth_ShouldReturnLastDay()
    {
        var dt = new DateTime(2023, 5, 15, 15, 30, 0);
        dt.EndOfMonth().Should().Be(new DateTime(2023, 5, 31, 23, 59, 59).AddTicks(9999999));
    }

    [Fact]
    public void DateTimeExtensions_StartOfWeek_ShouldReturnFirstDayOfWeek()
    {
        var dt = new DateTime(2023, 10, 18); // Wednesday
        dt.StartOfWeek(DayOfWeek.Monday).Should().Be(new DateTime(2023, 10, 16));
    }

    [Fact]
    public void DateTimeExtensions_IsBetween_ShouldReturnCorrectly()
    {
        var dt = new DateTime(2023, 5, 15);
        dt.IsBetween(new DateTime(2023, 5, 10), new DateTime(2023, 5, 20)).Should().BeTrue();
        dt.IsBetween(new DateTime(2023, 5, 20), new DateTime(2023, 5, 30)).Should().BeFalse();
    }

    [Fact]
    public void DateTimeExtensions_ToRelativeTime_ShouldReturnCorrectly()
    {
        var now = DateTime.UtcNow;
        now.ToRelativeTime().Should().Be("just now");
        now.AddMinutes(-5).ToRelativeTime().Should().Be("5m ago");
        now.AddHours(-2).ToRelativeTime().Should().Be("2h ago");
        now.AddDays(-3).ToRelativeTime().Should().Be("3d ago");
        now.AddDays(-60).ToRelativeTime().Should().Be(now.AddDays(-60).ToString("MMM dd, yyyy"));
    }

    [Fact]
    public void CollectionExtensions_IsNullOrEmpty_ShouldReturnCorrectly()
    {
        List<int>? list = null;
        list.IsNullOrEmpty().Should().BeTrue();
        
        list = new List<int>();
        list.IsNullOrEmpty().Should().BeTrue();
        
        list = new List<int> { 1 };
        list.IsNullOrEmpty().Should().BeFalse();
    }

    [Fact]
    public void CollectionExtensions_WhereNotNull_ShouldFilterNulls()
    {
        var list = new List<string?> { "a", null, "b", null };
        list.WhereNotNull().Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public async Task CollectionExtensions_ToListAsync_ShouldWorkWithAsyncEnumerable()
    {
        async IAsyncEnumerable<int> GetNumbers()
        {
            await Task.Yield();
            yield return 1;
            yield return 2;
        }

        var list = await GetNumbers().ToListAsync();
        list.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void CollectionExtensions_Batch_ShouldCreateBatches()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var batches = list.Batch(2).ToList();
        
        batches.Count.Should().Be(3);
        batches[0].Should().BeEquivalentTo(new[] { 1, 2 });
        batches[1].Should().BeEquivalentTo(new[] { 3, 4 });
        batches[2].Should().BeEquivalentTo(new[] { 5 });
    }
}
