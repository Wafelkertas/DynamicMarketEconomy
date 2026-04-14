namespace DynamicMarketEconomy;

using System;

public class PriceModel
{
    private readonly ModConfig config;
    private readonly MarketState state;
    private readonly IReadOnlyDictionary<int, MarketCategory> categoryRules;
    private readonly Random rng = new();

    private static readonly MarketCategory DefaultCategory = new()
    {
        DemandMultiplier = 1f,
        SupplySensitivity = 1f,
        Volatility = 1f
    };

    public PriceModel(ModConfig config, MarketState state, IReadOnlyDictionary<int, MarketCategory> categoryRules)
    {
        this.config = config;
        this.state = state;
        this.categoryRules = categoryRules;
    }

    public int AdjustPrice(int id, int basePrice)
    {
        RegisterBasePrice(id, basePrice);
        return CalculateAdjustedPrice(id, basePrice, applyVolatility: true);
    }

    public void RegisterBasePrice(int id, int basePrice)
    {
        if (basePrice <= 0)
            return;

        state.BasePriceByItem[id] = basePrice;
    }

    public void DailyUpdate()
    {
        foreach (int key in state.Demand.Keys.ToList())
        {
            state.Demand[key] *= config.DemandDecay;
        }

        foreach (int key in state.Supply.Keys.ToList())
        {
            state.Supply[key] *= config.SupplyDecay;
        }

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
        MarketCategory category = categoryRules.GetValueOrDefault(id, DefaultCategory);
        float demand = state.Demand.GetValueOrDefault(id, 1f);
        float supply = state.Supply.GetValueOrDefault(id, 1f);

        float demandFactor = demand * category.DemandMultiplier;
        float supplyFactor = (supply * category.SupplySensitivity) + 1f;
        float price = basePrice * (demandFactor / supplyFactor);

        if (applyVolatility)
        {
            float effectiveVolatility = config.Volatility * category.Volatility;
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
