namespace DynamicMarketEconomy;

/// <summary>
/// Per-item category behavior modifiers used by the dynamic market model.
/// </summary>
public class MarketCategory
{
    public float DemandMultiplier { get; init; }
    public float SupplySensitivity { get; init; }
    public float Volatility { get; init; }
}
