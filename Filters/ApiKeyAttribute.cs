using System.Security.Cryptography;
using System.Text;
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
            context.Result = new ObjectResult(new
            {
                message = "Chatbot API belum dikonfigurasi di server."
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || !IsMatch(providedKey.ToString(), expectedApiKey))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "API key tidak valid atau tidak ditemukan."
            });
            return;
        }

        await next();
    }

    private static bool IsMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}