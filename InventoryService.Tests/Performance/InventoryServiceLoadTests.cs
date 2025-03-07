using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using System.Text;
using System.Text.Json;

namespace InventoryService.Tests.Performance;

public class InventoryServiceLoadTests
{
    private const string BaseUrl = "http://localhost:5001";
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

    [Fact(Skip = "Run manually for performance testing")]
    public void ReserveStock_LoadTest()
    {
        var httpClient = new HttpClient();

        // Define the scenario
        var scenario = Scenario.Create("reserve_stock_scenario", async context =>
        {
            var reservation = new
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                OrderId = Guid.NewGuid()
            };

            var json = JsonSerializer.Serialize(reservation, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{BaseUrl}/api/inventory/reserve", content);
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 75,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(1))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    [Fact(Skip = "Run manually for performance testing")]
    public void UpdateStock_LoadTest()
    {
        var httpClient = new HttpClient();
        var productId = CreateTestProduct(httpClient).GetAwaiter().GetResult();

        var scenario = Scenario.Create("update_stock_scenario", async context =>
        {
            var update = new
            {
                Quantity = 100,
                Reason = "Restock"
            };

            var json = JsonSerializer.Serialize(update, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PutAsync($"{BaseUrl}/api/inventory/{productId}/stock", content);
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 50,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(1))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    [Fact(Skip = "Run manually for performance testing")]
    public void GetInventoryLevels_LoadTest()
    {
        var httpClient = new HttpClient();

        var scenario = Scenario.Create("get_inventory_levels", async context =>
        {
            var response = await httpClient.GetAsync($"{BaseUrl}/api/inventory");
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 200,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(2))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    private static async Task<Guid> CreateTestProduct(HttpClient httpClient)
    {
        var product = new
        {
            Name = "Test Product",
            SKU = $"SKU-{Guid.NewGuid()}",
            Price = 29.99m,
            InitialStock = 1000
        };

        var json = JsonSerializer.Serialize(product, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{BaseUrl}/api/inventory", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var createdProduct = JsonSerializer.Deserialize<ProductResponse>(responseJson, JsonOptions);
        
        return createdProduct?.Id ?? throw new Exception("Failed to create test product");
    }

    private class ProductResponse
    {
        public Guid Id { get; set; }
    }
} 