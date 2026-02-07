namespace ParkingApp.Application.CQRS;

/// <summary>
/// Marker interface for commands that modify state
/// </summary>
public interface ICommand<TResult>
{
}

/// <summary>
/// Marker interface for queries that only read data
/// </summary>
public interface IQuery<TResult>
{
}
