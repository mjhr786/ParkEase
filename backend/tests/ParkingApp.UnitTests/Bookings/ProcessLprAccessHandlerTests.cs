using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Application.Commands.Lpr;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Bookings;

public class ProcessLprAccessHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<ILprAccessAttemptRepository> _attempts = new();
    private readonly Mock<ILprPlateRuleRepository> _plateRules = new();

    public ProcessLprAccessHandlerTests()
    {
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.Bookings).Returns(_bookings.Object);
        _uow.Setup(x => x.LprAccessAttempts).Returns(_attempts.Object);
        _uow.Setup(x => x.LprPlateRules).Returns(_plateRules.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _attempts.Setup(x => x.AddAsync(It.IsAny<LprAccessAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LprAccessAttempt a, CancellationToken _) => a);
        _plateRules.Setup(x => x.GetEnabledByParkingSpaceIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LprPlateRule>());
    }

    private ProcessLprAccessHandler CreateSut(LprAccessOptions? options = null)
    {
        var monitor = new Mock<IOptionsMonitor<LprAccessOptions>>();
        monitor.Setup(x => x.CurrentValue).Returns(options ?? new LprAccessOptions());
        return new ProcessLprAccessHandler(
            _uow.Object,
            monitor.Object,
            NullLogger<ProcessLprAccessHandler>.Instance,
            fileStorage: null);
    }

    [Fact]
    public async Task Handle_InvalidPlate_DeniesWithoutLookup()
    {
        var spaceId = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "   ",
            spaceId,
            LprDirection.Entry,
            null,
            LprAccessSources.Simulator,
            "k1"));

        result.Success.Should().BeTrue();
        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.InvalidPlate);
        _spaces.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownFacility_Denies()
    {
        var spaceId = Guid.NewGuid();
        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234",
            spaceId,
            LprDirection.Entry,
            null,
            LprAccessSources.Iot,
            "cam1"));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.UnknownFacility);
    }

    [Fact]
    public async Task Handle_NoMatchingBooking_Denies()
    {
        var spaceId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetLprEnabled(true);

        // Force known id via reflection-friendly internal set if needed — use returned entity Id
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, spaceId);

        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _bookings.Setup(x => x.FindLprCandidatesAsync(
                spaceId, "KA01AB1234", LprDirection.Entry, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234",
            spaceId,
            LprDirection.Entry,
            DateTime.UtcNow,
            LprAccessSources.Simulator,
            null));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.NoMatchingBooking);
        _attempts.Verify(x => x.AddAsync(It.Is<LprAccessAttempt>(a => a.Decision == LprAccessDecision.Denied), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EntryGranted_ChecksInAndPersistsAttempt()
    {
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetLprEnabled(true);
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, spaceId);

        var booking = Booking.CreateMarketplace(
            userId,
            spaceId,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddHours(2),
            PricingType.Hourly,
            VehicleType.Car,
            100, 0, 0, 0, 100,
            vehicleNumber: "KA01AB1234");
        booking.Confirm();

        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _bookings.Setup(x => x.FindLprCandidatesAsync(
                spaceId, "KA01AB1234", LprDirection.Entry, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { booking });

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "ka 01 ab 1234",
            spaceId,
            LprDirection.Entry,
            DateTime.UtcNow,
            LprAccessSources.Simulator,
            "admin"));

        result.Success.Should().BeTrue();
        result.Data!.AccessGranted.Should().BeTrue();
        result.Data.BookingId.Should().Be(booking.Id);
        booking.Status.Should().Be(BookingStatus.InProgress);
        _bookings.Verify(x => x.Update(booking), Times.Once);
        _attempts.Verify(x => x.AddAsync(It.Is<LprAccessAttempt>(a => a.Decision == LprAccessDecision.Granted), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AmbiguousMatch_Denies()
    {
        var spaceId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetLprEnabled(true);
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, spaceId);

        var b1 = new Booking { Status = BookingStatus.Confirmed, ParkingSpaceId = spaceId, VehicleNumber = "ABC" };
        var b2 = new Booking { Status = BookingStatus.Confirmed, ParkingSpaceId = spaceId, VehicleNumber = "ABC" };

        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _bookings.Setup(x => x.FindLprCandidatesAsync(
                spaceId, "ABC", LprDirection.Entry, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking> { b1, b2 });

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "ABC",
            spaceId,
            LprDirection.Entry,
            DateTime.UtcNow,
            LprAccessSources.Iot,
            "k"));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.AmbiguousMatch);
    }

    [Fact]
    public async Task Handle_LprDisabled_Denies()
    {
        var spaceId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, spaceId);
        // IsLprEnabled defaults false

        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234",
            spaceId,
            LprDirection.Entry,
            DateTime.UtcNow,
            LprAccessSources.Iot,
            "k"));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.LprDisabled);
        _bookings.Verify(
            x => x.FindLprCandidatesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<LprDirection>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_KeyNotAuthorizedForFacility_Denies()
    {
        var spaceId = Guid.NewGuid();
        var otherSpace = Guid.NewGuid();
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234",
            spaceId,
            LprDirection.Entry,
            DateTime.UtcNow,
            LprAccessSources.Iot,
            "cam-a",
            AllowedParkingSpaceIds: new[] { otherSpace }));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.KeyNotAuthorizedForFacility);
        _spaces.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_VendorSimulator_NotOwner_Denies()
    {
        var ownerId = Guid.NewGuid();
        var otherVendor = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            ownerId, "Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetLprEnabled(true);
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, spaceId);

        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234",
            spaceId,
            LprDirection.Entry,
            DateTime.UtcNow,
            LprAccessSources.Simulator,
            $"vendor:{otherVendor}",
            SimulatorUserId: otherVendor,
            SimulatorIsAdmin: false));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.NotFacilityOwner);
    }

    [Fact]
    public async Task Handle_PlateDenied_Denies()
    {
        var spaceId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetLprEnabled(true);
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, spaceId);

        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _plateRules.Setup(x => x.GetEnabledByParkingSpaceIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                LprPlateRule.Create(spaceId, "KA01AB1234", LprPlateRuleType.Deny, Guid.NewGuid())
            });

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234", spaceId, LprDirection.Entry, DateTime.UtcNow, LprAccessSources.Iot, "k"));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.PlateDenied);
        _bookings.Verify(
            x => x.FindLprCandidatesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<LprDirection>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PlateNotOnAllowlist_Denies()
    {
        var spaceId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetLprEnabled(true);
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, spaceId);

        _spaces.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _plateRules.Setup(x => x.GetEnabledByParkingSpaceIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                LprPlateRule.Create(spaceId, "VIPONLY1", LprPlateRuleType.Allow, Guid.NewGuid())
            });

        var sut = CreateSut();
        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234", spaceId, LprDirection.Entry, DateTime.UtcNow, LprAccessSources.Iot, "k"));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.PlateNotAllowlisted);
    }

    [Fact]
    public async Task Handle_LowConfidence_Denies()
    {
        var spaceId = Guid.NewGuid();
        var sut = CreateSut(new LprAccessOptions { MinConfidence = 0.8 });

        var result = await sut.HandleAsync(new ProcessLprAccessCommand(
            "KA01AB1234",
            spaceId,
            LprDirection.Entry,
            DateTime.UtcNow,
            LprAccessSources.Iot,
            "k",
            Confidence: 0.5));

        result.Data!.AccessGranted.Should().BeFalse();
        result.Data.DenialReasonCode.Should().Be(LprDenialReasonCodes.LowConfidence);
        _spaces.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class OverstayBookingDomainTests
{
    [Fact]
    public void TryMarkOverstayNotified_OnlyOnce()
    {
        var booking = new Booking
        {
            Status = BookingStatus.InProgress,
            EndDateTime = DateTime.UtcNow.AddHours(-1)
        };

        booking.TryMarkOverstayNotified(DateTime.UtcNow).Should().BeTrue();
        booking.OverstayNotifiedAt.Should().NotBeNull();
        booking.TryMarkOverstayNotified(DateTime.UtcNow).Should().BeFalse();
    }
}
