using OrderFlow.Domain;

namespace OrderFlow.Application;

public sealed class OrderService(IOrderFlowStore store, IEmailSender email, IClock clock, VariantOptions variant, PricingRules rules,
    StockGate stockGate)
{
    public PricingRules Rules => rules;

    /// <summary>Time the order-screening step takes before a placed order is confirmed.</summary>
    private static readonly TimeSpan ScreeningDelay = TimeSpan.FromMilliseconds(1200);

    public async Task<Order> CreateAsync(int customerId, string? promoCode, IEnumerable<(int ProductId, int Quantity)> lines,
        bool validateQuantity, CancellationToken ct = default)
    {
        var customer = await store.GetCustomerAsync(customerId, ct) ?? throw new NotFoundException("Customer not found.");
        var now = clock.UtcNow;
        var order = new Order
        {
            CustomerId = customer.Id,
            Customer = customer,
            Status = OrderStatus.Draft,
            PromoCode = string.IsNullOrWhiteSpace(promoCode) ? null : promoCode.Trim().ToUpperInvariant(),
            CreatedAtUtc = now,
            Number = NewNumber(now),
        };
        foreach (var (productId, quantity) in lines)
            await AddLineCoreAsync(order, productId, quantity, validateQuantity, ct);
        store.Add(order);
        await store.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> AddLineAsync(int orderId, int productId, int quantity, bool validateQuantity, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderId, ct);
        if (order.Status != OrderStatus.Draft)
            throw new DomainException("Lines can only be added to a draft order.");
        await AddLineCoreAsync(order, productId, quantity, validateQuantity, ct);
        await store.SaveChangesAsync(ct);
        return order;
    }

    private async Task AddLineCoreAsync(Order order, int productId, int quantity, bool validateQuantity, CancellationToken ct)
    {
        if (validateQuantity && quantity < 1)
            throw new DomainException("Quantity must be at least 1.");
        var product = await store.GetProductAsync(productId, ct) ?? throw new NotFoundException("Product not found.");
        var existing = order.Lines.FirstOrDefault(l => l.ProductId == productId);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            return;
        }
        order.Lines.Add(new OrderLine
        {
            ProductId = product.Id,
            Product = product,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = quantity,
        });
    }

    public async Task<Order> PlaceAsync(int orderId, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderId, ct);
        await PlaceAndSaveAsync(order, ct);
        return order;
    }

    /// <summary>Create and place in one step (the web form).</summary>
    public async Task<Order> CreateAndPlaceAsync(int customerId, string? promoCode, IEnumerable<(int ProductId, int Quantity)> lines,
        CancellationToken ct = default)
    {
        var order = await CreateAsync(customerId, promoCode, lines, validateQuantity: true, ct);
        await PlaceAndSaveAsync(order, ct);
        return order;
    }

    /// <summary>
    /// Stock is checked and reduced inside one critical section, so that two orders for the last units
    /// cannot both pass the check. The legacy path (release 1.0 behaviour) runs the screening step between
    /// the check and the write without the gate.
    /// </summary>
    private async Task PlaceAndSaveAsync(Order order, CancellationToken ct)
    {
        if (variant.PlaceWithoutStockLock)
        {
            await PlaceCoreAsync(order, ct);
            await Task.Delay(ScreeningDelay, ct);
            await store.SaveChangesAsync(ct);
            return;
        }
        await stockGate.WaitAsync(ct);
        try
        {
            await PlaceCoreAsync(order, ct);
            await store.SaveChangesAsync(ct);
        }
        finally
        {
            stockGate.Release();
        }
    }

    private async Task PlaceCoreAsync(Order order, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Draft)
            throw new DomainException("Only a draft order can be placed.");
        if (order.Lines.Count == 0)
            throw new DomainException("An order needs at least one line before it can be placed.");
        foreach (var line in order.Lines)
        {
            var product = line.Product ?? await store.GetProductAsync(line.ProductId, ct)
                ?? throw new NotFoundException("Product not found.");
            if (!variant.SkipStockCheck && line.Quantity > product.Stock)
                throw new DomainException($"Not enough stock for {product.Name}: {product.Stock} available.");
            product.Stock -= line.Quantity;
        }
        order.Status = OrderStatus.Placed;
        order.PlacedAtUtc = clock.UtcNow;
        var customer = order.Customer ?? await store.GetCustomerAsync(order.CustomerId, ct);
        if (customer is not null)
            await email.SendAsync(customer.Email, $"Order {order.Number} confirmed",
                $"Dear {customer.Name}, your order {order.Number} has been placed.", ct);
    }

    public async Task<Order> PayAsync(int orderId, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderId, ct);
        if (order.Status != OrderStatus.Placed)
            throw new DomainException("Only a placed order can be paid.");
        var totals = await InvoiceTotalsAsync(order, ct);
        var now = clock.UtcNow;
        var customer = order.Customer!;
        order.Status = OrderStatus.Paid;
        order.PaidAtUtc = now;
        order.Invoice = new Invoice
        {
            Number = "INV-" + order.Number[3..],
            IssuedAtUtc = now,
            BillToName = customer.Name,
            BillToAddress = customer.FullAddress,
            Subtotal = totals.Subtotal,
            Discount = totals.Discount,
            Shipping = totals.Shipping,
            Net = totals.Net,
            Vat = totals.Vat,
            Total = totals.Total,
        };
        await store.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> ShipAsync(int orderId, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderId, ct);
        if (order.Status != OrderStatus.Paid)
            throw new DomainException("Only a paid order can be shipped.");
        order.Status = OrderStatus.Shipped;
        order.ShippedAtUtc = clock.UtcNow;
        await store.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> CancelAsync(int orderId, CancellationToken ct = default)
    {
        var order = await RequireAsync(orderId, ct);
        if (order.Status == OrderStatus.Cancelled)
            throw new DomainException("The order is already cancelled.");
        if (order.Status == OrderStatus.Shipped && !variant.CancelFromAnyStatus)
            throw new DomainException("A shipped order cannot be cancelled.");
        if (order.Status is OrderStatus.Placed or OrderStatus.Paid or OrderStatus.Shipped)
        {
            foreach (var line in order.Lines)
            {
                var product = line.Product ?? await store.GetProductAsync(line.ProductId, ct);
                if (product is not null) product.Stock += line.Quantity;
            }
        }
        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = clock.UtcNow;
        await store.SaveChangesAsync(ct);
        return order;
    }

    public async Task<bool> PromoAppliesAsync(Order order, CancellationToken ct = default)
    {
        if (!string.Equals(order.PromoCode, rules.PromoCode, StringComparison.OrdinalIgnoreCase))
            return false;
        if (variant.PromoOnAnyOrder)
            return true;
        var before = order.PlacedAtUtc ?? clock.UtcNow;
        var earlier = await store.CountPlacedOrdersAsync(order.CustomerId, order.Id, before, ct);
        return earlier == 0;
    }

    /// <summary>Totals as the order pages and the API show them.</summary>
    public async Task<OrderTotals> TotalsAsync(Order order, CancellationToken ct = default)
    {
        var tier = order.Customer?.Tier ?? (await store.GetCustomerAsync(order.CustomerId, ct))?.Tier ?? CustomerTier.Regular;
        return Pricing.Calculate(order.Lines, tier, await PromoAppliesAsync(order, ct), rules);
    }

    /// <summary>Totals as the invoicing module computes them when the invoice is issued.</summary>
    public async Task<OrderTotals> InvoiceTotalsAsync(Order order, CancellationToken ct = default)
    {
        var tier = order.Customer?.Tier ?? (await store.GetCustomerAsync(order.CustomerId, ct))?.Tier ?? CustomerTier.Regular;
        return Pricing.Calculate(order.Lines, tier, await PromoAppliesAsync(order, ct), rules, variant.InvoiceRounding);
    }

    public async Task<Order> RequireAsync(int orderId, CancellationToken ct = default) =>
        await store.GetOrderAsync(orderId, ct) ?? throw new NotFoundException("Order not found.");

    public static string NewNumber(DateTime utc) =>
        $"OF-{utc:yyyyMMdd}-{utc:HHmmss}{Random.Shared.Next(10, 100)}";
}
