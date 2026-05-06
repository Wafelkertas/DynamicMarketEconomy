namespace DynamicMarketEconomy;

using StardewModdingAPI;
using StardewValley;

/// <summary>
/// Coordinates daily economy updates so <see cref="ModEntry"/> stays focused on wiring.
/// </summary>
public class EconomyCoordinator
{
    private readonly ModConfig config;
    private readonly MarketState state;
    private readonly PriceModel priceModel;
    private readonly NpcSystem npcSystem;
    private readonly MultiplayerHandler multiplayer;
    private readonly IMonitor monitor;

    public EconomyCoordinator(
        ModConfig config,
        MarketState state,
        PriceModel priceModel,
        NpcSystem npcSystem,
        MultiplayerHandler multiplayer,
        IMonitor monitor)
    {
        this.config = config;
        this.state = state;
        this.priceModel = priceModel;
        this.npcSystem = npcSystem;
        this.multiplayer = multiplayer;
        this.monitor = monitor;
    }

    public void OnSaveLoaded()
    {
        if (Context.IsMainPlayer)
            multiplayer.BroadcastStateIfHost();
        else
            multiplayer.RequestStateFromHostIfClient();
    }

    public void OnDayStarted()
    {
        if (!Context.IsMainPlayer)
        {
            multiplayer.RequestStateFromHostIfClient();
            return;
        }

        npcSystem.Simulate();
        ApplySeasonDemandModifiers();
        priceModel.DailyUpdate();
        multiplayer.BroadcastStateIfHost();

        monitor.Log("Market updated", LogLevel.Debug);
    }

    public void OnDayEnding()
    {
        if (!Context.IsMainPlayer)
            return;

        ApplyShippingBinSupply();
        multiplayer.BroadcastStateIfHost();
    }

    private void ApplySeasonDemandModifiers()
    {
        if (Game1.currentSeason != "winter")
            return;

        foreach (int itemId in state.Demand.Keys.ToList())
            state.Demand[itemId] *= 0.9f;
    }

    private void ApplyShippingBinSupply()
    {
        Dictionary<int, float> dailySalesByItem = new();

        foreach (Item item in Game1.getFarm().shippingBin)
        {
            if (item is not StardewValley.Object obj)
                continue;

            int itemId = obj.ParentSheetIndex;
            float soldAmount = Math.Min(obj.Stack, 50) * 0.1f;
            float currentToday = dailySalesByItem.GetValueOrDefault(itemId, 0f);
            float remainingCap = Math.Max(0f, config.SupplyCapPerDay - currentToday);
            float cappedAmount = Math.Min(soldAmount, remainingCap);

            if (cappedAmount <= 0f)
                continue;

            dailySalesByItem[itemId] = currentToday + cappedAmount;
        }

        priceModel.RecordPlayerSales(dailySalesByItem);
    }
}
