using Microsoft.Extensions.Logging;
using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.Services;

public class ParkingAvailabilityPredictionService : IParkingAvailabilityPredictionService
{
    private const int HistoryLookbackDays = 42;
    private const int WeeklySampleCount = 6;
    private const int DailySampleCount = 14;
    private const int RecentWindowSampleCount = 6;

    private static readonly TimeSpan SingleForecastCacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan OwnerForecastCacheDuration = TimeSpan.FromSeconds(30);

    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IParkingAvailabilityModelService _modelService;
    private readonly ILogger<ParkingAvailabilityPredictionService> _logger;

    public ParkingAvailabilityPredictionService(
        IMarketplaceUnitOfWork unitOfWork,
        ICacheService cache,
        IParkingAvailabilityModelService modelService,
        ILogger<ParkingAvailabilityPredictionService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _modelService = modelService;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingAvailabilityForecastDto>> GetForecastAsync(
        Guid parkingSpaceId,
        int horizonHours = 24,
        int intervalMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var normalizedHorizon = NormalizeHorizonHours(horizonHours);
        var normalizedInterval = NormalizeIntervalMinutes(intervalMinutes);
        var cacheKey = CacheKeys.ParkingForecast(parkingSpaceId, normalizedHorizon, normalizedInterval);

        var cached = await _cache.GetAsync<ParkingAvailabilityForecastDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return new ApiResponse<ParkingAvailabilityForecastDto>(true, null, cached);
        }

        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<ParkingAvailabilityForecastDto>(false, "Parking space not found", null);
        }

        var forecast = await BuildForecastAsync(
            parking,
            normalizedHorizon,
            normalizedInterval,
            cancellationToken);

        await _cache.SetAsync(cacheKey, forecast, SingleForecastCacheDuration, cancellationToken);

