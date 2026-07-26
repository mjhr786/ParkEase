using FluentAssertions;
using Moq;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Application.Commands.EvCharging;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.EvCharging;

public class EvChargingSessionHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<IEvChargingSessionRepository> _sessions = new();
    private readonly Mock<IOcppChargeStationAdapter> _adapter = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public EvChargingSessionHandlerTests()
    {
        _uow.Setup(x => x.Bookings).Returns(_bookings.Object);
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.EvChargingSessions).Returns(_sessions.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sessions.Setup(x => x.AddAsync(It.IsAny<EvChargingSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvChargingSession s, CancellationToken _) => s);
    }

    private (Booking booking, ParkingSpace space) CreateConfirmedEvBooking()
    {
        var space = ParkingSpace.CreateForVendor(
            _ownerId, "EV Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetEvCharging(true, chargerCount: 2, ratePerKwh: 18m, pricingMode: EvPricingMode.PerKwh);

        var booking = Booking.CreateMarketplace(
            _userId, space.Id,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2),
            PricingType.Hourly, VehicleType.Car,
            100, 0, 0, 0, 100,
            includeEvCharging: true,
            evChargingFeeAmount: 0);
        booking.Confirm();
        return (booking, space);
    }

    [Fact]
    public async Task Start_WhenBookingMissing_ReturnsNotFound()
    {
        _bookings.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var handler = new StartEvChargingSessionHandler(_uow.Object, _adapter.Object);
        var result = await handler.HandleAsync(new StartEvChargingSessionCommand(Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Booking not found");
    }

    [Fact]
    public async Task Start_WhenBookingWithoutEv_ReturnsFailure()
    {
        var booking = Booking.CreateMarketplace(
            _userId, Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddHours(2),
            PricingType.Hourly, VehicleType.Car,
            0, 0, 0, 0, 0, includeEvCharging: false);
        booking.Confirm();
        _bookings.Setup(x => x.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var handler = new StartEvChargingSessionHandler(_uow.Object, _adapter.Object);
        var result = await handler.HandleAsync(new StartEvChargingSessionCommand(booking.Id));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("does not include EV");
    }

    [Fact]
    public async Task Start_WhenAdapterRejects_ReturnsFailure()
    {
        var (booking, space) = CreateConfirmedEvBooking();
        _bookings.Setup(x => x.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _sessions.Setup(x => x.GetActiveByBookingIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvChargingSession?)null);
        _adapter.Setup(x => x.StartTransactionAsync(It.IsAny<OcppStartTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcppStartTransactionResult(false, null, "station offline"));

        var handler = new StartEvChargingSessionHandler(_uow.Object, _adapter.Object);
        var result = await handler.HandleAsync(new StartEvChargingSessionCommand(booking.Id));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("station offline");
    }

    [Fact]
    public async Task Start_WhenAccepted_CreatesSession()
    {
        var (booking, space) = CreateConfirmedEvBooking();
        _bookings.Setup(x => x.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _sessions.Setup(x => x.GetActiveByBookingIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvChargingSession?)null);
        _adapter.Setup(x => x.StartTransactionAsync(It.IsAny<OcppStartTransactionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcppStartTransactionResult(true, "tx-99", null));

        var handler = new StartEvChargingSessionHandler(_uow.Object, _adapter.Object);
        var result = await handler.HandleAsync(new StartEvChargingSessionCommand(booking.Id, StationId: "ST-1", MeterStartKwh: 1m));

        result.Success.Should().BeTrue();
        result.Data!.OcppTransactionId.Should().Be("tx-99");
        _sessions.Verify(x => x.AddAsync(It.IsAny<EvChargingSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Start_WhenUnauthorizedActor_ReturnsUnauthorized()
    {
        var (booking, space) = CreateConfirmedEvBooking();
        _bookings.Setup(x => x.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new StartEvChargingSessionHandler(_uow.Object, _adapter.Object);
        var result = await handler.HandleAsync(new StartEvChargingSessionCommand(
            booking.Id, ActorUserId: Guid.NewGuid(), ActorIsAdmin: false));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task RecordMeter_WhenMissing_ReturnsNotFound()
    {
        _sessions.Setup(x => x.GetByOcppTransactionIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvChargingSession?)null);

        var handler = new RecordEvMeterValuesHandler(_uow.Object);
        var result = await handler.HandleAsync(new RecordEvMeterValuesCommand("missing", 5m));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Charge session not found");
    }

    [Fact]
    public async Task RecordMeter_WhenActive_Updates()
    {
        var (booking, space) = CreateConfirmedEvBooking();
        var session = EvChargingSession.Start(booking.Id, space.Id, "tx-1", 18m, 0m, "ST-1", 1, EvChargingSources.Iot);
        _sessions.Setup(x => x.GetByOcppTransactionIdAsync("tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new RecordEvMeterValuesHandler(_uow.Object);
        var result = await handler.HandleAsync(new RecordEvMeterValuesCommand("tx-1", 3.5m));

        result.Success.Should().BeTrue();
        session.LastMeterKwh.Should().Be(3.5m);
    }

    [Fact]
    public async Task Stop_WhenAccepted_CompletesSession()
    {
        var (booking, space) = CreateConfirmedEvBooking();
        var session = EvChargingSession.Start(booking.Id, space.Id, "tx-1", 18m, 0m, "ST-1", 1, EvChargingSources.Iot);
        session.RecordMeterValue(5m);
        _sessions.Setup(x => x.GetByOcppTransactionIdAsync("tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _bookings.Setup(x => x.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _adapter.Setup(x => x.StopTransactionAsync("tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcppStopTransactionResult(true, null));

        var handler = new StopEvChargingSessionHandler(_uow.Object, _adapter.Object);
        var result = await handler.HandleAsync(new StopEvChargingSessionCommand("tx-1", 5m));

        result.Success.Should().BeTrue();
        session.Status.Should().Be(EvChargingSessionStatus.Completed);
    }
}
