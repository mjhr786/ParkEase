using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.EvCharging;

internal sealed class StartEvChargingSessionHandler
    : ICommandHandler<StartEvChargingSessionCommand, ApiResponse<EvChargingSessionDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;
    private readonly IOcppChargeStationAdapter _adapter;

    public StartEvChargingSessionHandler(IMarketplaceUnitOfWork uow, IOcppChargeStationAdapter adapter)
    {
        _uow = uow;
        _adapter = adapter;
    }

    public async Task<ApiResponse<EvChargingSessionDto>> HandleAsync(
        StartEvChargingSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        var booking = await _uow.Bookings.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<EvChargingSessionDto>(false, "Booking not found", null);

        if (!booking.IncludeEvCharging)
            return new ApiResponse<EvChargingSessionDto>(false, "Booking does not include EV charging.", null);

        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.InProgress
            or BookingStatus.PendingExtension or BookingStatus.AwaitingExtensionPayment))
        {
            return new ApiResponse<EvChargingSessionDto>(
                false,
                $"Cannot start EV charge while booking is {booking.Status}.",
                null);
        }

        var space = await _uow.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
        if (space is null || !space.HasEvCharging)
            return new ApiResponse<EvChargingSessionDto>(false, "Facility does not offer EV charging.", null);

        if (command.ActorUserId is { } actor && !command.ActorIsAdmin
            && actor != booking.UserId && actor != space.OwnerId)
        {
            return new ApiResponse<EvChargingSessionDto>(false, "Unauthorized", null);
        }

        var open = await _uow.EvChargingSessions.GetActiveByBookingIdAsync(booking.Id, cancellationToken);
        if (open is not null)
            return new ApiResponse<EvChargingSessionDto>(false, "An active EV charge session already exists for this booking.", open.ToDto());

        var stationId = string.IsNullOrWhiteSpace(command.StationId) ? "MOCK-1" : command.StationId.Trim();
        var adapterResult = await _adapter.StartTransactionAsync(
            new OcppStartTransactionRequest(
                booking.Id,
                booking.ParkingSpaceId,
                stationId,
                command.ConnectorId,
                command.MeterStartKwh),
            cancellationToken);

        if (!adapterResult.Accepted || string.IsNullOrWhiteSpace(adapterResult.OcppTransactionId))
        {
            return new ApiResponse<EvChargingSessionDto>(
                false,
                adapterResult.Message ?? "Charge station rejected start transaction.",
                null);
        }

        // PerKwh uses space rate; Hourly sessions still track meter for ops but fee was locked at book.
        var ratePerKwh = space.EvPricingMode == EvPricingMode.PerKwh
            ? space.EvRatePerKwh
            : 0m;

        try
        {
            var session = EvChargingSession.Start(
                booking.Id,
                booking.ParkingSpaceId,
                adapterResult.OcppTransactionId,
                ratePerKwh,
                command.MeterStartKwh,
                stationId,
                command.ConnectorId,
                command.Source);

            await _uow.EvChargingSessions.AddAsync(session, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return new ApiResponse<EvChargingSessionDto>(true, "EV charge session started", session.ToDto());
        }
        catch (DomainException ex)
        {
            return new ApiResponse<EvChargingSessionDto>(false, ex.Message, null);
        }
    }
}

internal sealed class RecordEvMeterValuesHandler
    : ICommandHandler<RecordEvMeterValuesCommand, ApiResponse<EvChargingSessionDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public RecordEvMeterValuesHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<EvChargingSessionDto>> HandleAsync(
        RecordEvMeterValuesCommand command,
        CancellationToken cancellationToken = default)
    {
        var session = await _uow.EvChargingSessions.GetByOcppTransactionIdAsync(
            command.OcppTransactionId, cancellationToken);
        if (session is null)
            return new ApiResponse<EvChargingSessionDto>(false, "Charge session not found", null);

        try
        {
            session.RecordMeterValue(command.MeterKwh);
            _uow.EvChargingSessions.Update(session);
            await _uow.SaveChangesAsync(cancellationToken);
            return new ApiResponse<EvChargingSessionDto>(true, "Meter values recorded", session.ToDto());
        }
        catch (DomainException ex)
        {
            return new ApiResponse<EvChargingSessionDto>(false, ex.Message, null);
        }
    }
}

