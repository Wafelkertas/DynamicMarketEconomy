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
        float demand = state.Demand.GetValueOrDefault(id, 1f);
        float supply = state.Supply.GetValueOrDefault(id, 1f);

        float price = basePrice;

        price *= demand / (supply + 1f);

        // volatility
        float randomFactor = 1f + (float)(rng.NextDouble() * config.Volatility - config.Volatility / 2);
        price *= randomFactor;

        return Math.Max(1, (int)price);
    }

    public void DailyUpdate()
    {
        foreach (var key in state.Demand.Keys.ToList())
        {
            state.Demand[key] *= config.DemandDecay;
        }

        foreach (var key in state.Supply.Keys.ToList())
        {
            state.Supply[key] *= config.SupplyDecay;
        }
    }
}