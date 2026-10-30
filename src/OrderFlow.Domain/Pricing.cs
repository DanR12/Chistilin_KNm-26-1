namespace OrderFlow.Domain;

/// <summary>Business rules for discounts, shipping and VAT.</summary>
public sealed class PricingRules
{
    public decimal DiscountThreshold { get; init; } = 1000m;
    public bool ThresholdInclusive { get; init; }
    public decimal RegularRate { get; init; } = 0.25m;
    public decimal VipRate { get; init; } = 0.15m;
    public string PromoCode { get; init; } = "WELCOME";
    public decimal PromoRate { get; init; } = 0.05m;
    public decimal ShippingFlat { get; init; } = 50m;
    public decimal FreeShippingThreshold { get; init; } = 2000m;
    public decimal VatRate { get; init; } = 0.20m;
}

public readonly record struct OrderTotals(
    decimal Subtotal, decimal Discount, decimal Shipping, decimal Net, decimal Vat, decimal Total);

public static class Pricing
{
    public static decimal Subtotal(IEnumerable<OrderLine> lines) => lines.Sum(l => l.UnitPrice * l.Quantity);

    public static bool OverThreshold(decimal subtotal, PricingRules r) =>
        r.ThresholdInclusive ? subtotal >= r.DiscountThreshold : subtotal > r.DiscountThreshold;

    public static decimal DiscountRate(decimal subtotal, CustomerTier tier, bool promoApplies, PricingRules r)
    {
        decimal rate = OverThreshold(subtotal, r) ? (tier == CustomerTier.VIP ? r.VipRate : r.RegularRate) : 0m;
        if (promoApplies) rate += r.PromoRate;
        return rate;
    }

    public static decimal ShippingFor(decimal amount, PricingRules r) =>
        amount <= 0m ? 0m : (amount > r.FreeShippingThreshold ? 0m : r.ShippingFlat);

    /// <summary>Totals of an order. <paramref name="rounding"/> is the rule applied to every half-cent.</summary>
    public static OrderTotals Calculate(IEnumerable<OrderLine> lines, CustomerTier tier, bool promoApplies, PricingRules r,
        MidpointRounding rounding = MidpointRounding.AwayFromZero)
    {
        var list = lines.ToList();
        var subtotal = Round(Subtotal(list), rounding);
        var discount = Round(subtotal * DiscountRate(subtotal, tier, promoApplies, r), rounding);
        var net = subtotal - discount;
        var shipping = ShippingFor(net, r);
        var vat = Round((net + shipping) * r.VatRate, rounding);
        return new OrderTotals(subtotal, discount, shipping, net, vat, net + shipping + vat);
    }

    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static decimal Round(decimal v, MidpointRounding rounding) => Math.Round(v, 2, rounding);
}
