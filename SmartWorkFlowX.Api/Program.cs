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

// ---------------- SERVICES ----------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SmartWorkFlowX API", Version = "v1" });
});

// ✅ DB with retry (fixes Azure SQL cold start issue)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<SmartWorkflowXDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

// ---------------- REPOSITORIES ----------------

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IRoleRepository, EfRoleRepository>();
builder.Services.AddScoped<IWorkflowRepository, EfWorkflowRepository>();
builder.Services.AddScoped<ITaskRepository, EfTaskRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<INotificationRepository, EfNotificationRepository>();
builder.Services.AddScoped<IReportRepository, EfReportRepository>();

// ---------------- SERVICES ----------------

// ✅ Fully qualified to avoid namespace conflict
builder.Services.AddScoped<IAuthService, SmartWorkFlowX.Infrastructure.services.AuthService>();

builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<INotificationQueryService, NotificationQueryService>();

// Notification pipeline
builder.Services.AddScoped<DbNotificationService>();
builder.Services.AddScoped<INotificationService>(sp =>
    new SignalRNotificationDecorator(
        sp.GetRequiredService<DbNotificationService>(),
        sp.GetRequiredService<IHubContext<NotificationHub, INotificationClient>>()));

// ---------------- AZURE SERVICE BUS & WORKERS ----------------

builder.Services.AddSingleton(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    var sbConnStr = config["AzureServiceBus:ConnectionString"] ?? throw new InvalidOperationException("Missing Service Bus Connection String");
    // In dev, you might want to return null or a dummy client if the connection string is a placeholder
    return new Azure.Messaging.ServiceBus.ServiceBusClient(sbConnStr);
});

builder.Services.AddScoped<IMessagePublisher, ServiceBusMessagePublisher>();

builder.Services.AddHostedService<SmartWorkFlowX.Api.Workers.WorkflowAuditWorker>();
builder.Services.AddHostedService<SmartWorkFlowX.Api.Workers.WorkflowNotificationWorker>();
builder.Services.AddHostedService<SmartWorkFlowX.Api.Workers.BulkNotificationWorker>();

// ---------------- AUTH ----------------

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
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------- SIGNALR ----------------

builder.Services.AddSignalR();

// ---------------- CORS ----------------

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

// 🔥 Runs in background → app won't crash if DB is down
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SmartWorkflowXDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        logger.LogInformation("Starting database migration...");

        // optional retry loop (extra safety)
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await dbContext.Database.MigrateAsync();
                break;
            }
            catch
            {
                await Task.Delay(5000);
            }
        }

        logger.LogInformation("Seeding database...");

        await DbSeeder.SeedAsync(dbContext, authService);

        logger.LogInformation("Database migration & seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database init failed. App continues running.");
    }
});

// ---------------- MIDDLEWARE ----------------

app.UseGlobalExceptionHandler();

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartWorkFlowX API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// SignalR
app.MapHub<NotificationHub>("/hubs/notifications");

// Health check
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow
    });
});

app.MapControllers();

app.Run();