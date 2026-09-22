using System.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using MySqlConnector;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default");
if (builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(connectionString))
{
    var localConnection = new MySqlConnectionStringBuilder(connectionString);
    if (localConnection.Server is "127.0.0.1" or "localhost" && localConnection.SslMode == MySqlSslMode.Preferred)
    {
        // Laragon/local MySQL commonly advertises SSL without a usable Windows certificate.
        localConnection.SslMode = MySqlSslMode.None;
        connectionString = localConnection.ConnectionString;
    }
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
builder.Services.AddControllersWithViews();

if (builder.Environment.IsDevelopment())
{
    var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, ".codex-localappdata", "DataProtection-Keys");
    Directory.CreateDirectory(dataProtectionPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}

// Database connection (Dapper)
builder.Services.AddScoped<IDbConnection>(_ =>
    new MySqlConnection(connectionString ?? string.Empty));

// Repositories & Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddSingleton<DistanceService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
// PaymentService/IPaymentProvider are not registered until a real provider is selected.
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IStaffWorkspaceRepository, StaffWorkspaceRepository>();
builder.Services.AddScoped<IAdminWorkspaceRepository, AdminWorkspaceRepository>();

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth/login";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect(context.Request.Path.StartsWithSegments("/admin") ? "/admin/login" : options.LoginPath.Value!);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect(context.Request.Path.StartsWithSegments("/admin") ? "/admin/login" : options.AccessDeniedPath.Value!);
            return Task.CompletedTask;
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await LogDatabaseDiagnosticsAsync(app, connectionString);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task LogDatabaseDiagnosticsAsync(WebApplication app, string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        app.Logger.LogWarning("Database connection string 'Default' is not configured.");
        return;
    }

    MySqlConnectionStringBuilder connectionBuilder;
    try
    {
        connectionBuilder = new MySqlConnectionStringBuilder(connectionString);
    }
    catch (ArgumentException ex)
    {
        app.Logger.LogError(ex, "Database connection string 'Default' is malformed.");
        return;
    }

    app.Logger.LogInformation(
        "Database target: server {Server}, port {Port}, database {Database}, user {UserId}.",
        connectionBuilder.Server,
        connectionBuilder.Port,
        connectionBuilder.Database,
        connectionBuilder.UserID);

    try
    {
        await using var connection = new MySqlConnection(connectionBuilder.ConnectionString);
        await connection.OpenAsync();
        app.Logger.LogInformation("Database connectivity check succeeded.");
    }
    catch (Exception ex) when (ex is MySqlException or InvalidOperationException)
    {
        app.Logger.LogWarning(ex, "Database connectivity check failed for server {Server}, port {Port}, database {Database}.",
            connectionBuilder.Server,
            connectionBuilder.Port,
            connectionBuilder.Database);
    }
}
