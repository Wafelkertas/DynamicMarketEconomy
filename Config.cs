namespace DynamicMarketEconomy;

/// <summary>Mod configuration loaded from config.json.</summary>
public class ModConfig
{
    public float DemandDecay { get; set; } = 0.95f;
    public float SupplyDecay { get; set; } = 0.90f;
    public float Volatility { get; set; } = 0.1f;
    public int MaxHistoryLength { get; set; } = 30;
    public float SupplyCapPerDay { get; set; } = 8f;
}
