using Microsoft.AspNetCore.Mvc;
using MyKicksBuddy.Filters;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Services;

namespace MyKicksBuddy.Controllers;

[ApiController]
[Route("api/chatbot")]
[ApiKey]
public class ChatbotController : ControllerBase
{
    private readonly IOrderService _orderService;

    public ChatbotController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet("orders/{orderCode}")]
    public async Task<IActionResult> GetOrderByCode(string orderCode)
    {
        var detail = await _orderService.GetOrderByCodeAsync(orderCode);

        if (detail == null)
            return NotFound(new { message = "Maaf, pesanan dengan kode tersebut tidak ditemukan." });

        // Return hanya informasi yang aman, tanpa customerId, notes, address detail
        var response = new ChatbotOrderResponse
        {
            OrderCode = detail.OrderCode,
            Status = detail.Status,
            PaymentStatus = detail.PaymentStatus,
            FulfillmentType = detail.FulfillmentType,
            TotalAmount = detail.TotalAmount,
            CreatedAt = detail.CreatedAt,
            Items = detail.Items.Select(i => new ChatbotOrderItemResponse
            {
                ServiceName = i.ServiceName ?? string.Empty,
                Quantity = i.Quantity,
                Subtotal = i.Subtotal
            }).ToList()
        };

        return Ok(response);
    }

    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var services = await _orderService.GetAllServicesAsync();
        return Ok(services);
    }

    [HttpPost("estimate")]
    public async Task<IActionResult> GetEstimation([FromBody] EstimateRequest request)
    {
        if (request?.Items == null || !request.Items.Any())
            return BadRequest(new { message = "Item layanan tidak boleh kosong." });

        var allServices = await _orderService.GetAllServicesAsync();
        var serviceMap = allServices.ToDictionary(s => s.Id);

        decimal totalPrice = 0;
        int maxHours = 0;
        var details = new List<EstimateDetailItemResponse>();

        foreach (var item in request.Items)
        {
            if (!serviceMap.TryGetValue(item.ServiceId, out var service))
                return NotFound(new { message = $"Layanan dengan ID {item.ServiceId} tidak ditemukan." });

            decimal subtotal = service.Price * item.Quantity;
            totalPrice += subtotal;

            if (service.EstimatedHours > maxHours)
                maxHours = service.EstimatedHours;

            details.Add(new EstimateDetailItemResponse
            {
                ServiceName = service.Name,
                Quantity = item.Quantity,
                SubtotalPrice = subtotal,
                ServiceEstimatedHours = service.EstimatedHours
            });
        }

        var completionTime = DateTime.UtcNow.AddHours(maxHours);

        return Ok(new EstimateResponse
        {
            TotalPrice = totalPrice,
            EstimatedHours = maxHours,
            EstimatedCompletionText = $"Estimasi selesai sekitar {completionTime:dd MMM yyyy, HH:mm} ({maxHours} jam pengerjaan)",
            Details = details
        });
    }
}