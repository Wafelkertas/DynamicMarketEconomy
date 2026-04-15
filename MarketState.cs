namespace DynamicMarketEconomy;

using System.Collections.Generic;

public class MarketState
{
    public Dictionary<int, float> Demand = new();
    public Dictionary<int, float> Supply = new();
    public Dictionary<int, float> RecentSales = new();
    public Dictionary<int, float> SmoothedDemand = new();
    public Dictionary<int, int> BasePriceByItem = new();
    public Dictionary<int, List<float>> PriceHistory = new();
}
