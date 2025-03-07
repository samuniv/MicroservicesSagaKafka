using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using System.Text;
using System.Text.Json;

namespace PaymentService.Tests.Performance;

public class PaymentServiceLoadTests
{
    private const string BaseUrl = "http://localhost:5002";
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

    [Fact(Skip = "Run manually for performance testing")]
    public void ProcessPayment_LoadTest()
    {
        var httpClient = new HttpClient();

        // Define the scenario
        var scenario = Scenario.Create("process_payment_scenario", async context =>
        {
            var payment = new
            {
                OrderId = Guid.NewGuid(),
                Amount = 99.99m,
                Currency = "USD",
                PaymentMethod = "CreditCard",
                PaymentDetails = new
                {
                    CardNumber = "4111111111111111",
                    ExpiryMonth = 12,
                    ExpiryYear = 2025,
                    CVV = "123"
                }
            };

            var json = JsonSerializer.Serialize(payment, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{BaseUrl}/api/payments", content);
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 40,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(1))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    [Fact(Skip = "Run manually for performance testing")]
    public void GetPaymentStatus_LoadTest()
    {
        var httpClient = new HttpClient();
        var paymentId = CreateTestPayment(httpClient).GetAwaiter().GetResult();

        var scenario = Scenario.Create("get_payment_status_scenario", async context =>
        {
            var response = await httpClient.GetAsync($"{BaseUrl}/api/payments/{paymentId}");
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 150,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(1))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    [Fact(Skip = "Run manually for performance testing")]
    public void ProcessRefund_LoadTest()
    {
        var httpClient = new HttpClient();
        var paymentId = CreateAndCompleteTestPayment(httpClient).GetAwaiter().GetResult();

        var scenario = Scenario.Create("process_refund_scenario", async context =>
        {
            var refund = new
            {
                Amount = 99.99m,
                Reason = "Customer Request"
            };

            var json = JsonSerializer.Serialize(refund, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{BaseUrl}/api/payments/{paymentId}/refund", content);
            
            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 20,
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(1))
        );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    private static async Task<Guid> CreateTestPayment(HttpClient httpClient)
    {
        var payment = new
        {
            OrderId = Guid.NewGuid(),
            Amount = 99.99m,
            Currency = "USD",
            PaymentMethod = "CreditCard",
            PaymentDetails = new
            {
                CardNumber = "4111111111111111",
                ExpiryMonth = 12,
                ExpiryYear = 2025,
                CVV = "123"
            }
        };

        var json = JsonSerializer.Serialize(payment, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{BaseUrl}/api/payments", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var createdPayment = JsonSerializer.Deserialize<PaymentResponse>(responseJson, JsonOptions);
        
        return createdPayment?.Id ?? throw new Exception("Failed to create test payment");
    }

    private static async Task<Guid> CreateAndCompleteTestPayment(HttpClient httpClient)
    {
        var paymentId = await CreateTestPayment(httpClient);
        
        // Complete the payment
        await httpClient.PostAsync($"{BaseUrl}/api/payments/{paymentId}/complete", null);
        
        return paymentId;
    }

    private class PaymentResponse
    {
        public Guid Id { get; set; }
    }
} 