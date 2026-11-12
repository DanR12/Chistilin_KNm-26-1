using OrderFlow.Domain;

namespace OrderFlow.Domain.Tests;

/// <summary>The order most of the tests work with: four lines, subtotal 1,200.00.</summary>
public class StandardOrder
{
    public PricingRules Rules { get; } = new();

    public List<OrderLine> Lines { get; } = new()
    {
        new OrderLine { ProductName = "Standing desk", UnitPrice = 499.00m, Quantity = 2 },
        new OrderLine { ProductName = "Office chair", UnitPrice = 189.00m, Quantity = 1 },
        new OrderLine { ProductName = "Stapler", UnitPrice = 12.90m, Quantity = 1 },
        new OrderLine { ProductName = "Paper clip", UnitPrice = 0.10m, Quantity = 1 },
    };
}

public class PricingTests : IClassFixture<StandardOrder>
{
    private readonly StandardOrder _order;

    public PricingTests(StandardOrder order)
    {
        _order = order;
    }

    [Fact]
    public void Discounts()
    {
        var rules = new PricingRules();
        var lines = new List<OrderLine>
        {
            new OrderLine { ProductName = "Standing desk", UnitPrice = 499.00m, Quantity = 2 },
            new OrderLine { ProductName = "Office chair", UnitPrice = 189.00m, Quantity = 1 },
            new OrderLine { ProductName = "Stapler", UnitPrice = 12.90m, Quantity = 1 },
            new OrderLine { ProductName = "Paper clip", UnitPrice = 0.10m, Quantity = 1 },
        };

        var regular = Pricing.Calculate(lines, CustomerTier.Regular, false, rules);
        Assert.Equal(120m, regular.Discount);

        var vip = Pricing.Calculate(lines, CustomerTier.VIP, false, rules);
        Assert.Equal(180m, vip.Discount);

        var vipWithPromo = Pricing.Calculate(lines, CustomerTier.VIP, true, rules);
        Assert.Equal(240m, vipWithPromo.Discount);
    }

    [Fact]
    public void Shipping_test()
    {
        var rules = new PricingRules();

        foreach (var amount in new[] { 100m, 1500m, 2500m, 3000m })
        {
            var shipping = Pricing.ShippingFor(amount, rules);
            if (amount > 2000m)
                Assert.Equal(0m, shipping);
            else
                Assert.Equal(50m, shipping);
        }
    }

    [Fact]
    public void Adding_a_line()
    {
        _order.Lines.Add(new OrderLine { ProductName = "Paper clip", UnitPrice = 0.10m, Quantity = 1 });

        Assert.Equal(1200.10m, Pricing.Subtotal(_order.Lines));
    }

    [Fact]
    public void DiscountRate_Sub1200_VIP_Promo_Returns20()
    {
        var r = new PricingRules();
        var d = Pricing.DiscountRate(1200m, CustomerTier.VIP, true, r);
        Assert.Equal(0.2m, d);
    }

    [Fact]
    public void Totals_are_computed()
    {
        var totals = Pricing.Calculate(_order.Lines, CustomerTier.Regular, false, _order.Rules);

        Assert.True(totals.Total > 0);
        Assert.True(totals.Total > totals.Net);
        Assert.NotEqual(0m, totals.Discount);
    }
}
