using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Models.Entities;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "admin")]
[Route("admin")]
public sealed class AdminController : Controller
{
    private readonly IOrderService _orders;
    private readonly IServiceRepository _services;
    private readonly IUserRepository _users;
    private readonly IAdminWorkspaceRepository _workspace;
    public AdminController(IOrderService orders, IServiceRepository services, IUserRepository users, IAdminWorkspaceRepository workspace) => (_orders, _services, _users, _workspace) = (orders, services, users, workspace);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) && action == nameof(Login))
        { await next(); return; }
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { context.Result=Unauthorized(); return; }
        var user = await _users.GetByIdAsync(id);
        if (user is null || !user.IsActive || user.Role != "admin") { context.Result=Forbid(); return; }
        await next();
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true && User.IsInRole("admin") ? RedirectToAction(nameof(Index)) : View();

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(new AdminDashboardViewModel
    {
        Summary = await _orders.GetDashboardSummaryAsync(),
        RecentOrders = await _orders.GetAllOrdersAsync(null, null, null, 8),
        PriorityOrders = await _orders.GetAllOrdersAsync("waiting_approval", null, null, 8)
    });

    [HttpGet("orders")]
    public async Task<IActionResult> Orders(string? status, string? paymentStatus, string? search, DateTime? from, DateTime? to, int page = 1)
    {
        if (page < 1) page = 1;
        if (page > 100000 || (from.HasValue && to.HasValue && from > to)) return BadRequest();
        if (!string.IsNullOrEmpty(status) && !OrderStatusWorkflow.IsKnown(status)) return BadRequest();
        if (!string.IsNullOrEmpty(paymentStatus) && !PaymentStatusWorkflow.IsKnown(paymentStatus)) return BadRequest();
        var result = await _workspace.SearchOrdersAsync(search, status, paymentStatus, from, to, page, 20);
        return View(new OrdersPageViewModel { Orders=result.Rows,TotalCount=result.Count,Page=page,Status=status,PaymentStatus=paymentStatus,Search=search,From=from,To=to });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? end.AddDays(-29)).Date;
        if (start > end || end.Subtract(start).TotalDays > 366) return BadRequest();
        return View(await _workspace.GetReportAsync(start, end));
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments(string? search, string? status)
    {
        if (!string.IsNullOrEmpty(status) && !PaymentStatusWorkflow.IsKnown(status)) return BadRequest();
        ViewData["Search"] = search;
        ViewData["Status"] = status;
        return View(await _workspace.GetPaymentsAsync(search, status));
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications() => View(await _workspace.GetEventsAsync(100));

    [HttpGet("system-status")]
    public async Task<IActionResult> SystemStatus() => View(await _workspace.CheckDatabaseAsync());

    [HttpGet("pos")]
    public IActionResult Pos() => View();

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await _users.GetByIdAsync(id);
        if (user is null || !user.IsActive || user.Role != "admin") return Forbid();
        return View(new AdminProfileViewModel { Id=user.Id,FullName=user.FullName,Email=user.Email,Phone=user.Phone,Role=user.Role });
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(AdminProfileEditModel model)
    {
        if (!ModelState.IsValid) { TempData["Error"]="Periksa nama, email, dan nomor ponsel."; return RedirectToAction(nameof(Profile)); }
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await _users.GetByIdAsync(id);
        if (user is null || !user.IsActive || user.Role != "admin") return Forbid();
        var updated = await _users.UpdateAdminProfileAsync(id,model.FullName.Trim(),model.Email.Trim().ToLowerInvariant(),string.IsNullOrWhiteSpace(model.Phone)?null:model.Phone.Trim());
        if (updated)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier,id.ToString()), new Claim(ClaimTypes.Name,model.FullName.Trim()), new Claim(ClaimTypes.Role,"admin") }, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity));
        }
        TempData[updated?"Success":"Error"] = updated ? "Profil berhasil diperbarui." : "Email atau nomor ponsel telah dipakai akun lain.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost("profile/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(AdminPasswordChangeModel model)
    {
        if (!ModelState.IsValid) { TempData["Error"]="Kata sandi baru minimal 8 karakter dan konfirmasi harus sama."; return RedirectToAction(nameof(Profile)); }
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await _users.GetByIdAsync(id);
        if (user is null || !user.IsActive || user.Role != "admin") return Forbid();
        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user,user.PasswordHash,model.CurrentPassword)==PasswordVerificationResult.Failed)
        { TempData["Error"]="Kata sandi saat ini tidak cocok."; return RedirectToAction(nameof(Profile)); }
        var newHash = hasher.HashPassword(user,model.NewPassword);
        var updated = await _users.UpdateAdminPasswordHashAsync(id,user.PasswordHash,newHash);
        TempData[updated?"Success":"Error"] = updated ? "Kata sandi berhasil diperbarui." : "Kata sandi berubah di sesi lain. Coba lagi.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> OrderDetail(long id)
    {
        var order = await _orders.GetOrderDetailForStaffAsync(id);
        if (order is null) return NotFound();
        var allowed = OrderStatusWorkflow.OperatorNextStatuses(order.Status, order.PaymentStatus).ToList();
        return View(new OrderDetailPageViewModel { Order = order, Logs = (await _orders.GetOrderLogsAsync(id)).ToList(), AllowedStatuses = allowed });
    }

    [HttpPost("orders/{id:long}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(long id, string status, string? notes)
    {
        var result = await _orders.UpdateStatusAsync(id, status, long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), "admin", notes);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Status pesanan diperbarui dan dicatat." : result.Error;
        return RedirectToAction(nameof(OrderDetail), new { id });
    }

    [HttpGet("services")]
    public async Task<IActionResult> Services(string? search, string? status)
    {
        var all = await _services.GetAllAsync();
        ViewData["ActiveCount"] = all.Count(s => s.IsActive);
        ViewData["InactiveCount"] = all.Count(s => !s.IsActive);
        ViewData["AveragePrice"] = all.Count == 0 ? 0m : all.Average(s => s.Price);
        ViewData["Search"] = search;
        ViewData["Status"] = status;
        if (!string.IsNullOrWhiteSpace(search)) all = all.Where(s => s.Name.Contains(search.Trim(),StringComparison.OrdinalIgnoreCase) || (s.Description?.Contains(search.Trim(),StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        if (status == "active") all = all.Where(s => s.IsActive).ToList();
        else if (status == "inactive") all = all.Where(s => !s.IsActive).ToList();
        else if (!string.IsNullOrEmpty(status)) return BadRequest();
        return View(all);
    }

    [HttpGet("services/new")]
    public IActionResult CreateService() => View("ServiceForm", new ServiceAdminFormModel());

    [HttpPost("services/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateService(ServiceAdminFormModel model)
    {
        if (!ModelState.IsValid) return View("ServiceForm", model);
        await _services.CreateAsync(model); TempData["Success"] = "Layanan berhasil dibuat."; return RedirectToAction(nameof(Services));
    }

    [HttpGet("services/{id:long}/edit")]
    public async Task<IActionResult> EditService(long id)
    {
        var service = await _services.GetByIdAsync(id); if (service is null) return NotFound();
        return View("ServiceForm", new ServiceAdminFormModel { Id=service.Id, Name=service.Name, Description=service.Description, Price=service.Price, EstimatedDurationDays=service.EstimatedDurationDays, IsActive=service.IsActive });
    }

    [HttpPost("services/{id:long}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditService(long id, ServiceAdminFormModel model)
    {
        if (id != model.Id) return BadRequest(); if (!ModelState.IsValid) return View("ServiceForm", model);
        await _services.UpdateAsync(model); TempData["Success"] = "Layanan berhasil diperbarui."; return RedirectToAction(nameof(Services));
    }

    [HttpPost("services/{id:long}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleService(long id, string operation)
    {
        if (operation is not ("activate" or "deactivate")) return BadRequest();
        var active = operation == "activate";
        await _services.SetActiveAsync(id, active);
        TempData["Success"] = active ? "Layanan diaktifkan." : "Layanan dinonaktifkan.";
        return RedirectToAction(nameof(Services));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users() => View(await _users.GetInternalUsersAsync());
}
