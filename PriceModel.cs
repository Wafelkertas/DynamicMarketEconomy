namespace DynamicMarketEconomy;

using System;
using StardewModdingAPI;

public class PriceModel
{
    private const float DemandInertiaKeep = 0.9f;
    private const float DemandInertiaApply = 0.1f;
    private const float RecentSalesKeep = 0.7f;
    private const float RecentSalesApply = 0.3f;

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
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.itemDb = itemDb ?? throw new ArgumentNullException(nameof(itemDb));
        this.categoryRules = categoryRules ?? throw new ArgumentNullException(nameof(categoryRules));
    }

    public int AdjustPrice(int id, int basePrice)
    {
        RegisterBasePrice(id, basePrice);
        return CalculateAdjustedPrice(id, basePrice, applyVolatility: true);
    }

    public string GetRecommendation(int id, int basePrice)
    {
        if (basePrice <= 0)
            return "NEUTRAL";

        float demand = state.Demand.GetValueOrDefault(id, 1f);
        float supply = state.Supply.GetValueOrDefault(id, 1f);
        float ratio = demand / (supply + 1f);

        float trend = 0f;
        if (state.PriceHistory.TryGetValue(id, out List<float>? history) && history.Count >= 2)
            trend = history[^1] - history[^2];

        if (ratio > 1.2f && trend >= 0f)
            return "SELL";

        if (trend > 0f)
            return "HOLD";

        if (ratio < 0.8f)
            return "AVOID";

        return "NEUTRAL";
    }

    public void RegisterBasePrice(int id, int basePrice)
    {
        if (basePrice <= 0 || !Context.IsMainPlayer)
            return;

        state.BasePriceByItem[id] = basePrice;
    }

    public void RecordPlayerSales(IReadOnlyDictionary<int, float> soldTodayByItem)
    {
        if (soldTodayByItem is null)
            return;

        foreach ((int itemId, float soldAmount) in soldTodayByItem)
        {
            if (soldAmount <= 0f)
                continue;

            float shock = (float)Math.Log(soldAmount + 1f);

            float currentSupply = state.Supply.GetValueOrDefault(itemId, 1f);
            state.Supply[itemId] = currentSupply + shock;

            float previousRecentSales = state.RecentSales.GetValueOrDefault(itemId, 0f);
            float laggedRecentSales = previousRecentSales * RecentSalesKeep + soldAmount * RecentSalesApply;
            state.RecentSales[itemId] = laggedRecentSales;
        }
    }

    public void DailyUpdate()
    {
        ApplyDemandInertia();
        DecaySupply();
        DecayRecentSales();

        HashSet<int> trackedItems = new();
        foreach (int id in state.Demand.Keys)
            trackedItems.Add(id);
        foreach (int id in state.Supply.Keys)
            trackedItems.Add(id);
        foreach (int id in state.RecentSales.Keys)
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

        UpdateCategoryHistory(trackedItems, maxHistoryLength);
    }

    private void ApplyDemandInertia()
    {
        foreach (int itemId in state.Demand.Keys.ToList())
        {
            float newDemand = state.Demand[itemId];
            float previousSmoothedDemand = state.SmoothedDemand.GetValueOrDefault(itemId, newDemand);
            float smoothedDemand = previousSmoothedDemand * DemandInertiaKeep + newDemand * DemandInertiaApply;

            state.SmoothedDemand[itemId] = smoothedDemand;
            state.Demand[itemId] = smoothedDemand;
        }
    }

    private void DecaySupply()
    {
        float supplyDecay = Math.Clamp(config.SupplyDecay, 0.90f, 0.95f);
        foreach (int itemId in state.Supply.Keys.ToList())
            state.Supply[itemId] *= supplyDecay;
    }

    private void DecayRecentSales()
    {
        foreach (int itemId in state.RecentSales.Keys.ToList())
            state.RecentSales[itemId] *= RecentSalesKeep;
    }

    private int CalculateAdjustedPrice(int id, int basePrice, bool applyVolatility)
    {
        int category = itemDb.Get(id).Category;
        MarketCategory categoryRule = categoryRules.GetValueOrDefault(category, DefaultCategory);

        float demand = state.Demand.GetValueOrDefault(id, 1f);
        float marketSupply = state.Supply.GetValueOrDefault(id, 1f);
        float laggedSalesSupply = state.RecentSales.GetValueOrDefault(id, 0f);
        float effectiveSupply = Math.Max(0.10f, marketSupply + (laggedSalesSupply * 0.5f));

        float price = basePrice * ((demand * categoryRule.DemandMultiplier) / (1f + effectiveSupply * categoryRule.SupplySensitivity));

        if (applyVolatility)
        {
            float effectiveVolatility = Math.Max(0f, config.Volatility * categoryRule.Volatility);
            float randomFactor = 1f + (float)(rng.NextDouble() * effectiveVolatility - effectiveVolatility / 2f);
            price *= randomFactor;
        }

        float minPrice = basePrice * 0.5f;
        float maxPrice = basePrice * 2.5f;
        price = Math.Clamp(price, minPrice, maxPrice);

        return Math.Max(1, (int)MathF.Round(price));
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

    private void UpdateCategoryHistory(HashSet<int> trackedItems, int maxHistoryLength)
    {
        if (trackedItems.Count == 0)
            return;

        Dictionary<int, (float demandSum, float supplySum, int count)> aggregates = new();

        foreach (int itemId in trackedItems)
        {
            int category = itemDb.Get(itemId).Category;
            if (!aggregates.TryGetValue(category, out (float demandSum, float supplySum, int count) aggregate))
                aggregate = (0f, 0f, 0);

            aggregate.demandSum += state.Demand.GetValueOrDefault(itemId, 1f);
            aggregate.supplySum += state.Supply.GetValueOrDefault(itemId, 1f);
            aggregate.count++;
            aggregates[category] = aggregate;
        }

        foreach ((int category, (float demandSum, float supplySum, int count) values) in aggregates)
        {
            if (values.count <= 0)
                continue;

            float avgDemand = values.demandSum / values.count;
            float avgSupply = values.supplySum / values.count;

            AddCategoryHistoryPoint(state.CategoryDemandHistory, category, avgDemand, maxHistoryLength);
            AddCategoryHistoryPoint(state.CategorySupplyHistory, category, avgSupply, maxHistoryLength);
        }
    }

    private static void AddCategoryHistoryPoint(
        IDictionary<int, List<float>> target,
        int category,
        float value,
        int maxHistoryLength)
    {
        if (!target.TryGetValue(category, out List<float>? history))
        {
            history = new List<float>();
            target[category] = history;
        }

        history.Add(value);
        while (history.Count > maxHistoryLength)
            history.RemoveAt(0);
    }
}
