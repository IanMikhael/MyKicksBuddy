using System.Data;
using Dapper;
using MySqlConnector;
using MyKicksBuddy.Models.Dtos;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
}

Check(OrderStatusWorkflow.ProgressStage(OrderStatusWorkflow.PendingPayment) == 0, "pending at Dibuat");
Check(OrderStatusWorkflow.ProgressStage(OrderStatusWorkflow.WaitingPickup) == 0, "waiting pickup not Dijemput");
Check(OrderStatusWorkflow.ProgressStage(OrderStatusWorkflow.PickedUp) == 1, "picked up at Dijemput");
Check(OrderStatusWorkflow.ProgressStage(OrderStatusWorkflow.InProcess) == 2, "processing at Diproses");
Check(OrderStatusWorkflow.ProgressStage(OrderStatusWorkflow.Returned) == 2, "returned not yet Selesai");
Check(OrderStatusWorkflow.ProgressStage(OrderStatusWorkflow.Completed) == 3, "completed at Selesai");
Check(OrderStatusWorkflow.HistoryGroup(OrderStatusWorkflow.PendingPayment) == "active" &&
      OrderStatusWorkflow.HistoryGroup(OrderStatusWorkflow.Completed) == "completed" &&
      OrderStatusWorkflow.HistoryGroup(OrderStatusWorkflow.Cancelled) == "cancelled", "history groups");
Check(!OrderStatusWorkflow.OperatorNextStatuses(OrderStatusWorkflow.PendingPayment, PaymentStatusWorkflow.Unpaid)
      .Contains(OrderStatusWorkflow.WaitingApproval), "operator cannot confirm payment");
Check(!OrderStatusWorkflow.OperatorNextStatuses(OrderStatusWorkflow.WaitingPickup, PaymentStatusWorkflow.Unpaid)
      .Contains(OrderStatusWorkflow.PickedUp), "unpaid cannot be picked up");
Check(!OrderStatusWorkflow.CanTransition(OrderStatusWorkflow.Completed, OrderStatusWorkflow.InProcess), "completed is terminal");
Check(PaymentStatusWorkflow.Label(PaymentStatusWorkflow.Unpaid) == "Menunggu Konfirmasi" &&
      PaymentStatusWorkflow.Label(PaymentStatusWorkflow.Paid) == "Berhasil" &&
      PaymentStatusWorkflow.Label(PaymentStatusWorkflow.Failed) == "Gagal" &&
      PaymentStatusWorkflow.Label(PaymentStatusWorkflow.Expired) == "Kedaluwarsa", "payment labels");

// Local development fixture. This runner uses existing customer accounts and
// removes only rows bearing its unique identifiers in finally.
await using var db = new MySqlConnection("Server=127.0.0.1;Database=mykicksbuddy;User ID=root;SslMode=None");
await db.OpenAsync();
var customerIds = (await db.QueryAsync<long>("SELECT id FROM users WHERE role = 'customer' ORDER BY id LIMIT 2")).ToArray();
Check(customerIds.Length == 2, "two existing local customer accounts");
var marker = $"lifecycle-qa-{Guid.NewGuid():N}";
long serviceId = 0;
long orderId = 0;
try
{
    serviceId = await db.ExecuteScalarAsync<long>(@"
        INSERT INTO services (name, description, price, is_active)
        VALUES (@Marker, 'Temporary local check', 75000, 1);
        SELECT LAST_INSERT_ID();", new { Marker = marker });
    var repository = new OrderRepository(db);
    var service = new OrderService(repository, new AddressRepository(db));
    var created = await service.CreateOrderAsync(customerIds[0], new CreateOrderRequest
    {
        FulfillmentType = "drop_off",
        Items = [new OrderItemRequest { ServiceId = serviceId, Quantity = 2, ShoeDescription = "Test shoes" }]
    });
    Check(created.Success, "order creation");
    orderId = created.OrderId;
    var owned = await service.GetOrderDetailAsync(orderId, customerIds[0]);
    Check(owned is not null && owned.TotalAmount == 150000 && owned.Subtotal == 150000,
        "server calculates price x quantity, not client total");
    Check(owned!.Status == OrderStatusWorkflow.PendingPayment && owned.PaymentStatus == PaymentStatusWorkflow.Unpaid,
        "new order stays pending and unpaid");
    Check(await service.GetOrderDetailAsync(orderId, customerIds[1]) is null, "other customer cannot read order");
    Check(!(await service.GetOrdersByCustomerAsync(customerIds[1])).Any(x => x.Id == orderId),
        "other customer history excludes order");
    var beforeLogs = (await service.GetOrderLogsAsync(orderId)).Count();
    Check(beforeLogs == 1, "creation writes exactly one status log");
    var illegal = await service.UpdateStatusAsync(orderId, OrderStatusWorkflow.InProcess, customerIds[0], "customer", null);
    Check(!illegal.Success, "customer cannot set processing");
    illegal = await service.UpdateStatusAsync(orderId, OrderStatusWorkflow.Completed, customerIds[0], "customer", null);
    Check(!illegal.Success, "customer cannot set completed");
    illegal = await service.UpdateStatusAsync(orderId, OrderStatusWorkflow.PickedUp, customerIds[0], "kasir", null);
    Check(!illegal.Success, "staff cannot pick up unpaid order");
    illegal = await service.UpdateStatusAsync(orderId, OrderStatusWorkflow.WaitingApproval, customerIds[0], "admin", null);
    Check(!illegal.Success, "staff cannot manually confirm payment");
    var bypass = await repository.UpdateStatusWithLogAsync(orderId, OrderStatusWorkflow.PendingPayment,
        OrderStatusWorkflow.InProcess, customerIds[0], null);
    Check(!bypass, "repository rejects illegal transition");
    Check((await service.GetOrderLogsAsync(orderId)).Count() == beforeLogs, "rejected transitions do not duplicate log");
    Check((await service.GetOrderDetailAsync(orderId, customerIds[0]))!.PaymentStatus == PaymentStatusWorkflow.Unpaid,
        "rejected transitions do not mark payment paid");
    var cancelled = await service.UpdateStatusAsync(orderId, OrderStatusWorkflow.Cancelled, customerIds[0], "kasir", "Local lifecycle check");
    Check(cancelled.Success, "authorized cancellation of unpaid order");
    Check((await service.GetOrderLogsAsync(orderId)).Count() == beforeLogs + 1, "valid transition writes one log");
    var repeated = await service.UpdateStatusAsync(orderId, OrderStatusWorkflow.Cancelled, customerIds[0], "kasir", null);
    Check(!repeated.Success && (await service.GetOrderLogsAsync(orderId)).Count() == beforeLogs + 1,
        "repeat transition does not duplicate log");
}
finally
{
    if (orderId != 0)
    {
        await db.ExecuteAsync("DELETE FROM order_status_log WHERE order_id = @OrderId", new { OrderId = orderId });
        await db.ExecuteAsync("DELETE FROM order_items WHERE order_id = @OrderId", new { OrderId = orderId });
        await db.ExecuteAsync("DELETE FROM orders WHERE id = @OrderId", new { OrderId = orderId });
    }
    if (serviceId != 0)
        await db.ExecuteAsync("DELETE FROM services WHERE id = @ServiceId AND name = @Marker", new { ServiceId = serviceId, Marker = marker });
}
Console.WriteLine("PASS: local fixture removed");
