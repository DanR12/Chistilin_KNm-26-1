using OrderFlow.Domain;

namespace OrderFlow.Infrastructure;

public static class SeedData
{
    public static void EnsureSeeded(OrderFlowDbContext db, PricingRules rules, MidpointRounding invoiceRounding = MidpointRounding.AwayFromZero)
    {
        if (db.Products.Any()) return;

        var products = new List<Product>
        {
            new() { Sku = "STA-001", Name = "Paper clip", Price = 0.10m, Stock = 500 },
            new() { Sku = "STA-002", Name = "Rubber band", Price = 0.20m, Stock = 400 },
            new() { Sku = "STA-003", Name = "Pencil HB", Price = 0.70m, Stock = 300 },
            new() { Sku = "STA-004", Name = "Ballpoint pen", Price = 1.20m, Stock = 250 },
            new() { Sku = "STA-005", Name = "Notebook A5", Price = 3.50m, Stock = 120 },
            new() { Sku = "OFF-101", Name = "Stapler", Price = 12.90m, Stock = 40 },
            new() { Sku = "OFF-102", Name = "Laptop stand", Price = 39.90m, Stock = 2 },
            new() { Sku = "OFF-103", Name = "Desk lamp", Price = 45.00m, Stock = 25 },
            new() { Sku = "FUR-201", Name = "Whiteboard 120x90", Price = 149.00m, Stock = 12 },
            new() { Sku = "FUR-202", Name = "Office chair", Price = 189.00m, Stock = 15 },
            new() { Sku = "FUR-203", Name = "Monitor 27\"", Price = 329.00m, Stock = 9 },
            new() { Sku = "FUR-204", Name = "Standing desk", Price = 499.00m, Stock = 10 },
        };
        db.Products.AddRange(products);

        var alice = new Customer { Name = "Alice Koval", Email = "alice@example.com", Address = "Khreshchatyk St 22, apt 5", City = "Kyiv", Country = "Ukraine", Tier = CustomerTier.VIP };
        var bob = new Customer { Name = "Bob Melnyk", Email = "bob@example.com", Address = "Svobody Ave 10", City = "Lviv", Country = "Ukraine", Tier = CustomerTier.Regular };
        var carol = new Customer { Name = "Carol Shevchenko", Email = "carol@example.com", Address = "Derybasivska St 3", City = "Odesa", Country = "Ukraine", Tier = CustomerTier.Regular };
        db.Customers.AddRange(alice, bob, carol);
        db.SaveChanges();

        db.Users.AddRange(
            new UserAccount { Username = "manager", Password = "manager", Role = "manager" },
            new UserAccount { Username = "alice", Password = "alice", Role = "customer", CustomerId = alice.Id },
            new UserAccount { Username = "bob", Password = "bob", Role = "customer", CustomerId = bob.Id },
            new UserAccount { Username = "carol", Password = "carol", Role = "customer", CustomerId = carol.Id });

        Product P(string sku) => products.First(p => p.Sku == sku);
        var seq = 0;
        Order Make(Customer c, DateTime createdUtc, OrderStatus status, string? promo, params (Product Product, int Qty)[] lines)
        {
            seq++;
            var o = new Order
            {
                Customer = c, CustomerId = c.Id, Status = status, PromoCode = promo,
                CreatedAtUtc = createdUtc, Number = $"OF-{createdUtc:yyyyMMdd}-{createdUtc:HHmmss}{10 + seq}",
            };
            foreach (var (p, qty) in lines)
                o.Lines.Add(new OrderLine { Product = p, ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = qty });
            if (status >= OrderStatus.Placed && status != OrderStatus.Cancelled) o.PlacedAtUtc = createdUtc.AddMinutes(3);
            if (status is OrderStatus.Paid or OrderStatus.Shipped) o.PaidAtUtc = createdUtc.AddHours(2);
            if (status == OrderStatus.Shipped) o.ShippedAtUtc = createdUtc.AddDays(1);
            if (status == OrderStatus.Cancelled) { o.PlacedAtUtc = createdUtc.AddMinutes(3); o.CancelledAtUtc = createdUtc.AddHours(5); }
            if (status is OrderStatus.Paid or OrderStatus.Shipped)
            {
                var promoApplies = false; // seeded orders never use the first-order code
                var t = Pricing.Calculate(o.Lines, c.Tier, promoApplies, rules, invoiceRounding);
                o.Invoice = new Invoice
                {
                    Number = "INV-" + o.Number[3..], IssuedAtUtc = o.PaidAtUtc!.Value,
                    BillToName = c.Name, BillToAddress = c.FullAddress,
                    Subtotal = t.Subtotal, Discount = t.Discount, Shipping = t.Shipping, Net = t.Net, Vat = t.Vat, Total = t.Total,
                };
            }
            return o;
        }

        var d = new DateTime(2026, 8, 12, 9, 30, 12, DateTimeKind.Utc);
        db.Orders.AddRange(
            Make(alice, d.AddDays(0), OrderStatus.Shipped, null, (P("FUR-202"), 2), (P("OFF-103"), 1)),
            Make(bob, d.AddDays(1).AddHours(2), OrderStatus.Paid, null, (P("FUR-204"), 1), (P("FUR-203"), 1)),
            Make(carol, d.AddDays(2).AddHours(5), OrderStatus.Shipped, null, (P("STA-005"), 3), (P("STA-004"), 10)),
            Make(alice, d.AddDays(4), OrderStatus.Placed, null, (P("FUR-204"), 2), (P("OFF-101"), 1)),
            Make(bob, d.AddDays(5).AddHours(7), OrderStatus.Cancelled, null, (P("FUR-201"), 1)),
            Make(carol, d.AddDays(7).AddHours(1), OrderStatus.Paid, null, (P("FUR-203"), 4)),
            Make(bob, d.AddDays(9).AddHours(3), OrderStatus.Shipped, null, (P("OFF-102"), 1), (P("OFF-103"), 1)),
            Make(alice, d.AddDays(11).AddHours(6), OrderStatus.Placed, null, (P("STA-003"), 20)),
            Make(carol, d.AddDays(13).AddHours(2), OrderStatus.Paid, null, (P("FUR-202"), 1)),
            Make(bob, d.AddDays(15).AddHours(4), OrderStatus.Placed, "WELCOME", (P("OFF-101"), 1)),
            Make(alice, d.AddDays(17).AddHours(1), OrderStatus.Shipped, null, (P("FUR-201"), 5)),
            Make(carol, d.AddDays(19).AddHours(8), OrderStatus.Draft, null, (P("STA-004"), 1)),
            Make(bob, d.AddDays(21).AddHours(2), OrderStatus.Placed, null, (P("FUR-204"), 1), (P("FUR-201"), 1), (P("FUR-202"), 2)),
            Make(alice, d.AddDays(23).AddHours(5), OrderStatus.Placed, null, (P("FUR-204"), 6)),
            Make(carol, d.AddDays(25).AddHours(3), OrderStatus.Paid, null, (P("STA-001"), 3), (P("STA-002"), 3)),
            Make(alice, d.AddDays(27).AddHours(4), OrderStatus.Draft, null),
            // release 1.1: five more demo orders
            Make(alice, d.AddDays(28).AddHours(2), OrderStatus.Paid, null, (P("FUR-204"), 2), (P("STA-001"), 23)),
            Make(alice, d.AddDays(29).AddHours(6), OrderStatus.Shipped, null, (P("FUR-203"), 3), (P("OFF-101"), 1), (P("STA-004"), 1)),
            Make(alice, d.AddDays(30).AddHours(1), OrderStatus.Paid, null, (P("FUR-204"), 1), (P("FUR-203"), 1), (P("FUR-202"), 1), (P("STA-003"), 1), (P("STA-002"), 1)),
            Make(alice, d.AddDays(31).AddHours(3), OrderStatus.Shipped, null, (P("FUR-204"), 2), (P("OFF-101"), 1)),
            Make(carol, d.AddDays(32).AddHours(5), OrderStatus.Paid, null, (P("OFF-103"), 1), (P("STA-005"), 2))
        );
        db.SaveChanges();
    }
}
