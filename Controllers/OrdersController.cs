using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[Authorize(Roles = "customer")]
[Route("orders")]
public class OrdersController : Controller
{
    private readonly IOrderService _orderService;
    private readonly IAddressService _addressService;

    public OrdersController(IOrderService orderService, IAddressService addressService)
    {
        _orderService = orderService;
        _addressService = addressService;
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create([FromQuery] long? serviceId = null)
    {
        var model = new CreateOrderFormModel { ServiceId = serviceId ?? 0 };
        await PopulateFormAsync(model);
        return View(model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateOrderFormModel model)
    {
        if (model.FulfillmentType == "pickup_delivery" && !model.AddressId.HasValue)
            ModelState.AddModelError(nameof(model.AddressId), "Pilih alamat untuk layanan antar-jemput.");

        if (!ModelState.IsValid)
        {
            await PopulateFormAsync(model);
            return View(model);
        }

        var request = new CreateOrderRequest
        {
            FulfillmentType = model.FulfillmentType,
            AddressId = model.AddressId,
            Notes = model.Notes,
            Items =
            [
                new OrderItemRequest
                {
                    ServiceId = model.ServiceId,
                    Quantity = model.Quantity,
                    ShoeDescription = model.ShoeDescription.Trim()
                }
            ]
        };

        var result = await _orderService.CreateOrderAsync(GetCustomerId(), request);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Pesanan tidak dapat dibuat.");
            await PopulateFormAsync(model);
            return View(model);
        }

        return RedirectToAction(nameof(Payment), new { id = result.OrderId });
    }

    [HttpGet("")]
    public async Task<IActionResult> GetMyOrders()
    {
        var orders = await _orderService.GetOrdersByCustomerAsync(GetCustomerId());
        return Ok(orders);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var orders = await _orderService.GetOrdersByCustomerAsync(GetCustomerId());
        return View(orders);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetOrderDetail(long id)
    {
        var order = await _orderService.GetOrderDetailAsync(id, GetCustomerId());
        return order is null
            ? NotFound(new { message = "Pesanan tidak ditemukan atau bukan milik Anda." })
            : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreateApi([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _orderService.CreateOrderAsync(GetCustomerId(), request);
        return result.Success
            ? Ok(new { message = "Pesanan berhasil dibuat!", orderId = result.OrderId })
            : BadRequest(new { message = result.Error });
    }

    [HttpGet("{id:long}/detail")]
    public async Task<IActionResult> Detail(long id)
    {
        var order = await _orderService.GetOrderDetailAsync(id, GetCustomerId());
        if (order is null) return NotFound();
        var logs = (await _orderService.GetOrderLogsAsync(id)).ToList();
        return View(new OrderDetailPageViewModel { Order = order, Logs = logs });
    }

    [HttpGet("{id:long}/payment")]
    public async Task<IActionResult> Payment(long id)
    {
        var order = await _orderService.GetOrderDetailAsync(id, GetCustomerId());
        if (order is null)
            return NotFound();

        return View("PendingPayment", order);
    }

    private async Task PopulateFormAsync(CreateOrderFormModel model)
    {
        model.Services = await _orderService.GetAllServicesAsync();
        model.Addresses = await _addressService.GetByCustomerAsync(GetCustomerId());
    }

    private long GetCustomerId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
