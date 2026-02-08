namespace ParkingApp.BuildingBlocks.Common;

/// <summary>
/// Generic result type for operations that can succeed or fail
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public string? ErrorCode { get; }
    
    protected Result(bool isSuccess, string? error = null, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }
    
    public static Result Success() => new(true);
    public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);
    
    public static Result<T> Success<T>(T value) => new(value, true);
    public static Result<T> Failure<T>(string error, string? errorCode = null) => new(default, false, error, errorCode);
}

/// <summary>
/// Generic result type with value for operations that can succeed or fail
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }
    
    internal Result(T? value, bool isSuccess, string? error = null, string? errorCode = null) 
        : base(isSuccess, error, errorCode)
    {
        Value = value;
    }
    
    public static implicit operator Result<T>(T value) => new(value, true);
}

/// <summary>
/// Extension methods for Result types
/// </summary>
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> mapper)
    {
        return result.IsSuccess 
            ? Result.Success(mapper(result.Value!)) 
            : Result.Failure<TOut>(result.Error!, result.ErrorCode);
    }
    
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> mapper)
    {
        return result.IsSuccess 
            ? Result.Success(await mapper(result.Value!)) 
            : Result.Failure<TOut>(result.Error!, result.ErrorCode);
    }
    
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> binder)
    {
        return result.IsSuccess 
            ? binder(result.Value!) 
            : Result.Failure<TOut>(result.Error!, result.ErrorCode);
    }
    
    public static T Match<T>(this Result result, Func<T> onSuccess, Func<string, T> onFailure)
    {
        return result.IsSuccess ? onSuccess() : onFailure(result.Error!);
    }
    
    public static T Match<TValue, T>(this Result<TValue> result, Func<TValue, T> onSuccess, Func<string, T> onFailure)
    {
        return result.IsSuccess ? onSuccess(result.Value!) : onFailure(result.Error!);
    }
}
