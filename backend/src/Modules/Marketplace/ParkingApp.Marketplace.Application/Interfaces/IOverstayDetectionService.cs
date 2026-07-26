namespace ParkingApp.Marketplace.Application.Interfaces;

public interface IOverstayDetectionService
{
    Task<OverstayDetectionResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default);
}

public sealed record OverstayDetectionResult(
    int Notified,
    int Examined,
    int FeesAssessed = 0,
    int AutoCheckedOut = 0);
