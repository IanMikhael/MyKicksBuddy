using System.Data;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

const string HybridAuthenticationScheme = "CookieOrJwt";

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");

if (builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(connectionString))
{
    var localConnection = new MySqlConnectionStringBuilder(connectionString);

    if (localConnection.Server is "127.0.0.1" or "localhost" &&
        localConnection.SslMode == MySqlSslMode.Preferred)
    {
        // Laragon/local MySQL commonly advertises SSL without a usable
        // Windows certificate.
        localConnection.SslMode = MySqlSslMode.None;
        connectionString = localConnection.ConnectionString;
    }
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();

if (builder.Environment.IsDevelopment())
{
    var dataProtectionPath = Path.Combine(
        builder.Environment.ContentRootPath,
        ".codex-localappdata",
        "DataProtection-Keys");

    Directory.CreateDirectory(dataProtectionPath);

    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}

// Database
builder.Services.AddScoped<IDbConnection>(_ =>
    new MySqlConnection(connectionString ?? string.Empty));

// Core repositories & services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IAddressService, AddressService>();

builder.Services.AddSingleton<DistanceService>();

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Application UI/workspace services
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IStaffWorkspaceRepository, StaffWorkspaceRepository>();
builder.Services.AddScoped<IAdminWorkspaceRepository, AdminWorkspaceRepository>();

// JWT
builder.Services.AddScoped<IJwtService, JwtService>();

// Midtrans payment
builder.Services.Configure<MidtransOptions>(
    builder.Configuration.GetSection(MidtransOptions.SectionName));

builder.Services.AddScoped<MyKicksBuddy.Services.IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHttpClient<IMidtransSnapClient, MidtransSnapClient>();

// JWT configuration
var jwtKey = builder.Configuration["Jwt:SecretKey"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

// Cookie + JWT authentication.
//
// Browser MVC requests normally use Cookie authentication.
// Requests carrying "Authorization: Bearer ..." use JWT.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = HybridAuthenticationScheme;
        options.DefaultAuthenticateScheme = HybridAuthenticationScheme;
        options.DefaultChallengeScheme = HybridAuthenticationScheme;
    })
    .AddPolicyScheme(
        HybridAuthenticationScheme,
        HybridAuthenticationScheme,
        options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authorization =
                    context.Request.Headers.Authorization.ToString();

                return authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme
                    : CookieAuthenticationDefaults.AuthenticationScheme;
            };
        })
    .AddCookie(
        CookieAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
            options.LoginPath = "/auth/login";
            options.AccessDeniedPath = "/auth/login";

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.Redirect(
                    context.Request.Path.StartsWithSegments("/admin")
                        ? "/admin/login"
                        : options.LoginPath.Value!);

                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.Redirect(
                    context.Request.Path.StartsWithSegments("/admin")
                        ? "/admin/login"
                        : options.AccessDeniedPath.Value!);

                return Task.CompletedTask;
            };
        })
    .AddJwtBearer(
        JwtBearerDefaults.AuthenticationScheme,
        options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey))
            };
        });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await LogDatabaseDiagnosticsAsync(app, connectionString);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization(
    new RequestLocalizationOptions()
        .SetDefaultCulture("en-US")
        .AddSupportedCultures("en-US")
        .AddSupportedUICultures("en-US"));

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task LogDatabaseDiagnosticsAsync(
    WebApplication app,
    string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        app.Logger.LogWarning(
            "Database connection string 'Default' is not configured.");

        return;
    }

    MySqlConnectionStringBuilder connectionBuilder;

    try
    {
        connectionBuilder =
            new MySqlConnectionStringBuilder(connectionString);
    }
    catch (ArgumentException ex)
    {
        app.Logger.LogError(
            ex,
            "Database connection string 'Default' is malformed.");

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
        await using var connection =
            new MySqlConnection(connectionBuilder.ConnectionString);

        await connection.OpenAsync();

        app.Logger.LogInformation(
            "Database connectivity check succeeded.");
    }
    catch (Exception ex)
        when (ex is MySqlException or InvalidOperationException)
    {
        app.Logger.LogWarning(
            ex,
            "Database connectivity check failed for server {Server}, port {Port}, database {Database}.",
            connectionBuilder.Server,
            connectionBuilder.Port,
            connectionBuilder.Database);
    }
}