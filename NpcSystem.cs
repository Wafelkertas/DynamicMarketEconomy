namespace DynamicMarketEconomy;

using System.Collections.Generic;
using StardewModdingAPI;

public class NpcSystem
{
    private const float BaseDemandPerCategoryItem = 0.10f;
    private const float BaseDemandPerExplicitItem = 0.18f;

    private readonly ModConfig config;
    private readonly MarketState state;
    private readonly ItemDatabase itemDatabase;
    private readonly IMonitor monitor;
    private readonly Random rng = new();

    public NpcSystem(ModConfig config, MarketState state, ItemDatabase itemDatabase, IMonitor monitor)
    {
        this.config = config;
        this.state = state;
        this.itemDatabase = itemDatabase;
        this.monitor = monitor;
    }

    public void Simulate()
    {
        bool hasCategoryPrefs = config.NpcCategoryPreferences is { Count: > 0 };
        bool hasItemPrefs = config.NpcItemPreferences is { Count: > 0 };
        if (!hasCategoryPrefs && !hasItemPrefs)
            return;

        float variationSpan = Math.Clamp(config.NpcDemandRandomness, 0f, 0.45f) * 2f;
        HashSet<string> npcNames = new(StringComparer.OrdinalIgnoreCase);

        if (hasCategoryPrefs)
        {
            foreach (string npcName in config.NpcCategoryPreferences.Keys)
                npcNames.Add(npcName);
        }

        if (hasItemPrefs)
        {
            foreach (string npcName in config.NpcItemPreferences.Keys)
                npcNames.Add(npcName);
        }

        foreach (string npcName in npcNames)
        {
            List<int> preferredCategories = config.NpcCategoryPreferences.GetValueOrDefault(npcName, new List<int>());
            List<int> preferredItems = config.NpcItemPreferences.GetValueOrDefault(npcName, new List<int>());
            if (preferredCategories.Count == 0 && preferredItems.Count == 0)
                continue;

            float weight = config.NpcDemandWeights.GetValueOrDefault(npcName, 1f);
            float dailyVariation = 1f + ((float)rng.NextDouble() - 0.5f) * variationSpan;
            float categoryDemandPerItem = BaseDemandPerCategoryItem * weight * dailyVariation;
            float explicitDemandPerItem = BaseDemandPerExplicitItem * weight * dailyVariation;

            ApplyCategoryDemand(preferredCategories, categoryDemandPerItem);
            ApplyItemDemand(preferredItems, explicitDemandPerItem);
        }

        monitor.Log($"NPC demand simulation applied for {npcNames.Count} villagers.", LogLevel.Trace);
    }

    private void ApplyCategoryDemand(List<int> preferredCategories, float demandPerItem)
    {
        if (preferredCategories.Count == 0 || itemDatabase.Items.Count == 0)
            return;

        HashSet<int> categorySet = new(preferredCategories);
        foreach ((int itemId, ItemMeta meta) in itemDatabase.Items)
        {
            if (meta is null)
                continue;

            if (categorySet.Contains(meta.Category))
                IncreaseDemand(itemId, demandPerItem);
        }
    }

    private void ApplyItemDemand(List<int> preferredItems, float demandPerItem)
    {
        if (preferredItems.Count == 0)
            return;

        foreach (int itemId in preferredItems)
            IncreaseDemand(itemId, demandPerItem);
    }

    private void IncreaseDemand(int id, float amount)
    {
        if (id <= 0 || amount <= 0f)
            return;

        if (!state.Demand.ContainsKey(id))
            state.Demand[id] = 1f;

        state.Demand[id] += amount;
    }
}
