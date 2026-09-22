using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "kasir")]
[Route("staff")]
public sealed class StaffController : Controller
{
    private readonly IOrderService _orders;
    private readonly IStaffWorkspaceRepository _workspace;
    private readonly IUserRepository _users;

    public StaffController(IOrderService orders, IStaffWorkspaceRepository workspace, IUserRepository users) =>
        (_orders, _workspace, _users) = (orders, workspace, users);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User.IsInRole("kasir"))
        {
            var validId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId);
            var staff = validId ? await _users.GetByIdAsync(staffId) : null;
            if (staff is null || !staff.IsActive || staff.Role != "kasir")
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Result = RedirectToAction(nameof(Login));
                return;
            }
        }
        await next();
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login() => User.IsInRole("kasir") ? RedirectToAction(nameof(Index)) : View();

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var attention = await _workspace.GetOrdersAsync(null, null, PaymentStatusWorkflow.Paid, false, 1, 20);
        var pickups = await _workspace.GetOrdersAsync(null, "waiting_pickup", null, true, 1, 3);
        var payments = await _workspace.GetPaymentsAsync(null, null, 1, 20);
        return View(new StaffDashboardPageViewModel
        {
            Counts = await _workspace.GetCountsAsync(),
            Attention = attention.Rows.Where(x => OrderStatusWorkflow.OperatorNextStatuses(x.Status, x.PaymentStatus)
                .Any(next => next != OrderStatusWorkflow.Cancelled)).Take(5).ToArray(),
            Pickups = pickups.Rows,
            RecentPayments = payments.Rows.Where(x => x.Provider is not null).Take(3).ToArray()
        });
    }

    [HttpGet("orders")]
    public async Task<IActionResult> Orders(string? q, string? status, string? paymentStatus, int page = 1)
    {
        q = CleanQuery(q);
        status = OrderStatusWorkflow.IsKnown(status) ? status : null;
        paymentStatus = PaymentStatusWorkflow.IsKnown(paymentStatus) ? paymentStatus : null;
        page = Math.Clamp(page, 1, 100000);
        var data = await _workspace.GetOrdersAsync(q, status, paymentStatus, false, page, 20);
        return View(new StaffOrdersPageViewModel { Orders = data.Rows, Total = data.Total, Query = q, Status = status, PaymentStatus = paymentStatus, Page = page,
            Counts = await _workspace.GetCountsAsync(), SelectedDetail = data.Rows.Count > 0 ? await _orders.GetOrderDetailForStaffAsync(data.Rows[0].Id) : null });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q)
    {
        q = CleanQuery(q);
        var data = q is null ? (Rows: (IReadOnlyList<StaffOrderRow>)[], Total: 0)
            : await _workspace.GetOrdersAsync(q, null, null, false, 1, 20);
        return View(new StaffOrdersPageViewModel { Orders = data.Rows, Total = data.Total, Query = q, IsSearch = true });
    }

    [HttpGet("pickups")]
    public async Task<IActionResult> Pickups(string? q, int page = 1)
    {
        q = CleanQuery(q);
        page = Math.Clamp(page, 1, 100000);
        var data = await _workspace.GetOrdersAsync(q, null, null, true, page, 20);
        return View(new StaffOrdersPageViewModel { Orders = data.Rows, Total = data.Total, Query = q, Page = page, IsPickup = true });
    }

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var order = await _orders.GetOrderDetailForStaffAsync(id);
        if (order is null) return NotFound();
        return View(new OrderDetailPageViewModel
        {
            Order = order,
            Logs = (await _orders.GetOrderLogsAsync(id)).ToList(),
            AllowedStatuses = OrderStatusWorkflow.OperatorNextStatuses(order.Status, order.PaymentStatus)
        });
    }

    [HttpPost("orders/{id:long}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(long id, string status, string? notes)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)) return Forbid();
        var result = await _orders.UpdateStatusAsync(id, status, staffId, "kasir", notes);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Status pesanan diperbarui dan dicatat." : result.Error;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications() => View(await _workspace.GetActivityAsync(30));

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId)) return Forbid();
        var user = await _users.GetByIdAsync(staffId);
        return user is null || user.Role != "kasir" ? Forbid() : View(new StaffProfilePageViewModel { User = user });
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments(string? q, string? status, long? selected, int page = 1)
    {
        q = CleanQuery(q);
        status = PaymentStatusWorkflow.IsKnown(status) ? status : null;
        page = Math.Clamp(page, 1, 100000);
        var data = await _workspace.GetPaymentsAsync(q, status, page, 20);
        return View(new StaffPaymentsPageViewModel { Payments = data.Rows, Total = data.Total, Query = q, Status = status, SelectedOrderId = selected, Page = page });
    }

    [HttpGet("payments/{orderId:long}")]
    public async Task<IActionResult> PaymentStatus(long orderId)
    {
        var payment = await _workspace.GetPaymentAsync(orderId);
        return payment is null ? NotFound() : View(payment);
    }

    [HttpGet("pos")]
    public async Task<IActionResult> Pos(string? q, long? orderId)
    {
        q = CleanQuery(q);
        var data = q is null ? (Rows: (IReadOnlyList<StaffOrderRow>)[], Total: 0)
            : await _workspace.GetOrdersAsync(q, null, null, false, 1, 10);
        var selected = orderId.HasValue ? await _workspace.GetPaymentAsync(orderId.Value) : null;
        return View(new StaffPosPageViewModel { Query = q, Results = data.Rows, Selected = selected });
    }

    private static string? CleanQuery(string? query)
    {
        query = query?.Trim();
        return string.IsNullOrWhiteSpace(query) ? null : query[..Math.Min(query.Length, 80)];
    }
}
