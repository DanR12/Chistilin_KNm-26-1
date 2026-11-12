using Xunit;
using System.Collections.Generic;
using FluentAssertions;

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
        var lines = StandardOrder();
        var subtotal = Pricing.Subtotal(lines);
        subtotal.Should().Be(1200.10m, "subtotal is 2*500 + 1*200.10");
    }

    [Fact]
    public void Order_totals_are_computed_correctly_for_regular_customer_without_promo()
    {
        var rules = GetDefaultRules();
        var lines = StandardOrder();
        var rounding = System.MidpointRounding.AwayFromZero;

        var totals = Pricing.Calculate(lines, CustomerTier.Regular, false, rules, rounding);

        totals.Subtotal.Should().Be(1200.10m);
        totals.Discount.Should().Be(120.01m, "regular tier discount is 10% over 1000 threshold");
        totals.Shipping.Should().Be(50.00m);
        totals.Net.Should().Be(1130.09m);
        totals.Vat.Should().Be(226.02m);
        totals.Total.Should().Be(1356.11m);
    }

    [Theory]
    [InlineData(2000.00, 50.00)]
    [InlineData(2000.01, 0.00)]
    public void Shipping_is_free_only_strictly_over_threshold(decimal subtotal, decimal expectedShipping)
    {
        var rules = GetDefaultRules();
        var shipping = Pricing.ShippingFor(subtotal, rules);
        shipping.Should().Be(expectedShipping, "shipping is free only strictly over 2000");
    }

    [Theory]
    [InlineData(1000.00, CustomerTier.Regular, 0.00)]
    [InlineData(1000.01, CustomerTier.Regular, 0.10)]
    [InlineData(1000.01, CustomerTier.Vip, 0.15)]
    public void Discount_rate_depends_on_tier_and_strict_subtotal_threshold(decimal subtotal, CustomerTier tier, decimal expectedRate)
    {
        var rules = GetDefaultRules();
        var rate = Pricing.DiscountRate(subtotal, tier, false, rules);
        rate.Should().Be(expectedRate, "threshold is strictly greater than 1000");
    }
}