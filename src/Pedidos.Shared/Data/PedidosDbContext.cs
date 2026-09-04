using Microsoft.EntityFrameworkCore;
using Pedidos.Shared.Models;

namespace Pedidos.Shared.Data;

public class PedidosDbContext : DbContext
{
    public PedidosDbContext(DbContextOptions<PedidosDbContext> options) : base(options)
    {
    }

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("pedidos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Cliente).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Producto).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.RedisMessageId).HasMaxLength(50);
            entity.HasIndex(p => p.Estado);
        });
    }
}
