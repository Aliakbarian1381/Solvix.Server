using Solvix.Server.Hubs;
using Microsoft.EntityFrameworkCore;
using Solvix.Server.Data;
using Solvix.Server.Services;
using Microsoft.AspNetCore.Identity;
using Solvix.Server.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using System.Security.Claims;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IUserService, UserService>();
//builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserConnectionService, UserConnectionService>();
builder.Services.AddIdentity<AppUser, IdentityRole<long>>()
    .AddEntityFrameworkStores<ChatDbContext>()
    .AddDefaultTokenProviders();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();


builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 1;
    options.Password.RequiredUniqueChars = 1;

    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
});

var jwtKey = builder.Configuration["Jwt:Key"];
Console.WriteLine("📢 JWT KEY: " + jwtKey);

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
    ValidIssuer = builder.Configuration["Jwt:Issuer"], 
    ValidAudience = builder.Configuration["Jwt:Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    ClockSkew = TimeSpan.Zero
};

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var x = context.Exception.Message;
            Console.WriteLine("JWT FAILED: " + x);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("✅ TOKEN VALIDATED for user: " +
                context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            if (context.HttpContext.Request.Path.StartsWithSegments("/chathub"))
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
            }
            return Task.CompletedTask;
        }
    };
});


// CORS Configuration
//var clientAppUrl = builder.Configuration["ClientAppUrl"] ?? "https://localhost:7001"; // آدرس کلاینت خود را اینجا یا در appsettings قرار دهید

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowClientApp", // یک نام برای Policy انتخاب کنید
//               corsBuilder => // نام متغیر را به corsBuilder تغییر دادم
//               {
//                   corsBuilder.WithOrigins(clientAppUrl) // آدرس دقیق کلاینت
//                          .AllowAnyMethod()
//                          .AllowAnyHeader()
//                          .AllowCredentials(); // <<-- **بسیار مهم برای ارسال کوکی‌ها با SignalR**
//               });
//});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
               builder =>
               {
                   builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
               });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//app.UseCors("AllowClientApp"); // <<-- فعال کردن Policy

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("/chathub");

app.Run();
