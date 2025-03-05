using InventoryService.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Data;

public class InventoryDbContext : DbContext
{
    public DbSet<InventoryItem> InventoryItems { get; set; }

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.ProductId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.SKU)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 2);

            entity.HasIndex(e => e.ProductId)
                .IsUnique();

            entity.HasIndex(e => e.SKU)
                .IsUnique();
        });
    }
} 