namespace MyKicksBuddy.Services;

public static class StaffPaymentPresentation
{
    public static string Label(string? status, bool hasProviderRecord) => status switch
    {
        PaymentStatusWorkflow.Paid => "Terbayar",
        PaymentStatusWorkflow.Failed => "Gagal",
        PaymentStatusWorkflow.Expired => "Kedaluwarsa",
        PaymentStatusWorkflow.Unpaid when hasProviderRecord => "Menunggu pembayaran",
        _ => "Belum dibayar"
    };

    public static string Tone(string? status) => status switch
    {
        PaymentStatusWorkflow.Paid => "is-success",
        PaymentStatusWorkflow.Failed or PaymentStatusWorkflow.Expired => "is-danger",
        PaymentStatusWorkflow.Unpaid => "is-warning",
        _ => "is-neutral"
    };
}
