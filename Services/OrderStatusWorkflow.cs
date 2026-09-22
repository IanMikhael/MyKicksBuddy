namespace MyKicksBuddy.Services;

/// <summary>
/// Aturan transisi status pesanan yang diizinkan, supaya staff tidak bisa
/// melompat status secara sembarangan (mis. completed -> pending_payment).
/// </summary>
public static class OrderStatusWorkflow
{
    private static readonly Dictionary<string, string[]> AllowedNextStatuses = new()
    {
        ["pending_payment"] = new[] { "confirmed", "cancelled" },
        ["confirmed"] = new[] { "picked_up", "in_progress", "cancelled" },
        ["picked_up"] = new[] { "in_progress", "cancelled" },
        ["in_progress"] = new[] { "ready", "cancelled" },
        ["ready"] = new[] { "delivered", "completed", "cancelled" },
        ["delivered"] = new[] { "completed" },
        ["completed"] = Array.Empty<string>(),
        ["cancelled"] = Array.Empty<string>()
    };

    public static bool IsKnownStatus(string status) => AllowedNextStatuses.ContainsKey(status);

    /// <summary>
    /// Mengecek apakah transisi dari currentStatus ke nextStatus diizinkan.
    /// Selain status harus ada di daftar transisi yang valid, semua transisi
    /// kecuali ke "cancelled" mensyaratkan pesanan sudah payment_status = paid.
    /// </summary>
    public static bool CanTransition(string currentStatus, string paymentStatus, string nextStatus)
    {
        if (!AllowedNextStatuses.TryGetValue(currentStatus, out var allowedNext))
            return false;

        if (!allowedNext.Contains(nextStatus))
            return false;

        if (nextStatus == "cancelled")
            return true;

        return string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
    }
}
