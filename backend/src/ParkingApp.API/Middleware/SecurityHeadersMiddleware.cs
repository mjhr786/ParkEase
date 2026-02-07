namespace ParkingApp.API.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent clickjacking attacks
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        // Enable XSS protection
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        
        // Content Security Policy - adjust as needed for your app
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; img-src 'self' data: blob:; script-src 'self'; style-src 'self' 'unsafe-inline';");
        
        // Referrer Policy
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Permissions Policy (formerly Feature Policy)
        context.Response.Headers.Append("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=()");
        
        // HSTS - only add in production with HTTPS
        if (!context.Request.IsHttps)
        {
            // Uncomment in production with HTTPS
            // context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        await _next(context);
    }
}