internal sealed class StopEvChargingSessionHandler
    : ICommandHandler<StopEvChargingSessionCommand, ApiResponse<EvChargingSessionDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;
    private readonly IOcppChargeStationAdapter _adapter;

    public StopEvChargingSessionHandler(IMarketplaceUnitOfWork uow, IOcppChargeStationAdapter adapter)
    {
        _uow = uow;
        _adapter = adapter;
    }

    public async Task<ApiResponse<EvChargingSessionDto>> HandleAsync(
        StopEvChargingSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        var session = await _uow.EvChargingSessions.GetByOcppTransactionIdAsync(
            command.OcppTransactionId, cancellationToken);
        if (session is null)
            return new ApiResponse<EvChargingSessionDto>(false, "Charge session not found", null);

        if (session.Status == EvChargingSessionStatus.Completed)
            return new ApiResponse<EvChargingSessionDto>(true, "Session already completed", session.ToDto());

        var booking = await _uow.Bookings.GetByIdAsync(session.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<EvChargingSessionDto>(false, "Booking not found", null);

        var space = await _uow.ParkingSpaces.GetByIdAsync(session.ParkingSpaceId, cancellationToken);
        if (command.ActorUserId is { } actor && !command.ActorIsAdmin
            && actor != booking.UserId
            && (space is null || actor != space.OwnerId))
        {
            return new ApiResponse<EvChargingSessionDto>(false, "Unauthorized", null);
        }

        try
        {
            var fee = session.Stop(command.MeterStopKwh);
            await _adapter.StopTransactionAsync(session.OcppTransactionId, cancellationToken);

            // Only settle energy fee onto booking when facility bills per kWh.
            if (space?.EvPricingMode == EvPricingMode.PerKwh && fee > 0)
                booking.ApplyEvEnergyFee(fee);

            _uow.EvChargingSessions.Update(session);
            _uow.Bookings.Update(booking);
            await _uow.SaveChangesAsync(cancellationToken);

            return new ApiResponse<EvChargingSessionDto>(
                true,
                fee > 0 ? $"EV charge stopped; energy fee ₹{fee:0.00}" : "EV charge stopped",
                session.ToDto());
        }
        catch (DomainException ex)
        {
            return new ApiResponse<EvChargingSessionDto>(false, ex.Message, null);
        }
    }
}

internal sealed class SimulateEvChargingSessionHandler
    : ICommandHandler<SimulateEvChargingSessionCommand, ApiResponse<EvChargingSessionDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;
    private readonly IOcppChargeStationAdapter _adapter;

    public SimulateEvChargingSessionHandler(IMarketplaceUnitOfWork uow, IOcppChargeStationAdapter adapter)
    {
        _uow = uow;
        _adapter = adapter;
    }

    public async Task<ApiResponse<EvChargingSessionDto>> HandleAsync(
        SimulateEvChargingSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.EnergyKwh <= 0)
            return new ApiResponse<EvChargingSessionDto>(false, "Energy kWh must be positive", null);

        var startHandler = new StartEvChargingSessionHandler(_uow, _adapter);
        var start = await startHandler.HandleAsync(
            new StartEvChargingSessionCommand(
                command.BookingId,
                command.StationId,
                command.ConnectorId,
                MeterStartKwh: 0m,
                Source: EvChargingSources.Simulator,
                ActorUserId: command.ActorUserId == Guid.Empty ? null : command.ActorUserId,
                ActorIsAdmin: command.ActorIsAdmin),
            cancellationToken);

        if (!start.Success || start.Data is null)
            return start;

        var meterEnd = command.EnergyKwh;
        var meterHandler = new RecordEvMeterValuesHandler(_uow);
        var mid = Math.Round(meterEnd / 2m, 3, MidpointRounding.AwayFromZero);
        if (mid > 0)
        {
            await meterHandler.HandleAsync(
                new RecordEvMeterValuesCommand(start.Data.OcppTransactionId, mid, EvChargingSources.Simulator),
                cancellationToken);
        }

        var stopHandler = new StopEvChargingSessionHandler(_uow, _adapter);
        return await stopHandler.HandleAsync(
            new StopEvChargingSessionCommand(
                start.Data.OcppTransactionId,
                MeterStopKwh: meterEnd,
                Source: EvChargingSources.Simulator,
                ActorUserId: command.ActorUserId == Guid.Empty ? null : command.ActorUserId,
                ActorIsAdmin: command.ActorIsAdmin),
            cancellationToken);
    }
}
