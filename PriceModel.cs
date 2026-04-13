using StardewValley;

public class PriceModel
{
    private readonly ModConfig config;
    private readonly MarketState state;

    public PriceModel(ModConfig config, MarketState state)
    {
        this.config = config;
        this.state = state;
    }

    public int AdjustPrice(int itemId, int basePrice)
    {
        float demand = state.GetDemand(itemId);
        float supply = state.GetSupply(itemId);

        float factor =
            1f +
            (demand - 1f) * config.DemandImpact -
            (supply - 1f) * config.SupplyImpact;

        return (int)(basePrice * factor);
    }

    public void DailyUpdate()
    {
        foreach (var key in state.Demand.Keys.ToList())
        {
            state.Demand[key] = Lerp(state.Demand[key], 1f, 0.05f);
            state.Supply[key] = Lerp(state.Supply[key], 1f, 0.05f);
        }
    }

    private float Lerp(float a, float b, float t)
        => a + (b - a) * t;
}