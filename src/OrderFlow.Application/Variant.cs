namespace OrderFlow.Application;

/// <summary>
/// Build-time tuning of the deployment. The variant number selects the configuration
/// that a given installation runs with. Values are read from ORDERFLOW_VARIANT (1–30)
/// and, for maintenance builds, ORDERFLOW_DEFECTS ("all", "none" or a list such as "D2,D5,I1").
/// </summary>
public sealed class VariantOptions
{
    public int Number { get; init; } = 1;
    public IReadOnlySet<string> Enabled { get; init; } = new HashSet<string>();

    public bool On(string id) => Enabled.Contains(id);

    public bool DiscountThresholdInclusive => On("D1");
    public bool ShippingBadgeOnSubtotal => On("D2");
    public bool ApiMoneyArithmeticInDouble => On("D3");
    public bool ApiSkipsQuantityValidation => On("D4");
    public bool CancelFromAnyStatus => On("D5");
    public bool InvoiceReadsLiveAddress => On("D6");
    public bool SkipOrderOwnershipCheck => On("D7");
    public bool OrderSearchCaseSensitive => On("D8");
    public bool InvoiceLegacyTemplate => On("D9");
    public bool NoDuplicateSubmitGuard => On("D10");
    public bool PagerFloorsPageCount => On("D11");
    public bool SkipStockCheck => On("D12");
    public bool InvoiceShowsLocalTime => On("D13");
    public bool PromoOnAnyOrder => On("D14");

    // Release 1.1: behaviour that depends on state, timing and data rather than on a single request.
    public bool OrdersListCached => On("I1");
    public bool PlaceWithoutStockLock => On("I2");
    public bool InvoiceUsesBankersRounding => On("I3");

    /// <summary>Lifetime of the orders-list cache when it is enabled: 8–15 seconds, by variant.</summary>
    public int CacheSeconds => 8 + Number % 8;

    /// <summary>The intermittent item this variant runs with (the support inbox shows the matching message).</summary>
    public string IntermittentId => IntermittentFor(Number);

    /// <summary>
    /// I2 (the stock race) only goes to variants whose stock check is intact (no D12); the others alternate I1 and I3.
    /// </summary>
    public static string IntermittentFor(int number) => number switch
    {
        3 or 5 or 7 or 17 or 21 or 24 or 25 or 28 or 30 => "I2",
        1 or 4 or 8 or 10 or 12 or 14 or 16 or 19 or 22 or 26 or 29 => "I1",
        _ => "I3",
    };

    public MidpointRounding InvoiceRounding =>
        InvoiceUsesBankersRounding ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;

    public static readonly string[] AllIds =
        ["D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D10", "D11", "D12", "D13", "D14"];

    public static readonly string[] IntermittentIds = ["I1", "I2", "I3"];

    private static readonly Dictionary<int, string[]> Table = new()
    {
        [1] = ["D1", "D5", "D6", "D7", "D8", "D10", "D12", "D13", "D14"],
        [2] = ["D1", "D2", "D3", "D6", "D7", "D9", "D10", "D12", "D14"],
        [3] = ["D1", "D2", "D3", "D5", "D6", "D8", "D9", "D11", "D14"],
        [4] = ["D1", "D3", "D4", "D5", "D6", "D8", "D11", "D12", "D13"],
        [5] = ["D1", "D2", "D4", "D5", "D6", "D9", "D10", "D11", "D13"],
        [6] = ["D1", "D3", "D5", "D6", "D9", "D11", "D12", "D13", "D14"],
        [7] = ["D1", "D4", "D5", "D6", "D7", "D10", "D11", "D13", "D14"],
        [8] = ["D1", "D2", "D3", "D4", "D5", "D7", "D9", "D10", "D12"],
        [9] = ["D1", "D2", "D3", "D5", "D8", "D10", "D11", "D12", "D14"],
        [10] = ["D1", "D2", "D3", "D6", "D8", "D10", "D11", "D12", "D14"],
        [11] = ["D1", "D2", "D3", "D6", "D7", "D9", "D10", "D12", "D14"],
        [12] = ["D1", "D3", "D4", "D6", "D8", "D9", "D11", "D12", "D13"],
        [13] = ["D1", "D5", "D6", "D8", "D9", "D10", "D11", "D12", "D13"],
        [14] = ["D1", "D2", "D4", "D5", "D10", "D11", "D12", "D13", "D14"],
        [15] = ["D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D12"],
        [16] = ["D1", "D3", "D6", "D7", "D8", "D11", "D12", "D13", "D14"],
        [17] = ["D1", "D2", "D4", "D5", "D6", "D7", "D9", "D11", "D13"],
        [18] = ["D1", "D4", "D5", "D6", "D7", "D8", "D10", "D12", "D14"],
        [19] = ["D1", "D4", "D6", "D7", "D8", "D10", "D11", "D12", "D13"],
        [20] = ["D1", "D4", "D5", "D6", "D7", "D8", "D9", "D12", "D13"],
        [21] = ["D1", "D2", "D3", "D4", "D5", "D7", "D8", "D9", "D14"],
        [22] = ["D1", "D3", "D4", "D5", "D6", "D7", "D11", "D12", "D13"],
        [23] = ["D1", "D2", "D5", "D6", "D7", "D8", "D11", "D12", "D13"],
        [24] = ["D1", "D2", "D3", "D5", "D6", "D8", "D9", "D11", "D13"],
        [25] = ["D1", "D2", "D4", "D6", "D7", "D9", "D10", "D11", "D13"],
        [26] = ["D1", "D3", "D5", "D6", "D8", "D10", "D12", "D13", "D14"],
        [27] = ["D1", "D2", "D3", "D5", "D8", "D11", "D12", "D13", "D14"],
        [28] = ["D1", "D2", "D3", "D6", "D8", "D9", "D10", "D11", "D13"],
        [29] = ["D1", "D3", "D4", "D7", "D8", "D10", "D11", "D12", "D14"],
        [30] = ["D1", "D2", "D3", "D5", "D6", "D9", "D10", "D11", "D13"],
    };

    public static VariantOptions FromEnvironment()
    {
        var numberText = Environment.GetEnvironmentVariable("ORDERFLOW_VARIANT");
        int number = int.TryParse(numberText, out var n) && n >= 1 && n <= 30 ? n : 1;
        var set = new HashSet<string>(Table[number], StringComparer.OrdinalIgnoreCase) { IntermittentFor(number) };

        var overrideText = Environment.GetEnvironmentVariable("ORDERFLOW_DEFECTS");
        if (!string.IsNullOrWhiteSpace(overrideText))
        {
            var t = overrideText.Trim();
            if (t.Equals("all", StringComparison.OrdinalIgnoreCase))
                set = new HashSet<string>(AllIds, StringComparer.OrdinalIgnoreCase);
            else if (t.Equals("none", StringComparison.OrdinalIgnoreCase))
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            else
                set = new HashSet<string>(t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);
        }
        return new VariantOptions { Number = number, Enabled = set };
    }
}
