using System.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using MySqlConnector;
using MyKicksBuddy.Repositories;
using MyKicksBuddy.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database connection (Dapper)
builder.Services.AddScoped<IDbConnection>(_ =>
    new MySqlConnection(builder.Configuration.GetConnectionString("Default")));

// Repositories & Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddSingleton<DistanceService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.Configure<MidtransOptions>(builder.Configuration.GetSection(MidtransOptions.SectionName));
builder.Services.AddHttpClient<IMidtransSnapClient, MidtransSnapClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth/login";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization(new RequestLocalizationOptions()
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