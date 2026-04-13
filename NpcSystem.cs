using System.Collections.Generic;
using StardewValley;

public class NpcSystem
{
    private readonly MarketState state;

    public NpcSystem(MarketState state)
    {
        this.state = state;
    }

    public void Simulate()
    {
        foreach (var npc in Game1.getAllCharacters())
        {
            if (!npc.isVillager()) continue;

            foreach (var taste in Game1.NPCGiftTastes)
            {
                int itemId = taste.Key;

                float demand = (float)Game1.random.NextDouble() * 0.1f;

                if (!state.Demand.ContainsKey(itemId))
                    state.Demand[itemId] = 1f;

                state.Demand[itemId] += demand;
            }
        }
    }
}