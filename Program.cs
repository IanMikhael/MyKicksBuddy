using System.Data;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
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
builder.Services.AddScoped<IJwtService, JwtService>();

// Payment (Midtrans)
builder.Services.Configure<MidtransOptions>(builder.Configuration.GetSection(MidtransOptions.SectionName));
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHttpClient<IMidtransSnapClient, MidtransSnapClient>();

// JWT Authentication - gagal cepat dengan pesan jelas kalau konfigurasi belum benar,
// daripada NullReferenceException samar atau (lebih buruk) key lemah yang lolos diam-diam.
var jwtKey = builder.Configuration["Jwt:SecretKey"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Konfigurasi 'Jwt:SecretKey' belum diatur.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("'Jwt:SecretKey' terlalu pendek - minimal 32 karakter (256-bit) untuk HMAC-SHA256.");
if (string.IsNullOrWhiteSpace(jwtIssuer))
    throw new InvalidOperationException("Konfigurasi 'Jwt:Issuer' belum diatur.");
if (string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("Konfigurasi 'Jwt:Audience' belum diatur.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };

    // Token bisa valid secara kriptografis tapi sudah "dicabut" (logout, ganti password,
    // akun dinonaktifkan) - cek ulang security stamp & status akun ke DB di tiap request,
    // supaya token yang sudah dicabut benar-benar berhenti bisa dipakai, bukan cuma
    // menunggu masa berlakunya habis.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var tokenStamp = context.Principal?.FindFirstValue(JwtService.SecurityStampClaimType);
            var tokenRole = context.Principal?.FindFirstValue(ClaimTypes.Role);

            if (!long.TryParse(userIdClaim, out var userId) || string.IsNullOrEmpty(tokenStamp))
            {
                context.Fail("Token tidak valid.");
                return;
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await userRepository.GetByIdAsync(userId);

            // Stamp beda -> token sudah dicabut (logout/nonaktif). Role beda -> role user
            // sudah diubah manual di DB sejak token ini diterbitkan (mis. kasir diturunkan
            // jadi customer); token lama tidak boleh terus jalan dengan role lama itu.
            if (user is null || !user.IsActive || user.SecurityStamp != tokenStamp ||
                !string.Equals(user.Role, tokenRole, StringComparison.OrdinalIgnoreCase))
            {
                context.Fail("Sesi tidak lagi berlaku. Silakan login ulang.");
            }
        }
    };
});

// Di belakang reverse proxy (Nginx/VPS), RemoteIpAddress cuma balikin IP proxy-nya
// buat semua orang kalau header X-Forwarded-For tidak dipercaya - efeknya rate limit
// per-IP di bawah ini jadi dibagi rame-rame satu ember buat seluruh pengguna.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // IP reverse proxy production diisi lewat config (mis. env var
    // TrustedProxies__0=156.67.24.112), bukan hardcode di sini, supaya tim yang deploy
    // bisa mengunci ini tanpa perlu ubah kode. Kalau belum diisi (dev lokal / belum
    // sempat dikonfigurasi), semua upstream dipercaya - X-Forwarded-For jadi bisa
    // dipalsukan oleh client langsung, jadi ini WAJIB diisi begitu topologi production
    // (IP reverse proxy) sudah pasti.
    var trustedProxies = builder.Configuration.GetSection("TrustedProxies").Get<string[]>() ?? [];
    if (trustedProxies.Length > 0)
    {
        foreach (var proxyIp in trustedProxies)
        {
            if (System.Net.IPAddress.TryParse(proxyIp, out var parsed))
                options.KnownProxies.Add(parsed);
        }
    }
    else
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// Rate limiting - batasi percobaan login/register per IP supaya tidak gampang di-brute-force
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("chatbot", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Harus di paling awal pipeline - request lain (HTTPS redirect, rate limiter per-IP,
// auth) perlu IP/skema asli klien, bukan punya reverse proxy, dan itu cuma diperbaiki
// kalau middleware ini jalan duluan.
app.UseForwardedHeaders();

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

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();