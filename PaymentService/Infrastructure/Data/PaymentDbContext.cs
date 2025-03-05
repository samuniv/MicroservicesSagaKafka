using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Models;

namespace PaymentService.Infrastructure.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.OrderId)
                .IsRequired();

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(e => e.TransactionId)
                .HasMaxLength(100);

            entity.Property(e => e.ProcessedAt);

            // Create index for OrderId for faster lookups
            entity.HasIndex(e => e.OrderId);

            // Create index for TransactionId
            entity.HasIndex(e => e.TransactionId)
                .IsUnique()
                .HasFilter("[TransactionId] IS NOT NULL");
        });

        base.OnModelCreating(modelBuilder);
    }
} 