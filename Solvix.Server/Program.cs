using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Solvix.Server.API.Hubs;
using Solvix.Server.Application.Services;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
// ✅ خط 9 اصلاح شد - namespace مکرر حذف شد
using Solvix.Server.Data;
using Solvix.Server.Infrastructure.Repositories;
using Solvix.Server.Infrastructure.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ اضافه کردن Memory Cache - مشکل اصلی OtpService حل شد
builder.Services.AddMemoryCache();

// Database Configuration
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null))
    // ✅ غیرفعال کردن pending model changes warning
    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Identity Configuration - Fixed to use AppRole instead of IdentityRole<long>
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ChatDbContext>()
.AddDefaultTokenProviders();

// JWT Configuration with better error handling
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];

if (string.IsNullOrEmpty(jwtKey))
    throw new InvalidOperationException("JWT:Key not configured in appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Configure JWT for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Firebase Configuration with error handling
try
{
    var firebaseConfigPath = Path.Combine(builder.Environment.ContentRootPath, "fcm-service-account.json");
    if (File.Exists(firebaseConfigPath))
    {
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", firebaseConfigPath);
        builder.Services.AddSingleton(FirebaseAdmin.FirebaseApp.Create());
        builder.Logging.AddConsole();
        // ✅ حذف BuildServiceProvider call
        Console.WriteLine("Firebase configuration found and loaded.");
    }
    else
    {
        builder.Logging.AddConsole();
        Console.WriteLine($"Firebase service account file not found at {firebaseConfigPath}. Push notifications will be disabled.");
    }
}
catch (Exception ex)
{
    builder.Logging.AddConsole();
    Console.WriteLine($"Failed to initialize Firebase: {ex.Message}. Push notifications will be disabled.");
}

// HttpClient Configuration
builder.Services.AddHttpClient("NotificationClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Solvix-Server/1.0");
});

// ✅ اضافه کردن OtpClient برای OtpService
builder.Services.AddHttpClient("OtpClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Solvix-Server/1.0");
});

// Repository Pattern Registration - Fixed lifetimes
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IUserContactRepository, UserContactRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IGroupSettingsRepository, GroupSettingsRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Application Services Registration - Fixed lifetimes to all Scoped
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserConnectionService, UserConnectionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IGroupManagementService, GroupManagementService>();
builder.Services.AddScoped<ISearchService, SearchService>();

// Token service as scoped instead of singleton for better security
builder.Services.AddScoped<ITokenService, TokenService>();

// Authentication Services
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthenticationStrategy, PasswordAuthenticationStrategy>();
builder.Services.AddScoped<IAuthenticationStrategy, OtpAuthenticationStrategy>();
builder.Services.AddScoped<AuthenticationContext>();

// SignalR Configuration
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 102400; // 100KB
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Rate Limiting Configuration - Fixed to handle proxy scenarios
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AuthLimit", opt =>
    {
        opt.PermitLimit = 10; // 10 درخواست
        opt.Window = TimeSpan.FromMinutes(1); // در هر 1 دقیقه
        opt.QueueLimit = 2;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    }).AddFixedWindowLimiter("OtpRequestLimit", opt =>
    {
        opt.PermitLimit = 3; // 3 درخواست
        opt.Window = TimeSpan.FromMinutes(5); // در هر 5 دقیقه
        opt.QueueLimit = 1;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientId = GetClientIdentifier(context);
        return RateLimitPartition.GetFixedWindowLimiter(clientId, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });
    });
});

var app = builder.Build();

// Initialize Firebase with better error handling
try
{
    if (FirebaseAdmin.FirebaseApp.DefaultInstance != null)
    {
        app.Logger.LogInformation("Firebase initialized successfully for push notifications.");
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Firebase initialization failed. Push notifications will be disabled.");
    // Don't crash the app, just log and continue
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Solvix API V1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at app's root
    });
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/api/error");
    app.UseHsts();
}

// Middleware pipeline order is important
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

// Helper method for better client identification in rate limiting
static string GetClientIdentifier(HttpContext context)
{
    // Check for real IP behind proxy
    var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault()
                ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? context.Request.Headers.Host.ToString();

    return realIp;
}