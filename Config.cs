namespace DynamicMarketEconomy;

/// <summary>Mod configuration loaded from config.json.</summary>
public class ModConfig
{
    public float DemandDecay { get; set; } = 0.95f;
    public float SupplyDecay { get; set; } = 0.90f;
    public float Volatility { get; set; } = 0.1f;
    public int MaxHistoryLength { get; set; } = 30;
    public float SupplyCapPerDay { get; set; } = 8f;
    public float NpcDemandRandomness { get; set; } = 0.10f;

    // NPC name => item IDs that NPC influences in market demand.
    public Dictionary<string, List<int>> NpcItemPreferences { get; set; } = new()
    {
        ["Abigail"] = new List<int> { 66, 82, 284 },
        ["Caroline"] = new List<int> { 24, 188, 421 },
        ["Demetrius"] = new List<int> { 128, 129, 130 },
        ["Gus"] = new List<int> { 194, 216, 228 },
        ["Robin"] = new List<int> { 388, 709, 771 }
    };

    // NPC name => relative demand impact.
    public Dictionary<string, float> NpcDemandWeights { get; set; } = new()
    {
        ["Abigail"] = 1.10f,
        ["Caroline"] = 0.95f,
        ["Demetrius"] = 1.20f,
        ["Gus"] = 1.30f,
        ["Robin"] = 0.85f
    };
}
