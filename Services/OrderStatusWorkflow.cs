namespace MyKicksBuddy.Services;

public static class OrderStatusWorkflow
{
    public const string PendingPayment = "pending_payment";
    public const string WaitingApproval = "waiting_approval";
    public const string Approved = "approved";
    public const string WaitingPickup = "waiting_pickup";
    public const string PickedUp = "picked_up";
    public const string InProcess = "in_process";
    public const string ReadyToReturn = "ready_to_return";
    public const string Returned = "returned";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTransitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [PendingPayment] = [WaitingApproval, Cancelled], [WaitingApproval] = [Approved, Cancelled],
        [Approved] = [WaitingPickup], [WaitingPickup] = [PickedUp], [PickedUp] = [InProcess],
        [InProcess] = [ReadyToReturn], [ReadyToReturn] = [Returned], [Returned] = [Completed],
        [Completed] = [], [Cancelled] = []
    };
    public static bool IsKnown(string? status) => !string.IsNullOrWhiteSpace(status) && AllowedTransitions.ContainsKey(status);
    public static bool IsKnownStatus(string status) => IsKnown(status);
    public static IReadOnlyList<string> AllStatuses => AllowedTransitions.Keys.ToArray();
    public static bool CanTransition(string current, string next) => AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next, StringComparer.OrdinalIgnoreCase);
    public static bool CanTransition(string currentStatus, string paymentStatus, string nextStatus) => CanTransition(currentStatus, nextStatus) && (nextStatus == Cancelled || string.Equals(paymentStatus, PaymentStatusWorkflow.Paid, StringComparison.OrdinalIgnoreCase));
    public static IReadOnlyList<string> NextStatuses(string current) => AllowedTransitions.TryGetValue(current, out var allowed) ? allowed : [];
    public static IReadOnlyList<string> OperatorNextStatuses(string current, string paymentStatus) => NextStatuses(current).Where(next => next != WaitingApproval && (next == Cancelled ? paymentStatus != PaymentStatusWorkflow.Paid : paymentStatus == PaymentStatusWorkflow.Paid)).ToArray();
    public static string HistoryGroup(string? status) => status?.ToLowerInvariant() switch { Completed => "completed", Cancelled => "cancelled", _ => "active" };
    public static int ProgressStage(string? status) => status?.ToLowerInvariant() switch { PickedUp => 1, InProcess or ReadyToReturn or Returned => 2, Completed => 3, _ => 0 };
    public static string ProgressDescription(string? status, string? paymentStatus) => status?.ToLowerInvariant() switch
    {
        PendingPayment when paymentStatus == PaymentStatusWorkflow.Failed => "Pembayaran gagal. Pesanan belum diproses.", PendingPayment when paymentStatus == PaymentStatusWorkflow.Expired => "Waktu pembayaran habis. Pesanan belum diproses.",
        PendingPayment => "Menunggu pembayaran sebelum pesanan diproses.", WaitingApproval or Approved => "Pembayaran terverifikasi; pesanan menunggu persiapan.",
        WaitingPickup => "Sepatu menunggu penjemputan atau penyerahan.", PickedUp => "Sepatu telah dijemput atau diterima tim.",
        InProcess => "Sepatu sedang dirawat oleh tim MyKicksBuddy.", ReadyToReturn or Returned => "Perawatan selesai; sepatu dalam proses pengembalian.",
        Completed => "Pesanan selesai.", Cancelled => "Pesanan dibatalkan.", _ => "Pantau detail pesanan untuk pembaruan berikutnya."
    };
    public static string DisplayLabel(string? status, string? paymentStatus) => status?.ToLowerInvariant() switch { PendingPayment when paymentStatus == PaymentStatusWorkflow.Failed => "Pembayaran Gagal", PendingPayment when paymentStatus == PaymentStatusWorkflow.Expired => "Waktu Pembayaran Habis", _ => Label(status) };
    public static string Label(string? status) => status?.ToLowerInvariant() switch
    {
        PendingPayment => "Menunggu Pembayaran", WaitingApproval => "Menunggu Persetujuan", Approved => "Disetujui", WaitingPickup => "Menunggu Penjemputan",
        PickedUp => "Dijemput", InProcess => "Sedang Diproses", ReadyToReturn => "Siap Dikembalikan", Returned => "Sudah Dikembalikan",
        Completed => "Selesai", Cancelled => "Dibatalkan", _ => "Status Tidak Dikenal"
    };
}
