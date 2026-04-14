namespace DynamicMarketEconomy;

using System.Collections.Generic;
using StardewModdingAPI;

public class NpcSystem
{
    private readonly MarketState state;
    private readonly IMonitor monitor;
    private readonly Random rng = new();

    // Structured NPC preferences: NPC name => item IDs they tend to increase demand for.
    private readonly Dictionary<string, List<int>> npcPreferences = new()
    {
        ["Abigail"] = new List<int> { 66, 82, 284 },        // Amethyst, Fire Quartz, Beet
        ["Caroline"] = new List<int> { 24, 188, 421 },      // Parsnip, Green Bean, Sunflower
        ["Demetrius"] = new List<int> { 128, 129, 130 },    // Fish examples
        ["Gus"] = new List<int> { 194, 216, 228 },          // Fried Egg, Bread, Maki Roll
        ["Robin"] = new List<int> { 388, 709, 771 }         // Wood, Hardwood, Fiber
    };

    // Per-NPC demand influence weights.
    private readonly Dictionary<string, float> npcWeights = new()
    {
        ["Abigail"] = 1.10f,
        ["Caroline"] = 0.95f,
        ["Demetrius"] = 1.20f,
        ["Gus"] = 1.30f,
        ["Robin"] = 0.85f
    };

    public NpcSystem(MarketState state, IMonitor monitor)
    {
        this.state = state;
        this.monitor = monitor;
    }

    public void Simulate()
    {
        foreach ((string npcName, List<int> preferredItems) in npcPreferences)
        {
            float weight = npcWeights.GetValueOrDefault(npcName, 1f);
            float dailyVariation = 1f + ((float)rng.NextDouble() - 0.5f) * 0.20f; // ±10%
            float demandPerItem = 0.18f * weight * dailyVariation;

            foreach (int itemId in preferredItems)
                IncreaseDemand(itemId, demandPerItem);
        }

        monitor.Log($"NPC demand simulation applied for {npcPreferences.Count} villagers.", LogLevel.Trace);
    }

    private void IncreaseDemand(int id, float amount)
    {
        if (!state.Demand.ContainsKey(id))
            state.Demand[id] = 1f;

        state.Demand[id] += amount;
    }
}
