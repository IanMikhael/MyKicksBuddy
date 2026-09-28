namespace MyKicksBuddy.Services;

// The existing orders.payment_status value `unpaid` remains the pending value.
// Keeping it avoids reinterpreting rows already stored in the database.
public static class PaymentStatusWorkflow
{
    public const string Unpaid = "unpaid";
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Expired = "expired";

    public static bool IsKnown(string? status) => status is Unpaid or Paid or Failed or Expired;

    public static string Label(string? status) => status?.ToLowerInvariant() switch
    {
        Paid => "Berhasil",
        Failed => "Gagal",
        Expired => "Kedaluwarsa",
        _ => "Menunggu Konfirmasi"
    };
}
