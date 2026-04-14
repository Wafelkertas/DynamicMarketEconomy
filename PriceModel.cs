namespace DynamicMarketEconomy;

using System;

public class PriceModel
{
    private readonly ModConfig config;
    private readonly MarketState state;
    private readonly Random rng = new();

    public PriceModel(ModConfig config, MarketState state)
    {
        this.config = config;
        this.state = state;
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
        float demand = state.Demand.GetValueOrDefault(id, 1f);
        float supply = state.Supply.GetValueOrDefault(id, 1f);

        float price = basePrice * (demand / (supply + 1f));

        if (applyVolatility)
        {
            float randomFactor = 1f + (float)(rng.NextDouble() * config.Volatility - config.Volatility / 2f);
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
