using System.Collections.Generic;
using FluentAssertions;
using ParkingApp.BuildingBlocks.Common;
using Xunit;

namespace ParkingApp.UnitTests.BuildingBlocks;

public class PagedResultTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var items = new List<string> { "a", "b", "c" };
        var paged = new PagedResult<string>(items, 15, 2, 3);
        
        paged.Items.Should().BeEquivalentTo(items);
        paged.TotalCount.Should().Be(15);
        paged.PageNumber.Should().Be(2);
        paged.PageSize.Should().Be(3);
        paged.TotalPages.Should().Be(5);
        paged.HasPreviousPage.Should().BeTrue();
        paged.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldCalculatePaginationCorrectly_WhenFirstPage()
    {
        var paged = new PagedResult<string>(new List<string>(), 10, 1, 5);
        paged.TotalPages.Should().Be(2);
        paged.HasPreviousPage.Should().BeFalse();
        paged.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldCalculatePaginationCorrectly_WhenLastPage()
    {
        var paged = new PagedResult<string>(new List<string>(), 10, 2, 5);
        paged.TotalPages.Should().Be(2);
        paged.HasPreviousPage.Should().BeTrue();
        paged.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void Empty_ShouldReturnEmptyPagedResult()
    {
        var paged = PagedResult<string>.Empty(1, 10);
        paged.Items.Should().BeEmpty();
        paged.TotalCount.Should().Be(0);
        paged.TotalPages.Should().Be(0);
    }

    [Fact]
    public void Map_ShouldTransformItems()
    {
        var paged = new PagedResult<int>(new[] { 1, 2, 3 }, 10, 1, 3);
        var mapped = paged.Map(x => x.ToString());
        mapped.Items.Should().BeEquivalentTo("1", "2", "3");
        mapped.TotalCount.Should().Be(10);
    }
}

public class PaginationParamsTests
{
    [Fact]
    public void Properties_ShouldHaveDefaultValues()
    {
        var param = new PaginationParams();
        param.PageNumber.Should().Be(1);
        param.PageSize.Should().Be(10);
        param.Skip.Should().Be(0);
    }

    [Fact]
    public void PageNumber_ShouldNotBeLessThanOne()
    {
        var param = new PaginationParams { PageNumber = 0 };
        param.PageNumber.Should().Be(1);
        param.PageNumber = -5;
        param.PageNumber.Should().Be(1);
    }

    [Fact]
    public void PageSize_ShouldBeClamped()
    {
        var param = new PaginationParams { PageSize = 0 };
        param.PageSize.Should().Be(10); // default
        param.PageSize = 200;
        param.PageSize.Should().Be(100); // max
    }

    [Fact]
    public void Skip_ShouldCalculateCorrectly()
    {
        var param = new PaginationParams { PageNumber = 3, PageSize = 5 };
        param.Skip.Should().Be(10);
    }
}
