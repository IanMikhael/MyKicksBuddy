using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MyKicksBuddy.Services;

public sealed class MidtransSnapClient : IMidtransSnapClient
{
    private readonly HttpClient _httpClient;
    private readonly MidtransOptions _options;

    public MidtransSnapClient(HttpClient httpClient, IOptions<MidtransOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SnapTransactionResponse> CreateTransactionAsync(
        string providerOrderId,
        long grossAmount,
        int expiryMinutes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerKey))
            throw new InvalidOperationException("Konfigurasi Midtrans:ServerKey belum diatur.");

        var endpoint = _options.IsProduction
            ? "https://app.midtrans.com/snap/v1/transactions"
            : "https://app.sandbox.midtrans.com/snap/v1/transactions";

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ServerKey}:"));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new SnapTransactionRequest
            {
                TransactionDetails = new SnapTransactionDetails
                {
                    OrderId = providerOrderId,
                    GrossAmount = grossAmount
                },
                Expiry = new SnapExpiry
                {
                    StartTime = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7))
                        .ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " +0700",
                    Unit = "minutes",
                    Duration = expiryMinutes
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Midtrans mengembalikan status HTTP {(int)response.StatusCode}.");

        var result = await response.Content.ReadFromJsonAsync<SnapTransactionResponseBody>(cancellationToken: cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.Token) || string.IsNullOrWhiteSpace(result.RedirectUrl))
            throw new InvalidOperationException("Respons pembuatan transaksi dari Midtrans tidak lengkap.");

        return new SnapTransactionResponse(result.Token, result.RedirectUrl);
    }

    public async Task<MidtransTransactionStatusResponse> GetTransactionStatusAsync(
        string providerOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerKey))
            throw new InvalidOperationException("Konfigurasi Midtrans:ServerKey belum diatur.");

        var apiBase = _options.IsProduction
            ? "https://api.midtrans.com"
            : "https://api.sandbox.midtrans.com";
        var endpoint = $"{apiBase}/v2/{Uri.EscapeDataString(providerOrderId)}/status";
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ServerKey}:"));
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Midtrans Status API mengembalikan status HTTP {(int)response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MidtransTransactionStatusBody>(cancellationToken: cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.OrderId) ||
            string.IsNullOrWhiteSpace(result.StatusCode) || string.IsNullOrWhiteSpace(result.GrossAmount) ||
            string.IsNullOrWhiteSpace(result.TransactionStatus))
            throw new InvalidOperationException("Respons Status API dari Midtrans tidak lengkap.");

        return new MidtransTransactionStatusResponse(
            result.OrderId,
            result.StatusCode,
            result.GrossAmount,
            result.TransactionStatus,
            result.TransactionId,
            result.PaymentType,
            result.FraudStatus);
    }

    private sealed class SnapTransactionRequest
    {
        [JsonPropertyName("transaction_details")]
        public SnapTransactionDetails TransactionDetails { get; set; } = new();

        [JsonPropertyName("expiry")]
        public SnapExpiry Expiry { get; set; } = new();
    }

    private sealed class SnapTransactionDetails
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("gross_amount")]
        public long GrossAmount { get; set; }
    }

    private sealed class SnapExpiry
    {
        [JsonPropertyName("start_time")]
        public string StartTime { get; set; } = string.Empty;

        [JsonPropertyName("unit")]
        public string Unit { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public int Duration { get; set; }
    }

    private sealed class SnapTransactionResponseBody
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("redirect_url")]
        public string RedirectUrl { get; set; } = string.Empty;
    }

    private sealed class MidtransTransactionStatusBody
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("status_code")]
        public string StatusCode { get; set; } = string.Empty;

        [JsonPropertyName("gross_amount")]
        public string GrossAmount { get; set; } = string.Empty;

        [JsonPropertyName("transaction_status")]
        public string TransactionStatus { get; set; } = string.Empty;

        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("payment_type")]
        public string? PaymentType { get; set; }

        [JsonPropertyName("fraud_status")]
        public string? FraudStatus { get; set; }
    }
}