        return new ApiResponse<ParkingAvailabilityForecastDto>(true, null, forecast);
    }

    public async Task<ApiResponse<List<ParkingAvailabilityForecastDto>>> GetOwnerForecastsAsync(
        Guid ownerId,
        int horizonHours = 12,
        int intervalMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var normalizedHorizon = NormalizeHorizonHours(horizonHours);
        var normalizedInterval = NormalizeIntervalMinutes(intervalMinutes);
        var cacheKey = CacheKeys.OwnerForecast(ownerId, normalizedHorizon, normalizedInterval);

        var cached = await _cache.GetAsync<List<ParkingAvailabilityForecastDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return new ApiResponse<List<ParkingAvailabilityForecastDto>>(true, null, cached);
        }

        var parkingSpaces = (await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(ownerId, cancellationToken)).ToList();
        if (parkingSpaces.Count == 0)
        {
            return new ApiResponse<List<ParkingAvailabilityForecastDto>>(true, null, new List<ParkingAvailabilityForecastDto>());
        }

        var nowUtc = DateTime.UtcNow;
        var alignedNowUtc = AlignToInterval(nowUtc, normalizedInterval);
        var historyStartUtc = alignedNowUtc.AddDays(-HistoryLookbackDays);
        var forecastEndUtc = alignedNowUtc.AddHours(normalizedHorizon);
        var parkingIds = parkingSpaces.Select(parking => parking.Id).ToList();

        var bookings = (await _unitOfWork.Bookings.GetForecastRelevantBookingsForSpacesAsync(
            parkingIds,
            historyStartUtc,
            forecastEndUtc,
            cancellationToken)).ToList();

        var bookingsByParkingId = bookings
            .GroupBy(booking => booking.ParkingSpaceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<Booking>)group.ToList());

        var forecasts = new List<ParkingAvailabilityForecastDto>(parkingSpaces.Count);
        foreach (var parking in parkingSpaces)
        {
            var forecast = await BuildForecastAsync(
                parking,
                bookingsByParkingId.GetValueOrDefault(parking.Id) ?? Array.Empty<Booking>(),
                normalizedHorizon,
                normalizedInterval,
                nowUtc,
                cancellationToken);
            forecasts.Add(forecast);
        }

        await _cache.SetAsync(cacheKey, forecasts, OwnerForecastCacheDuration, cancellationToken);

        return new ApiResponse<List<ParkingAvailabilityForecastDto>>(true, null, forecasts);
    }

    private async Task<ParkingAvailabilityForecastDto> BuildForecastAsync(
        ParkingSpace parking,
        int horizonHours,
        int intervalMinutes,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var alignedNowUtc = AlignToInterval(nowUtc, intervalMinutes);
        var historyStartUtc = alignedNowUtc.AddDays(-HistoryLookbackDays);
        var forecastEndUtc = alignedNowUtc.AddHours(horizonHours);

        var bookings = (await _unitOfWork.Bookings.GetForecastRelevantBookingsForSpacesAsync(
            new[] { parking.Id },
            historyStartUtc,
            forecastEndUtc,
            cancellationToken)).ToList();

        return await BuildForecastAsync(
            parking,
            bookings,
            horizonHours,
            intervalMinutes,
            nowUtc,
            cancellationToken);
    }

    private async Task<ParkingAvailabilityForecastDto> BuildForecastAsync(
        ParkingSpace parking,
        IReadOnlyCollection<Booking> bookings,
        int horizonHours,
        int intervalMinutes,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var alignedNowUtc = AlignToInterval(nowUtc, intervalMinutes);
        var bucketCount = Math.Max(1, (int)Math.Ceiling((double)(horizonHours * 60) / intervalMinutes));
        var totalSpots = Math.Max(1, parking.TotalSpots);
        var historicalBookings = bookings.Where(booking => booking.EndDateTime <= nowUtc).ToList();
        var futureBookings = bookings.Where(booking => booking.EndDateTime > nowUtc).ToList();
        var rollingRecentRates = BuildRecentOccupancySeed(
            historicalBookings,
            alignedNowUtc,
            intervalMinutes,
            totalSpots,
            RecentWindowSampleCount);

        var buckets = new List<ParkingAvailabilityBucketDto>(bucketCount);

        for (var index = 0; index < bucketCount; index++)
        {
            var bucketStartUtc = alignedNowUtc.AddMinutes(index * intervalMinutes);
            var bucketEndUtc = bucketStartUtc.AddMinutes(intervalMinutes);
            var hoursAhead = Math.Max(0, (bucketStartUtc - nowUtc).TotalHours);

            var deterministicBooked = Clamp(
                futureBookings
                    .Where(booking => Overlaps(booking, bucketStartUtc, bucketEndUtc))
                    .Sum(GetFutureBookingWeight),
                0,
                totalSpots);

            var weeklySamples = BuildWeeklySamples(historicalBookings, bucketStartUtc, bucketEndUtc, totalSpots);
            var dailySamples = BuildDailySamples(historicalBookings, bucketStartUtc, bucketEndUtc, totalSpots);
            var historicalRate = CalculateHistoricalRate(weeklySamples, dailySamples);
            var recent1Rate = AverageRecent(rollingRecentRates, 1);
            var recent3Rate = AverageRecent(rollingRecentRates, 3);
            var recent6Rate = AverageRecent(rollingRecentRates, 6);

            var modelPrediction = await _modelService.PredictOccupancyAsync(
                new ParkingAvailabilityModelInputDto(
                    parking.Id,
                    totalSpots,
                    parking.HourlyRate,
                    (int)parking.ParkingType,
                    parking.Is24Hours,
                    parking.IsActive,
                    parking.AverageRating,
                    parking.TotalReviews,
                    bucketStartUtc,
                    historicalRate,
                    dailySamples.Count > 0 ? Clamp(dailySamples.Average(), 0, 1) : historicalRate,
                    recent1Rate,
                    recent3Rate,
                    recent6Rate),
                intervalMinutes,
                cancellationToken);

            var baselineRate = ResolveBaselineOccupancyRate(parking, historicalRate, modelPrediction);
            var predictedBookedDouble = Math.Max(deterministicBooked, baselineRate * totalSpots);
            var predictedBooked = (int)Math.Ceiling(Clamp(predictedBookedDouble, 0, totalSpots));
            var predictedAvailable = Math.Clamp(totalSpots - predictedBooked, 0, totalSpots);
            var predictedOccupancyRate = Math.Round(predictedBooked / (double)totalSpots, 3);
            var confidenceScore = CalculateConfidenceScore(
                weeklySamples.Count,
                dailySamples.Count,
                deterministicBooked,
                hoursAhead,
                modelPrediction);

            buckets.Add(new ParkingAvailabilityBucketDto(
                bucketStartUtc,
                bucketEndUtc,
                (int)Math.Ceiling(deterministicBooked),
                predictedBooked,
                predictedAvailable,
                Math.Round(historicalRate, 3),
                predictedOccupancyRate,
                confidenceScore,
                GetAvailabilityBand(predictedAvailable, totalSpots, parking.IsActive, deterministicBooked > 0),
                bucketStartUtc <= nowUtc && nowUtc < bucketEndUtc));

            if (rollingRecentRates.Count == RecentWindowSampleCount)
            {
                rollingRecentRates.Dequeue();
            }

            rollingRecentRates.Enqueue(predictedOccupancyRate);
        }

        var currentBucket = buckets.First();
        var likelyFullAtUtc = buckets
            .FirstOrDefault(bucket => bucket.PredictedAvailableSpots <= 0)?
            .StartDateTimeUtc;

        return new ParkingAvailabilityForecastDto(
            parking.Id,
            parking.Title,
            parking.IsActive,
            totalSpots,
            nowUtc,
            horizonHours,
            intervalMinutes,
            currentBucket.PredictedBookedSpots,
            currentBucket.PredictedAvailableSpots,
            currentBucket.PredictedOccupancyRate,
            currentBucket.ConfidenceScore,
            currentBucket.AvailabilityBand,
            (int)Math.Round(buckets.Average(bucket => bucket.PredictedAvailableSpots)),
            buckets.Max(bucket => bucket.PredictedBookedSpots),
            likelyFullAtUtc,
            buckets);
    }

    private static Queue<double> BuildRecentOccupancySeed(
        IReadOnlyCollection<Booking> historicalBookings,
        DateTime alignedNowUtc,
        int intervalMinutes,
        int totalSpots,
        int sampleCount)
    {
        var rates = new Queue<double>(sampleCount);
        for (var offset = sampleCount; offset >= 1; offset--)
        {
            var bucketStartUtc = alignedNowUtc.AddMinutes(-intervalMinutes * offset);
            var bucketEndUtc = bucketStartUtc.AddMinutes(intervalMinutes);
            rates.Enqueue(CalculateSampleOccupancy(historicalBookings, bucketStartUtc, bucketEndUtc, totalSpots));
        }

        return rates;
    }

    private static List<double> BuildWeeklySamples(
        IReadOnlyCollection<Booking> historicalBookings,
        DateTime bucketStartUtc,
        DateTime bucketEndUtc,
        int totalSpots)
    {
        var samples = new List<double>(WeeklySampleCount);
        for (var week = 1; week <= WeeklySampleCount; week++)
        {
            var sampleStartUtc = bucketStartUtc.AddDays(-7 * week);
            var sampleEndUtc = bucketEndUtc.AddDays(-7 * week);
            samples.Add(CalculateSampleOccupancy(historicalBookings, sampleStartUtc, sampleEndUtc, totalSpots));
        }

        return samples;
    }

    private static List<double> BuildDailySamples(
        IReadOnlyCollection<Booking> historicalBookings,
        DateTime bucketStartUtc,
        DateTime bucketEndUtc,
        int totalSpots)
    {
        var samples = new List<double>(DailySampleCount);
        for (var day = 1; day <= DailySampleCount; day++)
        {
            var sampleStartUtc = bucketStartUtc.AddDays(-day);
            var sampleEndUtc = bucketEndUtc.AddDays(-day);
            samples.Add(CalculateSampleOccupancy(historicalBookings, sampleStartUtc, sampleEndUtc, totalSpots));
        }

        return samples;
    }

    private static double CalculateSampleOccupancy(
        IReadOnlyCollection<Booking> bookings,
        DateTime sampleStartUtc,
        DateTime sampleEndUtc,
        int totalSpots)
    {
        var occupiedSpots = bookings
            .Where(booking => Overlaps(booking, sampleStartUtc, sampleEndUtc))
            .Sum(GetHistoricalBookingWeight);

        return Clamp(occupiedSpots / totalSpots, 0, 1);
    }

    private static double CalculateHistoricalRate(IReadOnlyCollection<double> weeklySamples, IReadOnlyCollection<double> dailySamples)
    {
        var weeklyRate = weeklySamples.Count > 0 ? weeklySamples.Average() : 0;
        var dailyRate = dailySamples.Count > 0 ? dailySamples.Average() : weeklyRate;

        return Clamp(weeklyRate * 0.65 + dailyRate * 0.35, 0, 1);
    }

    private static double CalculateConfidenceScore(
        int weeklySampleCount,
        int dailySampleCount,
        double deterministicBooked,
        double hoursAhead,
        ParkingAvailabilityModelPredictionDto? modelPrediction)
    {
        var historyCoverage = Math.Min(1.0, (weeklySampleCount + dailySampleCount) / 20.0);
        var deterministicSignal = deterministicBooked > 0 ? 0.95 : 0.6;
        var horizonFactor = hoursAhead <= 12 ? 1.0 : hoursAhead <= 24 ? 0.9 : 0.8;
        var modelSignal = modelPrediction?.UsedMachineLearning == true
            ? modelPrediction.ModelConfidenceScore
            : 0.55;

        return Math.Round(
            Clamp((historyCoverage * 0.35 + deterministicSignal * 0.25 + modelSignal * 0.4) * horizonFactor, 0.35, 0.98),
            3);
    }

    private static double ResolveBaselineOccupancyRate(
        ParkingSpace parking,
        double historicalRate,
        ParkingAvailabilityModelPredictionDto? modelPrediction)
    {
        if (!parking.IsActive)
        {
            return 0;
        }

        if (modelPrediction?.UsedMachineLearning != true)
        {
            return historicalRate;
        }

        return Clamp(modelPrediction.PredictedOccupancyRate, 0, 1);
    }

    private static string GetAvailabilityBand(int predictedAvailableSpots, int totalSpots, bool isListingActive, bool hasExistingDemand)
    {
        if (!isListingActive && !hasExistingDemand)
        {
            return "Listing inactive";
        }

        if (predictedAvailableSpots <= 0)
        {
            return "Full likely";
        }

        var freeRatio = predictedAvailableSpots / (double)totalSpots;
        if (freeRatio <= 0.15)
        {
            return "Very limited";
        }

        if (freeRatio <= 0.4)
        {
            return "Limited";
        }

        if (freeRatio <= 0.7)
        {
            return "Good";
        }

        return "High";
    }

    private static double GetFutureBookingWeight(Booking booking)
    {
        return booking.Status switch
        {
            BookingStatus.Confirmed => 1.0,
            BookingStatus.InProgress => 1.0,
            BookingStatus.AwaitingPayment => 0.85,
            BookingStatus.PendingExtension => 0.8,
            BookingStatus.AwaitingExtensionPayment => 0.9,
            BookingStatus.Pending => 0.55,
            _ => 0
        };
    }

    private static double GetHistoricalBookingWeight(Booking booking)
    {
        return booking.Status switch
        {
            BookingStatus.Cancelled => 0,
            BookingStatus.Rejected => 0,
            BookingStatus.Expired => 0,
            BookingStatus.Pending => 0.4,
            BookingStatus.AwaitingPayment => 0.5,
            _ => 1.0
        };
    }

    private static bool Overlaps(Booking booking, DateTime startUtc, DateTime endUtc)
    {
        return booking.StartDateTime < endUtc && booking.EndDateTime > startUtc;
    }

    private static double AverageRecent(IEnumerable<double> recentRates, int take)
    {
        var values = recentRates.TakeLast(take).ToList();
        if (values.Count == 0)
        {
            return 0;
        }

        return Clamp(values.Average(), 0, 1);
    }

    private static DateTime AlignToInterval(DateTime value, int intervalMinutes)
    {
        var alignedMinutes = value.Minute - (value.Minute % intervalMinutes);
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, alignedMinutes, 0, DateTimeKind.Utc);
    }

    private static int NormalizeHorizonHours(int horizonHours)
    {
        return Math.Clamp(horizonHours, 1, 48);
    }

    private static int NormalizeIntervalMinutes(int intervalMinutes)
    {
        return intervalMinutes switch
        {
            <= 15 => 15,
            <= 30 => 30,
            _ => 60
        };
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
