using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ParkingApp.API.Filters;

public class ApiKeyAuthFilter : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var configuration = context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        var expectedApiKey = configuration?.GetValue<string>("InternalJobs:ApiKey");

        if (string.IsNullOrEmpty(expectedApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var extractedBytes = Encoding.UTF8.GetBytes(extractedApiKey.ToString());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedApiKey);

        if (extractedBytes.Length != expectedBytes.Length || !CryptographicOperations.FixedTimeEquals(extractedBytes, expectedBytes))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }
}
