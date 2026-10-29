using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain;

namespace OrderFlow.Infrastructure;

public class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "customer";
    public int? CustomerId { get; set; }
}

public class OrderFlowDbContext(DbContextOptions<OrderFlowDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        b.Entity<Order>().HasIndex(o => o.Number);
        b.Entity<Order>().HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Order>().HasOne(o => o.Invoice).WithOne(i => i.Order).HasForeignKey<Invoice>(i => i.OrderId);
        b.Entity<Order>().HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId);
        b.Entity<OrderLine>().HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId);
        b.Entity<UserAccount>().HasIndex(u => u.Username).IsUnique();
    }
}
