using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MyKicksBuddy.Services;

public sealed class MidtransCoreClient : IMidtransCoreClient
{
    private readonly HttpClient _httpClient;
    private readonly MidtransOptions _options;

    public MidtransCoreClient(HttpClient httpClient, IOptions<MidtransOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<CoreChargeResponse> ChargeBankTransferAsync(
        string orderId,
        long grossAmount,
        string bank,
        CancellationToken cancellationToken = default)
    {
        return await ChargeAsync(new
        {
            payment_type = "bank_transfer",
            transaction_details = new { order_id = orderId, gross_amount = grossAmount },
            bank_transfer = new { bank }
        }, orderId, cancellationToken);
    }

    public Task<CoreChargeResponse> ChargeQrisAsync(string orderId, long grossAmount, CancellationToken cancellationToken = default) =>
        ChargeAsync(new
        {
            payment_type = "qris",
            transaction_details = new { order_id = orderId, gross_amount = grossAmount },
            qris = new { acquirer = "gopay" }
        }, orderId, cancellationToken);

    public Task<CoreChargeResponse> ChargeGoPayAsync(string orderId, long grossAmount, CancellationToken cancellationToken = default) =>
        ChargeAsync(new
        {
            payment_type = "gopay",
            transaction_details = new { order_id = orderId, gross_amount = grossAmount },
            gopay = new { enable_callback = false }
        }, orderId, cancellationToken);

    private async Task<CoreChargeResponse> ChargeAsync(object payload, string fallbackOrderId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "/v2/charge");
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CoreChargeBody>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || body is null)
            throw new HttpRequestException($"Midtrans Core API mengembalikan status HTTP {(int)response.StatusCode}.");
        if (!IsProviderSuccess(body.StatusCode))
            throw new HttpRequestException(
                $"Midtrans Core API menolak transaksi ({body.StatusCode ?? "tanpa kode"}): {body.StatusMessage ?? "tanpa pesan"}");
        if (string.IsNullOrWhiteSpace(body.TransactionStatus))
            throw new HttpRequestException("Midtrans Core API tidak mengembalikan status transaksi.");

