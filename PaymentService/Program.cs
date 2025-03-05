using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Repositories;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Data;
using PaymentService.Infrastructure.MessageBus;
using PaymentService.Infrastructure.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    SwaggerConfiguration.ConfigureSwagger(options);
});

// Add Authentication & Authorization
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization(options =>
{
    Policies.AddDlqPolicies(options);
});

// Configure rate limiting
builder.Services.Configure<RateLimitingSettings>(
    builder.Configuration.GetSection("RateLimiting"));

// Configure DbContext
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// Register services
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddSingleton<DlqMonitoringService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DlqMonitoringService>());
builder.Services.AddHostedService<DlqCleanupService>();

// Configure Kafka
builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("KafkaSettings"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "api-docs/{documentName}/swagger.json";
    });
    
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api-docs/v1/swagger.json", "Payment Service API V1");
        c.RoutePrefix = "api-docs";
        c.DocumentTitle = "Payment Service API Documentation";
        c.EnableDeepLinking();
        c.DisplayRequestDuration();
    });

    // Initialize and seed the database in development
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<PaymentDbContext>();
            var seeder = new PaymentDbSeeder(context);
            seeder.SeedAsync().Wait();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
