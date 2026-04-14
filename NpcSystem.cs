namespace DynamicMarketEconomy;

using System.Collections.Generic;

public class NpcSystem
{
    private readonly MarketState state;

    public NpcSystem(MarketState state)
    {
        this.state = state;
    }

    public void Simulate()
    {
        IncreaseDemand(24, 0.3f);   // Parsnip
        IncreaseDemand(128, 0.5f);  // Fish
        IncreaseDemand(194, 0.4f);  // Cooked food
    }

    private void IncreaseDemand(int id, float amount)
    {
        if (!state.Demand.ContainsKey(id))
            state.Demand[id] = 1f;

        state.Demand[id] += amount;
    }
}