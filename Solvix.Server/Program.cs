using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Solvix.Server.API.Hubs;
using Solvix.Server.Application.Services;
using Solvix.Server.Core.Entities;
using Solvix.Server.Core.Interfaces;
using Solvix.Server.Data;
using Solvix.Server.Infrastructure.Repositories;
using Solvix.Server.Infrastructure.Services;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole<long>>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<ChatDbContext>()
.AddDefaultTokenProviders();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key not configured in appsettings.json for JWT Bearer setup.");
}
var issuer = builder.Configuration["Jwt:Issuer"];
if (string.IsNullOrEmpty(issuer))
{
    throw new InvalidOperationException("JWT Issuer not configured in appsettings.json for JWT Bearer setup.");
}
var audience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrEmpty(audience))
{
    throw new InvalidOperationException("JWT Audience not configured in appsettings.json for JWT Bearer setup.");
}

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
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

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

// اضافه کردن کش برای نگهداری کدهای OTP
builder.Services.AddMemoryCache();

// تنظیم HttpClient برای سرویس OTP
builder.Services.AddHttpClient("OtpClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ثبت سرویس‌های مورد نیاز
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<IChatService, ChatService>();
builder.Services.AddTransient<IUserConnectionService, UserConnectionService>();
builder.Services.AddTransient<INotificationService, NotificationService>();


builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<ISearchService, SearchService>();


// ثبت سرویس‌های جدید برای احراز هویت با OTP
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthenticationStrategy, PasswordAuthenticationStrategy>();
builder.Services.AddScoped<IAuthenticationStrategy, OtpAuthenticationStrategy>();
builder.Services.AddScoped<AuthenticationContext>();

builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
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
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
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
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(15)
            })
    );
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policyBuilder =>
        {
            policyBuilder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

FirebaseAdminSetup.Initialize(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/api/error");
    app.UseHsts();
}

app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Use(async (context, next) =>
{
    if (!HttpMethods.IsPost(context.Request.Method) &&
        !HttpMethods.IsPut(context.Request.Method) &&
        !HttpMethods.IsPatch(context.Request.Method) &&
        !HttpMethods.IsDelete(context.Request.Method))
    {
        await next(context);
        return;
    }

    if (context.Request.Path.StartsWithSegments("/chathub") ||
        context.Request.Path.StartsWithSegments("/api/auth"))
    {
        await next(context);
        return;
    }

    await next(context);
});

app.Run();