using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyKicksBuddy.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string ApiKeyHeader = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>();

        var expectedApiKey = configuration.GetValue<string>("ChatbotApiKey");

        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            await next();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || providedKey.ToString() != expectedApiKey)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "API key tidak valid atau tidak ditemukan."
            });
            return;
        }

        await next();
    }
}