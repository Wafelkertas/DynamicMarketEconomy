using System.Collections.Generic;

public class MarketState
{
    public Dictionary<int, float> Demand = new();
    public Dictionary<int, float> Supply = new();

    public float GetDemand(int id) => Demand.GetValueOrDefault(id, 1f);
    public float GetSupply(int id) => Supply.GetValueOrDefault(id, 1f);
}