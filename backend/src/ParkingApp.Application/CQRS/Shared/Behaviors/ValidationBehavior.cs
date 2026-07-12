using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.CQRS.Behaviors;

/// <summary>
/// Runs FluentValidation for commands: validators registered for the command type
/// and for nested properties named <c>Dto</c> (or ending with <c>Dto</c>).
/// On failure, returns <see cref="ApiResponse{T}"/> with errors when possible.
/// </summary>
public sealed class ValidationBehavior : IDispatcherBehavior
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationBehavior(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public int Order => 10;

    public async Task<TResult> HandleAsync<TResult>(
        object request,
        bool isCommand,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        if (!isCommand)
            return await next();

        var errors = new List<string>();

        await CollectValidationErrorsAsync(request, request.GetType(), errors, cancellationToken);

        // Nested DTOs on commands (e.g. RegisterCommand.Dto, CreateParkingCommand.Dto)
        foreach (var prop in request.GetType().GetProperties())
        {
            if (prop.GetIndexParameters().Length > 0)
                continue;

            var name = prop.Name;
            if (!name.Equals("Dto", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = prop.GetValue(request);
            if (value == null)
                continue;

            await CollectValidationErrorsAsync(value, prop.PropertyType, errors, cancellationToken);
        }

        if (errors.Count == 0)
            return await next();

        if (TryCreateApiValidationFailure<TResult>(errors, out var failure))
            return failure;

        throw new ValidationException(string.Join("; ", errors));
    }

    private async Task CollectValidationErrorsAsync(
        object instance,
        Type instanceType,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(instanceType);
        var validators = _serviceProvider.GetServices(validatorType);
        foreach (var validatorObj in validators)
        {
            if (validatorObj is not IValidator validator)
                continue;

            var context = new ValidationContext<object>(instance);
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (result.IsValid)
                continue;

            foreach (var failure in result.Errors)
            {
                if (!string.IsNullOrWhiteSpace(failure.ErrorMessage))
                    errors.Add(failure.ErrorMessage);
            }
        }
    }

    private static bool TryCreateApiValidationFailure<TResult>(List<string> errors, out TResult result)
    {
        result = default!;
        var resultType = typeof(TResult);
        if (!resultType.IsGenericType || resultType.GetGenericTypeDefinition() != typeof(ApiResponse<>))
            return false;

        // ApiResponse<T>(bool Success, string? Message, T? Data, List<string>? Errors = null)
        var created = Activator.CreateInstance(
            resultType,
            false,
            "Validation failed",
            null,
            errors);

        if (created is not TResult typed)
            return false;

        result = typed;
        return true;
    }
}
