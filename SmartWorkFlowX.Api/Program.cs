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
using SmartWorkFlowX.Infrastructure.services;
using SmartWorkFlowX.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Services Configuration ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SmartWorkFlowX API", Version = "v1" });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SmartWorkflowXDbContext>(options =>
    options.UseSqlServer(connectionString));

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

// ─── Notification Pipeline (DB + SignalR Decorator) ─────────────────────────
builder.Services.AddScoped<DbNotificationService>(); // inner: DB persistence
builder.Services.AddScoped<INotificationService>(sp =>
    new SignalRNotificationDecorator(
        sp.GetRequiredService<DbNotificationService>(),
        sp.GetRequiredService<IHubContext<NotificationHub, INotificationClient>>()));

// JWT Authentication
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

        // Allow SignalR to receive the JWT token from query string
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

// SignalR
builder.Services.AddSignalR();

// CORS — AllowCredentials is required for SignalR WebSocket handshake
builder.Services.AddCors(options => {
    options.AddPolicy("AllowDevelopment", policy => {
        policy.WithOrigins(
                  "http://localhost:3000", 
                  "http://localhost:5173", 
                  "https://smart-work-flow-x-frontend.vercel.app"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// --- Apply Database Migrations Automatically ---
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SmartWorkflowXDbContext>();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    
    dbContext.Database.Migrate();
    
    // Seed default roles and admin user
    SmartWorkFlowX.Infrastructure.Data.DbSeeder.SeedAsync(dbContext, authService).GetAwaiter().GetResult();
}

// --- 2. Middleware Pipeline ---
app.UseGlobalExceptionHandler(); // Must be first

app.UseCors("AllowDevelopment");

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

// SignalR Hub endpoint
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapControllers();

app.Run();

