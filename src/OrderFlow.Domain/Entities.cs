namespace OrderFlow.Domain;

public enum CustomerTier { Regular = 0, VIP = 1 }

public enum OrderStatus { Draft = 0, Placed = 1, Paid = 2, Shipped = 3, Cancelled = 4 }

public class DomainException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public CustomerTier Tier { get; set; }

    public string FullAddress => $"{Address}, {City}, {Country}";
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class Order
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public string? PromoCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PlacedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public Invoice? Invoice { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string BillToName { get; set; } = "";
    public string BillToAddress { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Total { get; set; }
}
