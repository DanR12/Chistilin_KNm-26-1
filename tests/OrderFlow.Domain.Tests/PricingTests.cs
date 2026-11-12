using Xunit;
using System.Collections.Generic;

namespace OrderFlow.Domain.Tests;

public class PricingTests
{
    private PricingRules GetDefaultRules() => new PricingRules();
    
    private List<OrderLine> StandardOrder() => new List<OrderLine>
    {
        new OrderLine { Quantity = 2, UnitPrice = 500.00m },
        new OrderLine { Quantity = 1, UnitPrice = 200.10m }
    };

    [Fact]
    public void Subtotal_is_the_sum_of_quantities_multiplied_by_unit_prices()
    {
        // Arrange
        var lines = StandardOrder();

        // Act
        var subtotal = Pricing.Subtotal(lines);

        // Assert
        Assert.Equal(1200.10m, subtotal);
    }

    [Fact]
    public void Order_totals_are_computed_correctly_for_regular_customer_without_promo()
    {
        // Arrange
        var rules = GetDefaultRules();
        var lines = StandardOrder();
        var rounding = System.MidpointRounding.AwayFromZero;

        // Act
        var totals = Pricing.Calculate(lines, CustomerTier.Regular, false, rules, rounding);

        // Assert
        Assert.Equal(1200.10m, totals.Subtotal);
        Assert.Equal(120.01m, totals.Discount);
        Assert.Equal(50.00m, totals.Shipping);
        Assert.Equal(1130.09m, totals.Net);
        Assert.Equal(226.02m, totals.Vat);
        Assert.Equal(1356.11m, totals.Total);
    }

    [Fact]
    public void Free_shipping_is_applied_when_subtotal_is_over_threshold()
    {
        // Arrange
        var rules = GetDefaultRules();
        var lines = new List<OrderLine> { new OrderLine { Quantity = 5, UnitPrice = 500.00m } }; // Subtotal 2500

        // Act
        var shipping = Pricing.ShippingFor(2500.00m, rules);

        // Assert
        Assert.Equal(0.00m, shipping);
    }

    [Fact]
    public void Shipping_flat_rate_is_applied_when_subtotal_is_under_threshold()
    {
        // Arrange
        var rules = GetDefaultRules();

        // Act
        var shipping = Pricing.ShippingFor(1200.10m, rules);

        // Assert
        Assert.Equal(50.00m, shipping);
    }

    [Fact]
    public void Discount_rate_for_regular_customer_over_threshold()
    {
        // Arrange
        var rules = GetDefaultRules();

        // Act
        var rate = Pricing.DiscountRate(1200.10m, CustomerTier.Regular, false, rules);

        // Assert
        Assert.Equal(0.10m, rate);
    }
}