namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>
/// Optional counters for Corporate SSO (PR9). Default is no-op until a metrics host exists.
/// Do not block GA on Prometheus/OTel — Serilog remains the MVP signal path.
/// </summary>
public interface ICorporateSsoMetrics
{
    void RecordStart(string result);
    void RecordCallback(string result, string? errorCode = null);
    void RecordComplete(string result, string? errorCode = null);
    void RecordTokenExchangeError(string? idpHost = null);
}

/// <summary>No-op metrics sink for MVP / environments without a metrics pipeline.</summary>
public sealed class NoOpCorporateSsoMetrics : ICorporateSsoMetrics
{
    public static readonly NoOpCorporateSsoMetrics Instance = new();

    public void RecordStart(string result) { }
    public void RecordCallback(string result, string? errorCode = null) { }
    public void RecordComplete(string result, string? errorCode = null) { }
    public void RecordTokenExchangeError(string? idpHost = null) { }
}
