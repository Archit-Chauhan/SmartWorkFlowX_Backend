using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartWorkFlowX.Api.Hubs;
using SmartWorkFlowX.Api.Middleware;
using SmartWorkFlowX.Api.Services;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;
using SmartWorkFlowX.Infrastructure.Repositories;
using SmartWorkFlowX.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Services Configuration ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ DB with retry (CRITICAL FIX)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<SmartWorkflowXDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

// ─── Domain Repository Registrations ────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IRoleRepository, EfRoleRepository>();
builder.Services.AddScoped<IWorkflowRepository, EfWorkflowRepository>();
builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<INotificationRepository, EfNotificationRepository>();
builder.Services.AddScoped<IReportRepository, EfReportRepository>();

// ─── Application Service Registrations ──────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<INotificationQueryService, NotificationQueryService>();

// Notification decorator
builder.Services.AddScoped<DbNotificationService>();
builder.Services.AddScoped<INotificationService>(sp =>
    new SignalRNotificationDecorator(
        sp.GetRequiredService<DbNotificationService>(),
        sp.GetRequiredService<IHubContext<NotificationHub, INotificationClient>>()));

// Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "https://smart-work-flow-x-frontend.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ---------------- BUILD ----------------

var app = builder.Build();

// ---------------- SAFE DB INIT (NON-BLOCKING) ----------------

// 🔥 THIS IS THE KEY CHANGE
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SmartWorkflowXDbContext>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        logger.LogInformation("Starting DB migration...");

        await db.Database.MigrateAsync();

        logger.LogInformation("Seeding database...");

        await DbSeeder.SeedAsync(db, auth);

        logger.LogInformation("DB migration & seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DB init failed. App will continue running.");
    }
});

// ---------------- MIDDLEWARE ----------------

app.UseGlobalExceptionHandler();

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        time = DateTime.UtcNow
    });
});

app.MapControllers();

app.Run();