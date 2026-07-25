using FluentAssertions;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Queries.Parking;

namespace ParkingApp.UnitTests.CQRS;

public class SearchPagingNormalizeTests
{
    [Fact]
    public void NormalizeSearchPaging_ClampsOversizedPageSize()
    {
        var dto = new ParkingSearchDto(Page: 2, PageSize: 200);
        var normalized = SearchParkingHandler.NormalizeSearchPaging(dto, maxPageSize: 40);

        normalized.Page.Should().Be(2);
        normalized.PageSize.Should().Be(40);
    }

    [Fact]
    public void NormalizeSearchPaging_DefaultsInvalidPage()
    {
        var dto = new ParkingSearchDto(Page: 0, PageSize: 0);
        var normalized = SearchParkingHandler.NormalizeSearchPaging(dto, maxPageSize: 40);

        normalized.Page.Should().Be(1);
        normalized.PageSize.Should().Be(20);
    }
}
