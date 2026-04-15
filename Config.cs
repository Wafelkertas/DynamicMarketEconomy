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

    // NPC name => item categories that NPC influences in market demand.
    public Dictionary<string, List<int>> NpcCategoryPreferences { get; set; } = new()
    {
        ["Abigail"] = new List<int> { -2, -12, -4 },       // gems, minerals, fish
        ["Caroline"] = new List<int> { -81, -79 },         // greens/forage, fruits
        ["Demetrius"] = new List<int> { -81, -4 },         // forage, fish
        ["Gus"] = new List<int> { -7, -26 },               // cooking, artisan goods
        ["Robin"] = new List<int> { -74, -15 },            // seeds, construction resources
        ["Pierre"] = new List<int> { -75, -79 },           // vegetables, fruits
        ["Willy"] = new List<int> { -4 },                  // fish
        ["Marnie"] = new List<int> { -5, -6 },             // eggs, milk
        ["Clint"] = new List<int> { -15, -12 },            // ores/bars, minerals
        ["Evelyn"] = new List<int> { -80 },                // flowers
        ["Harvey"] = new List<int> { -75, -79, -7 },       // produce + cooked health foods
        ["Leah"] = new List<int> { -81, -79 },             // forage, fruits
        ["Sebastian"] = new List<int> { -4, -12 },         // fish, minerals
        ["Linus"] = new List<int> { -81, -4 },             // forage, fish
        ["Pam"] = new List<int> { -7, -26 },               // food, drinks/artisan
        ["Shane"] = new List<int> { -5, -6, -26 },         // animal products, beer/artisan
        ["Jodi"] = new List<int> { -75, -79, -7 },         // ingredients and meals
        ["Kent"] = new List<int> { -7 },                   // cooked food
        ["Emily"] = new List<int> { -18, -26 },            // cloth, artisan goods
        ["Haley"] = new List<int> { -80, -79 }             // flowers, fruits
    };

    // NPC name => item IDs that NPC influences in market demand.
    // These are explicit overrides layered on top of category preferences.
    public Dictionary<string, List<int>> NpcItemPreferences { get; set; } = new()
    {
        ["Abigail"] = new List<int> { 66, 82, 284 },
        ["Caroline"] = new List<int> { 24, 188, 421 },
        ["Demetrius"] = new List<int> { 128, 129, 130 },
        ["Gus"] = new List<int> { 194, 216, 228 },
        ["Robin"] = new List<int> { 388, 709, 771 },
        ["Pierre"] = new List<int> { 24, 190, 192 },
        ["Willy"] = new List<int> { 128, 130, 131 },
        ["Marnie"] = new List<int> { 176, 180, 184 },
        ["Clint"] = new List<int> { 334, 335, 336 },
        ["Evelyn"] = new List<int> { 421, 425, 597 },
        ["Harvey"] = new List<int> { 24, 250, 773 },
        ["Leah"] = new List<int> { 16, 20, 22 },
        ["Sebastian"] = new List<int> { 130, 86, 84 },
        ["Linus"] = new List<int> { 16, 128, 129 },
        ["Pam"] = new List<int> { 346, 303, 348 },
        ["Shane"] = new List<int> { 306, 307, 346 },
        ["Jodi"] = new List<int> { 194, 258, 270 },
        ["Kent"] = new List<int> { 205, 214, 240 },
        ["Emily"] = new List<int> { 428, 440, 446 },
        ["Haley"] = new List<int> { 421, 593, 595 }
    };

    // NPC name => relative demand impact.
    public Dictionary<string, float> NpcDemandWeights { get; set; } = new()
    {
        ["Abigail"] = 1.10f,
        ["Caroline"] = 0.95f,
        ["Demetrius"] = 1.20f,
        ["Gus"] = 1.30f,
        ["Robin"] = 0.85f,
        ["Pierre"] = 1.15f,
        ["Willy"] = 1.15f,
        ["Marnie"] = 1.00f,
        ["Clint"] = 1.05f,
        ["Evelyn"] = 0.90f,
        ["Harvey"] = 1.05f,
        ["Leah"] = 0.95f,
        ["Sebastian"] = 0.90f,
        ["Linus"] = 0.85f,
        ["Pam"] = 0.95f,
        ["Shane"] = 1.10f,
        ["Jodi"] = 1.00f,
        ["Kent"] = 0.95f,
        ["Emily"] = 1.00f,
        ["Haley"] = 0.90f
    };
}
