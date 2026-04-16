namespace DynamicMarketEconomy;

using System;
using System.Collections.Generic;

public class MarketState
{
    public Dictionary<int, float> Demand { get; set; }
    public Dictionary<int, float> Supply { get; set; }
    public Dictionary<int, List<float>> CategoryDemandHistory { get; set; }
    public Dictionary<int, List<float>> CategorySupplyHistory { get; set; }
    public Dictionary<int, float> FarmFoodListings { get; set; }
    public Dictionary<int, float> GusFoodListings { get; set; }
    public Dictionary<int, float> PendingPlayerSales { get; set; }
    public Dictionary<string, NpcNeeds> Needs { get; set; }
    public Dictionary<int, float> RecentSales { get; set; }
    public Dictionary<int, float> SmoothedDemand { get; set; }
    public Dictionary<int, int> BasePriceByItem { get; set; }
    public Dictionary<int, List<float>> PriceHistory { get; set; }

    public MarketState()
    {
        Demand = new Dictionary<int, float>();
        Supply = new Dictionary<int, float>();
        CategoryDemandHistory = new Dictionary<int, List<float>>();
        CategorySupplyHistory = new Dictionary<int, List<float>>();
        FarmFoodListings = new Dictionary<int, float>();
        GusFoodListings = new Dictionary<int, float>();
        PendingPlayerSales = new Dictionary<int, float>();
        Needs = new Dictionary<string, NpcNeeds>(StringComparer.OrdinalIgnoreCase);
        RecentSales = new Dictionary<int, float>();
        SmoothedDemand = new Dictionary<int, float>();
        BasePriceByItem = new Dictionary<int, int>();
        PriceHistory = new Dictionary<int, List<float>>();
    }
}
