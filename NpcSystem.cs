namespace DynamicMarketEconomy;

using System.Collections.Generic;
using StardewModdingAPI;

public class NpcSystem
{
    private const float BaseDemandPerItem = 0.18f;

    private readonly ModConfig config;
    private readonly MarketState state;
    private readonly IMonitor monitor;
    private readonly Random rng = new();

    public NpcSystem(ModConfig config, MarketState state, IMonitor monitor)
    {
        this.config = config;
        this.state = state;
        this.monitor = monitor;
    }

    public void Simulate()
    {
        if (config.NpcItemPreferences.Count == 0)
            return;

        float variationSpan = Math.Clamp(config.NpcDemandRandomness, 0f, 0.45f) * 2f;

        foreach ((string npcName, List<int> preferredItems) in config.NpcItemPreferences)
        {
            if (preferredItems.Count == 0)
                continue;

            float weight = config.NpcDemandWeights.GetValueOrDefault(npcName, 1f);
            float dailyVariation = 1f + ((float)rng.NextDouble() - 0.5f) * variationSpan;
            float demandPerItem = BaseDemandPerItem * weight * dailyVariation;

            foreach (int itemId in preferredItems)
                IncreaseDemand(itemId, demandPerItem);
        }

        monitor.Log($"NPC demand simulation applied for {config.NpcItemPreferences.Count} villagers.", LogLevel.Trace);
    }

    private void IncreaseDemand(int id, float amount)
    {
        if (!state.Demand.ContainsKey(id))
            state.Demand[id] = 1f;

        state.Demand[id] += amount;
    }
}
