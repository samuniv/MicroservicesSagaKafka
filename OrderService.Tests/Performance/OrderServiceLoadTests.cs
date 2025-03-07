using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using System.Text;
using System.Text.Json;

namespace OrderService.Tests.Performance;

public class OrderServiceLoadTests
{
    private const string BaseUrl = "http://localhost:5000";
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

    [Fact(Skip = "Run manually for performance testing")]
    public void CreateOrder_LoadTest()
    {
        var httpClient = new HttpClient();

        // Define the scenario
        var scenario = Scenario.Create("create_order_scenario", async context =>
        {
            var order = new
            {
                CustomerId = Guid.NewGuid().ToString(),
                Items = new[]
                {
                    new
                    {
                        ProductId = Guid.NewGuid(),
                        Quantity = 1,
                        Price = 10.00m
                    }
                }
            };

            var json = JsonSerializer.Serialize(order, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{BaseUrl}/api/orders", content);
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 50,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(1))
        );

        // Run the scenario
        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    [Fact(Skip = "Run manually for performance testing")]
    public void GetOrder_LoadTest()
    {
        var httpClient = new HttpClient();

        // Create a test order first
        var orderId = CreateTestOrder(httpClient).GetAwaiter().GetResult();

        // Define the scenario
        var scenario = Scenario.Create("get_order_scenario", async context =>
        {
            var response = await httpClient.GetAsync($"{BaseUrl}/api/orders/{orderId}");
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 100,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(1))
        );

        // Run the scenario
        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    [Fact(Skip = "Run manually for performance testing")]
    public void CompleteOrderFlow_LoadTest()
    {
        var httpClient = new HttpClient();

        // Define the scenario
        var scenario = Scenario.Create("complete_order_flow", async context =>
        {
            try
            {
                // Step 1: Create Order
                var orderId = await CreateTestOrder(httpClient);

                // Step 2: Wait for Inventory Reservation (simulated)
                await Task.Delay(100);

                // Step 3: Check Order Status
                var response = await httpClient.GetAsync($"{BaseUrl}/api/orders/{orderId}");
                
                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: (int)response.StatusCode)
                    : Response.Fail(statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Response.Fail(error: ex.Message);
            }
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 10,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(5))
        );

        // Run the scenario
        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    private static async Task<Guid> CreateTestOrder(HttpClient httpClient)
    {
        var order = new
        {
            CustomerId = Guid.NewGuid().ToString(),
            Items = new[]
            {
                new
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1,
                    Price = 10.00m
                }
            }
        };

        var json = JsonSerializer.Serialize(order, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{BaseUrl}/api/orders", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var createdOrder = JsonSerializer.Deserialize<OrderResponse>(responseJson, JsonOptions);
        
        return createdOrder?.Id ?? throw new Exception("Failed to create test order");
    }

    private class OrderResponse
    {
        public Guid Id { get; set; }
    }
} 