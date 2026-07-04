using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public class ParkingAvailabilityMlModelService : IParkingAvailabilityModelService
{
    private const int TrainingLookbackDays = 84;
    private const int MinimumTrainingRowCount = 250;
    private const int WeeklySampleCount = 6;
    private const int DailySampleCount = 14;

    private static readonly MLContext MlContext = new(seed: 42);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ParkingAvailabilityMlModelService> _logger;

    public ParkingAvailabilityMlModelService(
        IUnitOfWork unitOfWork,
        IMemoryCache memoryCache,
        ILogger<ParkingAvailabilityMlModelService> logger)
    {
        _unitOfWork = unitOfWork;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<ParkingAvailabilityModelPredictionDto?> PredictOccupancyAsync(
        ParkingAvailabilityModelInputDto input,
        int intervalMinutes,
        CancellationToken cancellationToken = default)
    {
        var normalizedIntervalMinutes = NormalizeIntervalMinutes(intervalMinutes);
        var modelArtifact = await GetOrTrainModelAsync(normalizedIntervalMinutes, cancellationToken);
        if (modelArtifact == null)
        {
            return null;
        }

        var predictionEngine = MlContext.Model.CreatePredictionEngine<ParkingAvailabilityTrainingRow, ParkingAvailabilityPredictionRow>(modelArtifact.Model);
        var prediction = predictionEngine.Predict(MapInferenceInput(input));
        var predictedRate = Clamp(prediction.Score, 0, input.IsListingActive ? 1 : 0);

        return new ParkingAvailabilityModelPredictionDto(
            predictedRate,
            modelArtifact.ModelConfidenceScore,
            modelArtifact.TrainingRowCount,
            true);
    }

    private async Task<ModelArtifact?> GetOrTrainModelAsync(int intervalMinutes, CancellationToken cancellationToken)
    {
        var cacheKey = $"parking-availability-ml-model:{intervalMinutes}";
        if (_memoryCache.TryGetValue<ModelArtifact>(cacheKey, out var cachedArtifact))
        {
            return cachedArtifact;
        }

        var artifact = await TrainModelAsync(intervalMinutes, cancellationToken);
        if (artifact == null)
        {
            _logger.LogInformation(
                "Skipping ML.NET availability model for interval {IntervalMinutes} because there was not enough training data",
                intervalMinutes);
            return null;
        }

        _memoryCache.Set(cacheKey, artifact, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        });

        return artifact;
    }

    private async Task<ModelArtifact?> TrainModelAsync(int intervalMinutes, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var alignedNowUtc = AlignToInterval(nowUtc, intervalMinutes);
        var historyStartUtc = alignedNowUtc.AddDays(-TrainingLookbackDays);

        var parkingSpaces = await _unitOfWork.ParkingSpaces.Query()
            .AsNoTracking()
            .Where(parking => parking.TotalSpots > 0)
            .Select(parking => new ParkingSnapshot(
                parking.Id,
                parking.TotalSpots,
                parking.HourlyRate,
                parking.ParkingType,
                parking.Is24Hours,
                parking.IsActive,
                parking.AverageRating,
                parking.TotalReviews))
            .ToListAsync(cancellationToken);

        if (parkingSpaces.Count == 0)
        {
            return null;
        }

        var parkingIds = parkingSpaces.Select(parking => parking.Id).ToList();
        var bookings = await _unitOfWork.Bookings.Query()
            .AsNoTracking()
            .Where(booking =>
                parkingIds.Contains(booking.ParkingSpaceId) &&
                booking.StartDateTime < alignedNowUtc &&
                booking.EndDateTime > historyStartUtc &&
                booking.Status != BookingStatus.Cancelled &&
                booking.Status != BookingStatus.Rejected &&
                booking.Status != BookingStatus.Expired)
            .ToListAsync(cancellationToken);

        var trainingRows = BuildTrainingRows(
            parkingSpaces,
            bookings,
            historyStartUtc,
            alignedNowUtc,
            intervalMinutes);

        if (trainingRows.Count < MinimumTrainingRowCount)
        {
            return null;
        }

        var trainingData = MlContext.Data.LoadFromEnumerable(trainingRows);
        var dataSplit = MlContext.Data.TrainTestSplit(trainingData, testFraction: 0.2, seed: 42);

        const string dayOfWeekFeatures = "DayOfWeekFeatures";
        const string hourBucketFeatures = "HourBucketFeatures";
        const string monthFeatures = "MonthFeatures";
        const string parkingTypeFeatures = "ParkingTypeFeatures";

        var pipeline = MlContext.Transforms.Categorical.OneHotEncoding(new[]
            {
                new InputOutputColumnPair(dayOfWeekFeatures, nameof(ParkingAvailabilityTrainingRow.DayOfWeek)),
                new InputOutputColumnPair(hourBucketFeatures, nameof(ParkingAvailabilityTrainingRow.HourBucket)),
                new InputOutputColumnPair(monthFeatures, nameof(ParkingAvailabilityTrainingRow.Month)),
                new InputOutputColumnPair(parkingTypeFeatures, nameof(ParkingAvailabilityTrainingRow.ParkingType))
            })
            .Append(MlContext.Transforms.Concatenate(
                "RawFeatures",
                nameof(ParkingAvailabilityTrainingRow.TotalSpots),
                nameof(ParkingAvailabilityTrainingRow.HourlyRate),
                nameof(ParkingAvailabilityTrainingRow.AverageRating),
                nameof(ParkingAvailabilityTrainingRow.TotalReviews),
                nameof(ParkingAvailabilityTrainingRow.WeeklyOccupancyRate),
                nameof(ParkingAvailabilityTrainingRow.DailyOccupancyRate),
                nameof(ParkingAvailabilityTrainingRow.Recent1OccupancyRate),
                nameof(ParkingAvailabilityTrainingRow.Recent3OccupancyRate),
                nameof(ParkingAvailabilityTrainingRow.Recent6OccupancyRate),
                nameof(ParkingAvailabilityTrainingRow.IsWeekend),
                nameof(ParkingAvailabilityTrainingRow.Is24Hours),
                nameof(ParkingAvailabilityTrainingRow.IsListingActive),
                dayOfWeekFeatures,
                hourBucketFeatures,
                monthFeatures,
                parkingTypeFeatures))
            .Append(MlContext.Transforms.NormalizeMeanVariance("Features", "RawFeatures"))
            .Append(MlContext.Regression.Trainers.Sdca(
                labelColumnName: nameof(ParkingAvailabilityTrainingRow.Label),
                featureColumnName: "Features",
                maximumNumberOfIterations: 200));

        var model = pipeline.Fit(dataSplit.TrainSet);
        var evaluationData = model.Transform(dataSplit.TestSet);
        var metrics = MlContext.Regression.Evaluate(
            evaluationData,
            labelColumnName: nameof(ParkingAvailabilityTrainingRow.Label),
            scoreColumnName: "Score");

        var modelConfidenceScore = CalculateModelConfidence(metrics, trainingRows.Count);
        _logger.LogInformation(
            "Trained parking availability ML.NET model for interval {IntervalMinutes} with {TrainingRows} rows and confidence {ConfidenceScore}",
            intervalMinutes,
            trainingRows.Count,
            modelConfidenceScore);

        return new ModelArtifact(model, trainingRows.Count, modelConfidenceScore);
    }

    private static List<ParkingAvailabilityTrainingRow> BuildTrainingRows(
        IReadOnlyCollection<ParkingSnapshot> parkingSpaces,
        IReadOnlyCollection<Booking> bookings,
        DateTime historyStartUtc,
        DateTime alignedNowUtc,
        int intervalMinutes)
    {
        var bucketCount = (int)Math.Max(1, Math.Ceiling((alignedNowUtc - historyStartUtc).TotalMinutes / intervalMinutes));
        var bucketsPerDay = Math.Max(1, (24 * 60) / intervalMinutes);
        var bookingsByParkingId = bookings
            .GroupBy(booking => booking.ParkingSpaceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var rows = new List<ParkingAvailabilityTrainingRow>(parkingSpaces.Count * Math.Min(bucketCount, 1024));

        foreach (var parking in parkingSpaces)
        {
            var parkingBookings = bookingsByParkingId.GetValueOrDefault(parking.Id) ?? new List<Booking>();
            var occupancyRates = BuildOccupancyRates(
                parkingBookings,
                historyStartUtc,
                bucketCount,
                intervalMinutes,
                parking.TotalSpots);

            for (var index = 1; index < bucketCount; index++)
            {
                var bucketStartUtc = historyStartUtc.AddMinutes(index * intervalMinutes);
                var weeklyAverage = AverageSameSlotHistory(occupancyRates, index, bucketsPerDay * 7, WeeklySampleCount);
                var dailyAverage = AverageSameSlotHistory(occupancyRates, index, bucketsPerDay, DailySampleCount);
                var recent1 = AverageRecentHistory(occupancyRates, index, 1);
                var recent3 = AverageRecentHistory(occupancyRates, index, 3);
                var recent6 = AverageRecentHistory(occupancyRates, index, 6);

                rows.Add(new ParkingAvailabilityTrainingRow
                {
                    Label = (float)occupancyRates[index],
                    TotalSpots = parking.TotalSpots,
                    HourlyRate = (float)parking.HourlyRate,
                    AverageRating = (float)parking.AverageRating,
                    TotalReviews = parking.TotalReviews,
                    WeeklyOccupancyRate = (float)weeklyAverage,
                    DailyOccupancyRate = (float)dailyAverage,
                    Recent1OccupancyRate = (float)recent1,
                    Recent3OccupancyRate = (float)recent3,
                    Recent6OccupancyRate = (float)recent6,
                    IsWeekend = IsWeekend(bucketStartUtc) ? 1f : 0f,
                    Is24Hours = parking.Is24Hours ? 1f : 0f,
                    IsListingActive = parking.IsActive ? 1f : 0f,
                    DayOfWeek = bucketStartUtc.DayOfWeek.ToString(),
                    HourBucket = bucketStartUtc.ToString("HH"),
                    Month = bucketStartUtc.ToString("MM"),
                    ParkingType = parking.ParkingType.ToString()
                });
            }
        }

        return rows;
    }

    private static double[] BuildOccupancyRates(
        IReadOnlyCollection<Booking> bookings,
        DateTime historyStartUtc,
        int bucketCount,
        int intervalMinutes,
        int totalSpots)
    {
        var differenceArray = new double[bucketCount + 1];
        foreach (var booking in bookings)
        {
            var weight = GetHistoricalBookingWeight(booking);
            if (weight <= 0)
            {
                continue;
            }

            var startOffset = (booking.StartDateTime - historyStartUtc).TotalMinutes / intervalMinutes;
            var endOffset = (booking.EndDateTime - historyStartUtc).TotalMinutes / intervalMinutes;
            var startIndex = Math.Max(0, (int)Math.Floor(startOffset));
            var endIndex = Math.Min(bucketCount, (int)Math.Ceiling(endOffset));

            if (startIndex >= bucketCount || endIndex <= 0 || startIndex >= endIndex)
            {
                continue;
            }

            differenceArray[startIndex] += weight;
            differenceArray[endIndex] -= weight;
        }

        var running = 0d;
        var occupancyRates = new double[bucketCount];
        for (var index = 0; index < bucketCount; index++)
        {
            running += differenceArray[index];
            occupancyRates[index] = Clamp(running / totalSpots, 0, 1);
        }

        return occupancyRates;
    }

    private static ParkingAvailabilityTrainingRow MapInferenceInput(ParkingAvailabilityModelInputDto input)
    {
        var parkingTypeName = Enum.IsDefined(typeof(ParkingType), input.ParkingType)
            ? ((ParkingType)input.ParkingType).ToString()
            : ParkingType.Open.ToString();

        return new ParkingAvailabilityTrainingRow
        {
            TotalSpots = input.TotalSpots,
            HourlyRate = (float)input.HourlyRate,
            AverageRating = (float)input.AverageRating,
            TotalReviews = input.TotalReviews,
            WeeklyOccupancyRate = (float)input.WeeklyOccupancyRate,
            DailyOccupancyRate = (float)input.DailyOccupancyRate,
            Recent1OccupancyRate = (float)input.Recent1OccupancyRate,
            Recent3OccupancyRate = (float)input.Recent3OccupancyRate,
            Recent6OccupancyRate = (float)input.Recent6OccupancyRate,
            IsWeekend = IsWeekend(input.BucketStartUtc) ? 1f : 0f,
            Is24Hours = input.Is24Hours ? 1f : 0f,
            IsListingActive = input.IsListingActive ? 1f : 0f,
            DayOfWeek = input.BucketStartUtc.DayOfWeek.ToString(),
            HourBucket = input.BucketStartUtc.ToString("HH"),
            Month = input.BucketStartUtc.ToString("MM"),
            ParkingType = parkingTypeName
        };
    }

    private static double AverageSameSlotHistory(double[] occupancyRates, int currentIndex, int step, int sampleCount)
    {
        var samples = new List<double>(sampleCount);
        for (var sample = 1; sample <= sampleCount; sample++)
        {
            var index = currentIndex - (step * sample);
            if (index < 0)
            {
                break;
            }

            samples.Add(occupancyRates[index]);
        }

        return samples.Count == 0 ? 0 : Clamp(samples.Average(), 0, 1);
    }

    private static double AverageRecentHistory(double[] occupancyRates, int currentIndex, int take)
    {
        var startIndex = Math.Max(0, currentIndex - take);
        var count = currentIndex - startIndex;
        if (count <= 0)
        {
            return 0;
        }

        return Clamp(occupancyRates.Skip(startIndex).Take(count).Average(), 0, 1);
    }

    private static double CalculateModelConfidence(RegressionMetrics metrics, int trainingRowCount)
    {
        var sampleSignal = Clamp(trainingRowCount / 5000d, 0.35, 1d);
        var errorSignal = Clamp(1d - metrics.RootMeanSquaredError, 0.2, 0.95);
        var rSquaredSignal = double.IsNaN(metrics.RSquared)
            ? 0.4
            : Clamp((metrics.RSquared + 1d) / 2d, 0.2, 0.95);

        return Math.Round(Clamp(sampleSignal * 0.25 + errorSignal * 0.4 + rSquaredSignal * 0.35, 0.35, 0.97), 3);
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

    private static bool IsWeekend(DateTime value)
    {
        return value.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private static DateTime AlignToInterval(DateTime value, int intervalMinutes)
    {
        var alignedMinutes = value.Minute - (value.Minute % intervalMinutes);
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, alignedMinutes, 0, DateTimeKind.Utc);
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

    private sealed record ParkingSnapshot(
        Guid Id,
        int TotalSpots,
        decimal HourlyRate,
        ParkingType ParkingType,
        bool Is24Hours,
        bool IsActive,
        double AverageRating,
        int TotalReviews);

    private sealed record ModelArtifact(
        ITransformer Model,
        int TrainingRowCount,
        double ModelConfidenceScore);

    private sealed class ParkingAvailabilityTrainingRow
    {
        public float Label { get; set; }
        public float TotalSpots { get; set; }
        public float HourlyRate { get; set; }
        public float AverageRating { get; set; }
        public float TotalReviews { get; set; }
        public float WeeklyOccupancyRate { get; set; }
        public float DailyOccupancyRate { get; set; }
        public float Recent1OccupancyRate { get; set; }
        public float Recent3OccupancyRate { get; set; }
        public float Recent6OccupancyRate { get; set; }
        public float IsWeekend { get; set; }
        public float Is24Hours { get; set; }
        public float IsListingActive { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public string HourBucket { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;
        public string ParkingType { get; set; } = string.Empty;
    }

    private sealed class ParkingAvailabilityPredictionRow
    {
        public float Score { get; set; }
    }
}
