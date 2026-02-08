namespace ParkingApp.BuildingBlocks.Exceptions;

/// <summary>
/// Base exception for all domain-specific exceptions
/// </summary>
public abstract class DomainException : Exception
{
    public string ErrorCode { get; }
    
    protected DomainException(string message, string errorCode = "DOMAIN_ERROR") 
        : base(message)
    {
        ErrorCode = errorCode;
    }
    
    protected DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR") 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Thrown when a requested resource is not found
/// </summary>
public class NotFoundException : DomainException
{
    public string ResourceType { get; }
    public object? ResourceId { get; }
    
    public NotFoundException(string resourceType, object? resourceId = null)
        : base($"{resourceType} not found" + (resourceId != null ? $" (Id: {resourceId})" : ""), "NOT_FOUND")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
    
    public static NotFoundException For<T>(object? id = null) => 
        new(typeof(T).Name, id);
}

/// <summary>
/// Thrown when a validation rule fails
/// </summary>
public class ValidationException : DomainException
{
    public Dictionary<string, string[]> Errors { get; }
    
    public ValidationException(string message) 
        : base(message, "VALIDATION_ERROR")
    {
        Errors = new Dictionary<string, string[]>();
    }
    
    public ValidationException(Dictionary<string, string[]> errors)
        : base("One or more validation errors occurred", "VALIDATION_ERROR")
    {
        Errors = errors;
    }
    
    public ValidationException(string field, string error)
        : base($"Validation failed for {field}: {error}", "VALIDATION_ERROR")
    {
        Errors = new Dictionary<string, string[]> { { field, new[] { error } } };
    }
}

/// <summary>
/// Thrown when user is not authorized to perform an action
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "You are not authorized to perform this action")
        : base(message, "UNAUTHORIZED")
    {
    }
}

/// <summary>
/// Thrown when user is authenticated but lacks permissions
/// </summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "You do not have permission to access this resource")
        : base(message, "FORBIDDEN")
    {
    }
}

/// <summary>
/// Thrown when a business rule is violated
/// </summary>
public class BusinessRuleException : DomainException
{
    public string RuleName { get; }
    
    public BusinessRuleException(string ruleName, string message)
        : base(message, "BUSINESS_RULE_VIOLATION")
    {
        RuleName = ruleName;
    }
}

/// <summary>
/// Thrown when there's a conflict with existing data
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message)
        : base(message, "CONFLICT")
    {
    }
}

/// <summary>
/// Thrown when an external service fails
/// </summary>
public class ExternalServiceException : DomainException
{
    public string ServiceName { get; }
    
    public ExternalServiceException(string serviceName, string message, Exception? innerException = null)
        : base($"{serviceName}: {message}", innerException!, "EXTERNAL_SERVICE_ERROR")
    {
        ServiceName = serviceName;
    }
}
