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

    // 1. Cek Status Pesanan berdasarkan Order Code
    [HttpGet("orders/{orderCode}")]
    public async Task<IActionResult> GetOrderByCode(string orderCode)
    {
        var order = await _orderService.GetOrderByCodeAsync(orderCode);

        if (order == null)
            return NotFound(new { message = "Maaf, pesanan dengan kode tersebut tidak ditemukan." });

        return Ok(order);
    }

    // 2. Daftar Layanan
    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var services = await _orderService.GetAllServicesAsync();
        return Ok(services);
    }

    // 3. Estimasi Harga dan Waktu
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

        var completionTime = DateTime.Now.AddHours(maxHours);

        return Ok(new EstimateResponse
        {
            TotalPrice = totalPrice,
            EstimatedHours = maxHours,
            EstimatedCompletionText = $"Estimasi selesai sekitar {completionTime:dd MMM yyyy, HH:mm} ({maxHours} jam pengerjaan)",
            Details = details
        });
    }

    // 4. Buat Pesanan Baru via Chatbot (Menyelesaikan Task 15)
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request == null || request.Items == null || !request.Items.Any())
        {
            return BadRequest(new { message = "Data pesanan atau item layanan tidak boleh kosong." });
        }

        // Tentukan ID pelanggan default untuk pesanan via chatbot jika tidak dikirim dari payload
        long customerId = request.CustomerId > 0 ? request.CustomerId : 2;

        var result = await _orderService.CreateOrderAsync(customerId, request);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Error });
        }

        // Ambil detail ringkas pesanan yang baru dibuat untuk diumpankan kembali ke AI / Response n8n
        var createdOrder = await _orderService.GetOrderDetailAsync(result.OrderId, customerId);

        return Ok(new 
        { 
            message = "Pesanan berhasil dibuat via chatbot!",
            orderId = result.OrderId,
            orderCode = createdOrder?.OrderCode,
            totalAmount = createdOrder?.TotalAmount,
            status = createdOrder?.Status,
            paymentStatus = createdOrder?.PaymentStatus,
            data = request
        });
    }
}