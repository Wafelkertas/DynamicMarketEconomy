namespace DynamicMarketEconomy;

using System;
using StardewModdingAPI;

public class PriceModel
{
    private readonly ModConfig config;
    private readonly MarketState state;
    private readonly ItemDatabase itemDb;
    private readonly IReadOnlyDictionary<int, MarketCategory> categoryRules;
    private readonly Random rng = new();

    private static readonly MarketCategory DefaultCategory = new()
    {
        DemandMultiplier = 1f,
        SupplySensitivity = 1f,
        Volatility = 1f
    };

    public PriceModel(
        ModConfig config,
        MarketState state,
        ItemDatabase itemDb,
        IReadOnlyDictionary<int, MarketCategory> categoryRules)
    {
        this.config = config;
        this.state = state;
        this.itemDb = itemDb;
        this.categoryRules = categoryRules;
    }

    public int AdjustPrice(int id, int basePrice)
    {
        RegisterBasePrice(id, basePrice);
        return CalculateAdjustedPrice(id, basePrice, applyVolatility: true);
    }

    public void RegisterBasePrice(int id, int basePrice)
    {
        if (basePrice <= 0 || !Context.IsMainPlayer)
            return;

        state.BasePriceByItem[id] = basePrice;
    }

    public void DailyUpdate()
    {
        foreach (int key in state.Demand.Keys.ToList())
            state.Demand[key] *= config.DemandDecay;

        foreach (int key in state.Supply.Keys.ToList())
            state.Supply[key] *= config.SupplyDecay;

        HashSet<int> trackedItems = new();
        foreach (int id in state.Demand.Keys)
            trackedItems.Add(id);
        foreach (int id in state.Supply.Keys)
            trackedItems.Add(id);
        foreach (int id in state.BasePriceByItem.Keys)
            trackedItems.Add(id);

        int maxHistoryLength = Math.Clamp(config.MaxHistoryLength, 30, 60);

        foreach (int key in trackedItems)
        {
            int basePrice = state.BasePriceByItem.GetValueOrDefault(key, 0);
            if (basePrice <= 0)
                continue;

            float adjustedPrice = CalculateAdjustedPrice(key, basePrice, applyVolatility: true);
            AddHistoryPoint(key, adjustedPrice, maxHistoryLength);
        }
    }

    private int CalculateAdjustedPrice(int id, int basePrice, bool applyVolatility)
    {
        int category = itemDb.Get(id).Category;
        MarketCategory categoryRule = categoryRules.GetValueOrDefault(category, DefaultCategory);

        float demand = state.Demand.GetValueOrDefault(id, 1f);
        float supply = state.Supply.GetValueOrDefault(id, 1f);

        float price = basePrice * ((demand * categoryRule.DemandMultiplier) / (1f + supply * categoryRule.SupplySensitivity));

        if (applyVolatility)
        {
            float effectiveVolatility = config.Volatility * categoryRule.Volatility;
            float randomFactor = 1f + (float)(rng.NextDouble() * effectiveVolatility - effectiveVolatility / 2f);
            price *= randomFactor;
        }

        return Math.Max(1, (int)price);
    }

    private void AddHistoryPoint(int itemId, float adjustedPrice, int maxHistoryLength)
    {
        if (!state.PriceHistory.TryGetValue(itemId, out List<float>? history))
        {
            history = new List<float>();
            state.PriceHistory[itemId] = history;
        }

        history.Add(adjustedPrice);
        while (history.Count > maxHistoryLength)
            history.RemoveAt(0);
    }
}
