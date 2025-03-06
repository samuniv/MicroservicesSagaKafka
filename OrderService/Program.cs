using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.BackgroundServices;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Mapping;
using OrderService.Infrastructure.MessageBus;
using OrderService.Infrastructure.RateLimiting;
using OrderService.Infrastructure.Repositories;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/order-service-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] [{MachineName}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

// Configure services
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithMachineName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/order-service-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] [{MachineName}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// Add services to the container.
builder.Services.AddControllers();

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(OrderMappingProfile));

// Configure Response Caching
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 32 * 1024; // 32KB
    options.UseCaseSensitivePaths = false;
});

// Configure Rate Limiting
builder.Services.Configure<RateLimitingSettings>(
    builder.Configuration.GetSection("RateLimiting"));

// Configure Kafka
builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddHostedService<KafkaConsumerService>();

// Add Saga Orchestrator
builder.Services.AddScoped<OrderCreationSaga>();

// Add DbContext
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// Add Repositories
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Register background services
builder.Services.AddHostedService<OrderCleanupService>();
builder.Services.AddHostedService<OrderNotificationService>();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Order Service API", 
        Version = "v1",
        Description = "Order Service for Microservices Saga Pattern implementation"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service API V1");
        c.RoutePrefix = string.Empty; // Serve the Swagger UI at the app's root
    });

    // Initialize and seed the database in development
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<OrderDbContext>();
            var seeder = new PaymentDbSeeder(context);
            await seeder.SeedAsync();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
}

app.UseHttpsRedirection();
app.UseResponseCaching();
app.UseAuthorization();
app.MapControllers();

try
{
    Log.Information("Starting Order Service");
    
    // Initialize the database
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        try
        {
            if (context.Database.EnsureCreated())
            {
                Log.Information("Database created successfully");
            }
            
            if (context.Database.GetPendingMigrations().Any())
            {
                Log.Information("Applying pending migrations");
                context.Database.Migrate();
                Log.Information("Migrations applied successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while initializing the database");
            throw;
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
