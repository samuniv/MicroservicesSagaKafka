using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Models;

namespace PaymentService.Infrastructure.Data;

public class PaymentDbSeeder
{
    private readonly PaymentDbContext _context;

    public PaymentDbSeeder(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        // Ensure database is created and migrations are applied
        await _context.Database.MigrateAsync();

        // Check if there's any data
        if (await _context.Payments.AnyAsync())
        {
            return; // Database has been seeded
        }

        // Add sample payments for development
        var completedPayment = new Payment(Guid.NewGuid(), 100.00m);
        completedPayment.Process("DEV-TRANS-001");
        completedPayment.Complete();

        var processingPayment = new Payment(Guid.NewGuid(), 250.50m);
        processingPayment.Process("DEV-TRANS-002");

        var failedPayment = new Payment(Guid.NewGuid(), 75.25m);
        failedPayment.Process("DEV-TRANS-003");
        failedPayment.Fail();

        var samplePayments = new List<Payment>
        {
            completedPayment,
            processingPayment,
            failedPayment
        };

        await _context.Payments.AddRangeAsync(samplePayments);
        await _context.SaveChangesAsync();
    }
} 