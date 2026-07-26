using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Common;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.ValueObjects;

namespace ParkingApp.Marketplace.Application.Commands.Lpr;

internal sealed class ProcessLprAccessHandler : ICommandHandler<ProcessLprAccessCommand, ApiResponse<LprAccessResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IFileStorage? _fileStorage;
    private readonly IOptionsMonitor<LprAccessOptions> _options;
    private readonly ILogger<ProcessLprAccessHandler> _logger;

    public ProcessLprAccessHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IOptionsMonitor<LprAccessOptions> options,
        ILogger<ProcessLprAccessHandler> logger,
        IFileStorage? fileStorage = null)
    {
        _unitOfWork = unitOfWork;
        _options = options;
        _logger = logger;
        _fileStorage = fileStorage;
    }

    public async Task<ApiResponse<LprAccessResultDto>> HandleAsync(
        ProcessLprAccessCommand command,
        CancellationToken cancellationToken = default)
    {
        var occurredAt = (command.OccurredAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        var plateRaw = command.LicensePlate ?? string.Empty;
        var normalized = LicensePlate.Normalize(plateRaw) ?? string.Empty;
        var source = string.IsNullOrWhiteSpace(command.Source) ? LprAccessSources.Iot : command.Source.Trim();
        var confidence = command.Confidence;
        var imageUrl = command.ImageUrl;

        if (string.IsNullOrEmpty(normalized))
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.InvalidPlate, "License plate is missing or invalid.",
                source, command.ClientKeyId, cancellationToken, confidence: confidence, imageUrl: imageUrl);
        }

        if (command.ParkingSpaceId == Guid.Empty)
        {
            return await DenyAsync(
                Guid.Empty, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.UnknownFacility, "Parking facility is required.",
                source, command.ClientKeyId, cancellationToken, persistAttempt: false,
                confidence: confidence, imageUrl: imageUrl);
        }

        // Optional confidence gate (only when camera supplies a score and threshold is configured)
        var minConfidence = _options.CurrentValue.MinConfidence;
        if (minConfidence is > 0 and <= 1
            && confidence.HasValue
            && confidence.Value < minConfidence.Value)
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.LowConfidence,
                $"Plate confidence {confidence.Value:F2} is below the minimum {minConfidence.Value:F2}.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        if (command.AllowedParkingSpaceIds is { Count: > 0 }
            && !command.AllowedParkingSpaceIds.Contains(command.ParkingSpaceId))
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.KeyNotAuthorizedForFacility,
                "This API key is not authorized for the requested parking facility.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, cancellationToken);
        if (parking is null || !parking.IsActive)
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.UnknownFacility,
                "Parking facility was not found or is inactive.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        if (!parking.IsLprEnabled)
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.LprDisabled,
                "LPR is not enabled for this parking facility.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        if (command.SimulatorUserId.HasValue
            && !command.SimulatorIsAdmin
            && parking.OwnerId != command.SimulatorUserId.Value)
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.NotFacilityOwner,
                "You can only simulate LPR for parking spaces you own.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        // Best-effort image upload from base64 (does not block access on failure)
        imageUrl = await TryResolveImageUrlAsync(command, imageUrl, cancellationToken);

        var plateRules = await _unitOfWork.LprPlateRules.GetEnabledByParkingSpaceIdAsync(
            command.ParkingSpaceId, cancellationToken);

        if (plateRules.Any(r =>
                r.RuleType == LprPlateRuleType.Deny
                && LicensePlate.Matches(r.LicensePlateNormalized, normalized)))
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.PlateDenied,
                "This license plate is denied at this facility.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        var allowRules = plateRules.Where(r => r.RuleType == LprPlateRuleType.Allow).ToList();
        if (allowRules.Count > 0
            && allowRules.All(r => !LicensePlate.Matches(r.LicensePlateNormalized, normalized)))
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.PlateNotAllowlisted,
                "This facility only allows listed plates for LPR access.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        var candidates = await _unitOfWork.Bookings.FindLprCandidatesAsync(
            command.ParkingSpaceId, normalized, command.Direction, occurredAt, cancellationToken);

        if (candidates.Count == 0)
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.NoMatchingBooking,
                "No eligible booking for this plate at this facility.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        if (candidates.Count > 1)
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.AmbiguousMatch,
                "Multiple eligible bookings match this plate; resolve manually.",
                source, command.ClientKeyId, cancellationToken,
                confidence: confidence, imageUrl: imageUrl);
        }

        var booking = candidates[0];

        try
        {
            if (command.Direction == LprDirection.Entry)
            {
                booking.CheckIn(occurredAt);
            }
            else
            {
                // Finalize overstay fee at exit before completing the booking.
                if (booking.ParkingSpace is null && booking.ParkingSpaceId != Guid.Empty)
                {
                    // ParkingSpace usually loaded via FindLprCandidates; ensure rate available.
                }

                OverstayFeeAssessor.TryAssess(
                    booking,
                    _options.CurrentValue.Overstay,
                    occurredAt,
                    out _);
                booking.CheckOut(occurredAt);
            }

            var attempt = LprAccessAttempt.CreateGranted(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                booking.Id, source, command.ClientKeyId, confidence, imageUrl);

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.LprAccessAttempts.AddAsync(attempt, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // PII: do not log full plates at Information — use AttemptId + ids only.
            _logger.LogInformation(
                "LPR {Direction} granted AttemptId={AttemptId} SpaceId={SpaceId} BookingId={BookingId} Confidence={Confidence} PlateSuffix={PlateSuffix}",
                command.Direction, attempt.Id, command.ParkingSpaceId, booking.Id, confidence, PlateSuffix(normalized));

            return new ApiResponse<LprAccessResultDto>(true, "Access granted", new LprAccessResultDto(
                true, LprAccessDecision.Granted.ToString(), null, null,
                booking.Id, booking.BookingReference, command.ParkingSpaceId, normalized,
                command.Direction.ToString(), occurredAt, attempt.Id, confidence, imageUrl));
        }
        catch (BusinessRuleException ex) when (ex.RuleName is "Booking.CheckInWindow")
        {
            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                LprDenialReasonCodes.OutsideCheckInWindow, ex.Message,
                source, command.ClientKeyId, cancellationToken, bookingId: booking.Id,
                confidence: confidence, imageUrl: imageUrl);
        }
        catch (BusinessRuleException ex) when (ex.RuleName is "Booking.CheckIn" or "Booking.CheckOut")
        {
            var code = booking.Status == BookingStatus.Completed
                ? LprDenialReasonCodes.AlreadyCompleted
                : LprDenialReasonCodes.InvalidState;

            return await DenyAsync(
                command.ParkingSpaceId, plateRaw, normalized, command.Direction, occurredAt,
                code, ex.Message, source, command.ClientKeyId, cancellationToken,
                bookingId: booking.Id, confidence: confidence, imageUrl: imageUrl);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<LprAccessResultDto>(ex);
        }
    }

    private async Task<string?> TryResolveImageUrlAsync(
        ProcessLprAccessCommand command,
        string? existingUrl,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(existingUrl))
            return existingUrl.Trim();

        if (string.IsNullOrWhiteSpace(command.ImageBase64) || _fileStorage is null)
            return null;

        try
        {
            var payload = command.ImageBase64.Trim();
            var contentType = "image/jpeg";
            var base64 = payload;

            if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = payload.IndexOf(',');
                if (comma > 0)
                {
                    var header = payload[..comma];
                    base64 = payload[(comma + 1)..];
                    if (header.Contains("image/png", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/png";
                    else if (header.Contains("image/webp", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/webp";
                }
            }

            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length == 0 || bytes.Length > 2_000_000)
                return null;

            var ext = contentType switch
            {
                "image/png" => "png",
                "image/webp" => "webp",
                _ => "jpg"
            };

            await using var stream = new MemoryStream(bytes);
            var fileName = $"lpr/{command.ParkingSpaceId:N}/{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.{ext}";
            return await _fileStorage.UploadFileAsync(stream, fileName, contentType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store LPR plate image; continuing without image");
            return null;
        }
    }

    private async Task<ApiResponse<LprAccessResultDto>> DenyAsync(
        Guid parkingSpaceId,
        string plateRaw,
        string normalized,
        LprDirection direction,
        DateTime occurredAt,
        string reasonCode,
        string message,
        string source,
        string? clientKeyId,
        CancellationToken cancellationToken,
        Guid? bookingId = null,
        bool persistAttempt = true,
        double? confidence = null,
        string? imageUrl = null)
    {
        Guid? attemptId = null;

        if (persistAttempt && parkingSpaceId != Guid.Empty)
        {
            try
            {
                var attempt = LprAccessAttempt.CreateDenied(
                    parkingSpaceId, plateRaw, normalized, direction, occurredAt,
                    reasonCode, source, clientKeyId, bookingId, confidence, imageUrl);

                await _unitOfWork.LprAccessAttempts.AddAsync(attempt, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                attemptId = attempt.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist LPR denial attempt for {ReasonCode}", reasonCode);
            }
        }

        // PII: do not log full plates at Information — use AttemptId + denial code.
        _logger.LogInformation(
            "LPR {Direction} denied Reason={Reason} AttemptId={AttemptId} SpaceId={SpaceId} BookingId={BookingId} PlateSuffix={PlateSuffix}",
            direction, reasonCode, attemptId, parkingSpaceId, bookingId, PlateSuffix(normalized));

        return new ApiResponse<LprAccessResultDto>(true, "Access denied", new LprAccessResultDto(
            false, LprAccessDecision.Denied.ToString(), reasonCode, message,
            bookingId, null, parkingSpaceId, normalized, direction.ToString(),
            occurredAt, attemptId, confidence, imageUrl));
    }

    /// <summary>Last up to 4 characters of normalized plate for coarse ops correlation without full PII.</summary>
    private static string PlateSuffix(string? normalized)
    {
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;
        return normalized.Length <= 4 ? "****" : normalized[^4..];
    }
}
