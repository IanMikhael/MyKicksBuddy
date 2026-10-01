using System.Data;
using Dapper;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;
using MyKicksBuddy.Models.Entities;

namespace MyKicksBuddy.Services;

public sealed class DevelopmentDataSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevelopmentDataSeeder> _logger;

    public DevelopmentDataSeeder(
        IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<DevelopmentDataSeeder> logger)
    {
        _services = services;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() || !_configuration.GetValue("DevelopmentSeed:Enabled", false))
            return;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();
        await OpenWithRetryAsync(db, cancellationToken);
        using var transaction = db.BeginTransaction();

        try
        {
            var customerId = await EnsureUserAsync(db, transaction, "customer@mykicksbuddy.local", "Customer Sandbox", "customer", "Sandbox123!");
            await EnsureUserAsync(db, transaction, "admin@mykicksbuddy.local", "Admin Sandbox", "admin", "Admin123!");

            var serviceId = await db.QuerySingleAsync<long>(
                "SELECT id FROM services WHERE name = 'Deep Clean' LIMIT 1", transaction: transaction);

            var addressId = await db.QueryFirstOrDefaultAsync<long?>(
                "SELECT id FROM customer_addresses WHERE user_id = @CustomerId AND label = 'Alamat Sandbox' LIMIT 1",
                new { CustomerId = customerId }, transaction);
            if (addressId is null)
            {
                addressId = await db.ExecuteScalarAsync<long>(@"
                    INSERT INTO customer_addresses
                        (user_id, label, full_address, latitude, longitude, distance_km, is_within_radius, is_default)
                    VALUES
                        (@CustomerId, 'Alamat Sandbox', 'Dekat toko MyKicksBuddy', -7.821653, 110.129172, 0, TRUE, TRUE);
                    SELECT LAST_INSERT_ID();", new { CustomerId = customerId }, transaction);
            }

            var orderId = await db.QueryFirstOrDefaultAsync<long?>(
                "SELECT id FROM orders WHERE order_code = 'MKC-SANDBOX-001' LIMIT 1", transaction: transaction);
            if (orderId is null)
            {
                orderId = await db.ExecuteScalarAsync<long>(@"
                    INSERT INTO orders
                        (order_code, customer_id, channel, fulfillment_type, address_id, distance_km,
                         subtotal, total, status, payment_status, notes)
                    VALUES
                        ('MKC-SANDBOX-001', @CustomerId, 'online', 'pickup_delivery', @AddressId, 0,
                         60000, 60000, 'pending_payment', 'unpaid', 'Order otomatis untuk testing Sandbox');
                    SELECT LAST_INSERT_ID();", new { CustomerId = customerId, AddressId = addressId }, transaction);

                await db.ExecuteAsync(@"
                    INSERT INTO order_items (order_id, service_id, shoe_description, qty, price, subtotal)
                    VALUES (@OrderId, @ServiceId, 'Sneakers putih - data Sandbox', 1, 60000, 60000);
                    INSERT INTO order_status_log (order_id, status, note, changed_by)
                    VALUES (@OrderId, 'pending_payment', 'Order Sandbox dibuat otomatis', @CustomerId);",
                    new { OrderId = orderId, ServiceId = serviceId, CustomerId = customerId }, transaction);
            }

            transaction.Commit();
            _logger.LogInformation("Development seed siap: customer dan order Sandbox tersedia.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task OpenWithRetryAsync(IDbConnection db, CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (db is MySqlConnection mysqlConnection)
                    await mysqlConnection.OpenAsync(cancellationToken);
                else
                    db.Open();

                return;
            }
            catch (MySqlException exception) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Database belum siap untuk development seed (percobaan {Attempt}/{MaxAttempts}).",
                    attempt,
                    maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private static async Task<long> EnsureUserAsync(
        IDbConnection db,
        IDbTransaction transaction,
        string email,
        string fullName,
        string role,
        string password)
    {
        var existingId = await db.QueryFirstOrDefaultAsync<long?>(
            "SELECT id FROM users WHERE email = @Email LIMIT 1", new { Email = email }, transaction);
        if (existingId is not null) return existingId.Value;

        var user = new User { Email = email, FullName = fullName, Role = role, IsActive = true };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        return await db.ExecuteScalarAsync<long>(@"
            INSERT INTO users (role, full_name, email, password_hash, is_active)
            VALUES (@Role, @FullName, @Email, @PasswordHash, TRUE);
            SELECT LAST_INSERT_ID();", user, transaction);
    }
}
