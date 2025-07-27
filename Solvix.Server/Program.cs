using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Solvix.Server.API.Hubs;
using Solvix.Server.Application.Services;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Core.Interfaces.Solvix.Server.Core.Interfaces;
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

// Database Configuration
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

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
if (string.IsNullOrEmpty(issuer))
    throw new InvalidOperationException("JWT:Issuer not configured in appsettings.json");
if (string.IsNullOrEmpty(audience))
    throw new InvalidOperationException("JWT:Audience not configured in appsettings.json");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // Set to true in production
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // SignalR Token Configuration
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
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "Authentication failed");
            return Task.CompletedTask;
        }
    };
});

// Memory Cache for OTP
builder.Services.AddMemoryCache();

// HTTP Client Configuration
builder.Services.AddHttpClient("OtpClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "Solvix-Server/1.0");
});

// Repository Pattern Registration - Fixed lifetimes
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IUserContactRepository, UserContactRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IGroupMemberRepository, GroupMemberRepository>();
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
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIdentifier(httpContext),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            })
    );

    options.AddPolicy("AuthLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIdentifier(httpContext),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(5)
            })
    );

    options.AddPolicy("OtpRequestLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIdentifier(httpContext),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(15)
            })
    );
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
    });
});

// Enhanced Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add EventLog only on Windows platforms in production
if (!builder.Environment.IsDevelopment() && OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog();
}

var app = builder.Build();

// Initialize Firebase with better error handling
try
{
    FirebaseAdminSetup.Initialize(app);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Firebase Admin SDK initialization failed. Push notifications will be disabled.");
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