        return new CoreChargeResponse(
            body.OrderId ?? fallbackOrderId,
            body.StatusCode ?? ((int)response.StatusCode).ToString(),
            body.StatusMessage ?? string.Empty,
            body.TransactionStatus ?? string.Empty,
            body.TransactionId,
            body.PaymentType,
            body.FraudStatus,
            body.VaNumbers?.FirstOrDefault()?.Bank,
            body.VaNumbers?.FirstOrDefault()?.Number,
            body.Actions?.Where(action => !string.IsNullOrWhiteSpace(action.Name) && !string.IsNullOrWhiteSpace(action.Url))
                .Select(action => new CoreAction(action.Name!, action.Url!)).ToArray() ?? []);
    }

    public async Task<(byte[] Content, string ContentType)> GetQrCodeAsync(
        string transactionId,
        string paymentType,
        CancellationToken cancellationToken = default)
    {
        var path = paymentType.ToLowerInvariant() switch
        {
            "qris" => $"/v2/qris/{Uri.EscapeDataString(transactionId)}/qr-code",
            "gopay" => $"/v2/gopay/{Uri.EscapeDataString(transactionId)}/qr-code",
            _ => throw new ArgumentException("QR hanya tersedia untuk QRIS atau GoPay.", nameof(paymentType))
        };
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Midtrans QR API mengembalikan status HTTP {(int)response.StatusCode}.");
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return (content, response.Content.Headers.ContentType?.MediaType ?? "image/png");
    }

    public async Task<CoreTransactionStatusResponse> GetTransactionStatusAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v2/{Uri.EscapeDataString(orderId)}/status");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CoreStatusBody>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || body is null)
            throw new HttpRequestException($"Respons status Midtrans tidak dapat dibaca (HTTP {(int)response.StatusCode}).");
        if (!IsProviderSuccess(body.StatusCode))
            throw new HttpRequestException(
                $"Midtrans menolak pemeriksaan status ({body.StatusCode ?? "tanpa kode"}): {body.StatusMessage ?? "tanpa pesan"}");

        return new CoreTransactionStatusResponse(
            body.OrderId ?? orderId,
            body.StatusCode ?? ((int)response.StatusCode).ToString(),
            body.StatusMessage ?? string.Empty,
            body.TransactionStatus ?? string.Empty,
            body.GrossAmount ?? string.Empty,
            body.TransactionId,
            body.PaymentType,
            body.FraudStatus);
    }

    public async Task<CoreCancelResponse> CancelTransactionAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/v2/{Uri.EscapeDataString(orderId)}/cancel");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<CoreCancelBody>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || body is null)
            throw new HttpRequestException($"Respons cancel Midtrans tidak dapat dibaca (HTTP {(int)response.StatusCode}).");
        if (!IsProviderSuccess(body.StatusCode))
            throw new HttpRequestException(
                $"Midtrans menolak pembatalan ({body.StatusCode ?? "tanpa kode"}): {body.StatusMessage ?? "tanpa pesan"}");

        return new CoreCancelResponse(
            body.OrderId ?? orderId,
            body.StatusCode ?? ((int)response.StatusCode).ToString(),
            body.StatusMessage ?? string.Empty,
            body.TransactionStatus ?? string.Empty);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerKey))
            throw new InvalidOperationException("Konfigurasi Midtrans:ServerKey belum diatur.");

        var baseUrl = _options.IsProduction ? "https://api.midtrans.com" : "https://api.sandbox.midtrans.com";
        var request = new HttpRequestMessage(method, baseUrl + path);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ServerKey}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static bool IsProviderSuccess(string? statusCode) =>
        statusCode is { Length: > 0 } && statusCode[0] == '2';

    private sealed class CoreChargeBody
    {
        [JsonPropertyName("order_id")] public string? OrderId { get; set; }
        [JsonPropertyName("status_code")] public string? StatusCode { get; set; }
        [JsonPropertyName("status_message")] public string? StatusMessage { get; set; }
        [JsonPropertyName("transaction_status")] public string? TransactionStatus { get; set; }
        [JsonPropertyName("transaction_id")] public string? TransactionId { get; set; }
        [JsonPropertyName("payment_type")] public string? PaymentType { get; set; }
        [JsonPropertyName("fraud_status")] public string? FraudStatus { get; set; }
        [JsonPropertyName("va_numbers")] public List<VaNumber>? VaNumbers { get; set; }
        [JsonPropertyName("actions")] public List<ActionBody>? Actions { get; set; }
    }

    private sealed class VaNumber
    {
        [JsonPropertyName("bank")] public string? Bank { get; set; }
        [JsonPropertyName("va_number")] public string? Number { get; set; }
    }

    private sealed class ActionBody
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    private sealed class CoreStatusBody
    {
        [JsonPropertyName("order_id")] public string? OrderId { get; set; }
        [JsonPropertyName("status_code")] public string? StatusCode { get; set; }
        [JsonPropertyName("status_message")] public string? StatusMessage { get; set; }
        [JsonPropertyName("transaction_status")] public string? TransactionStatus { get; set; }
        [JsonPropertyName("gross_amount")] public string? GrossAmount { get; set; }
        [JsonPropertyName("transaction_id")] public string? TransactionId { get; set; }
        [JsonPropertyName("payment_type")] public string? PaymentType { get; set; }
        [JsonPropertyName("fraud_status")] public string? FraudStatus { get; set; }
    }

    private sealed class CoreCancelBody
    {
        [JsonPropertyName("order_id")] public string? OrderId { get; set; }
        [JsonPropertyName("status_code")] public string? StatusCode { get; set; }
        [JsonPropertyName("status_message")] public string? StatusMessage { get; set; }
        [JsonPropertyName("transaction_status")] public string? TransactionStatus { get; set; }
    }
}
