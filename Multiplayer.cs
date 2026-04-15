namespace DynamicMarketEconomy;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

public class MarketPacket
{
    public Dictionary<int, float> Demand { get; set; } = new();
    public Dictionary<int, float> Supply { get; set; } = new();
    public Dictionary<int, float> RecentSales { get; set; } = new();
    public Dictionary<int, float> SmoothedDemand { get; set; } = new();
    public Dictionary<int, int> BasePriceByItem { get; set; } = new();
    public Dictionary<int, List<float>> PriceHistory { get; set; } = new();
}

public class MultiplayerHandler
{
    private const string StateSyncMessageType = "market-state-sync";
    private const string StateRequestMessageType = "market-state-request";

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly MarketState state;
    private readonly string modId;

    public MultiplayerHandler(IModHelper helper, IMonitor monitor, IManifest manifest, MarketState state)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.state = state;
        modId = manifest.UniqueID;

        helper.Multiplayer.ModMessageReceived += OnReceive;
    }

    public void BroadcastStateIfHost()
    {
        if (!Context.IsMainPlayer || !Context.IsMultiplayer)
            return;

        helper.Multiplayer.SendMessage(BuildPacket(), StateSyncMessageType, new[] { modId });
    }

    public void RequestStateFromHostIfClient()
    {
        if (!Context.IsMultiplayer || Context.IsMainPlayer || Game1.MasterPlayer is null)
            return;

        helper.Multiplayer.SendMessage(
            message: "request",
            messageType: StateRequestMessageType,
            modIDs: new[] { modId },
            playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
    }

    private void OnReceive(object? sender, ModMessageReceivedEventArgs e)
    {
        if (e.FromModID != modId)
            return;

        if (e.Type == StateRequestMessageType)
        {
            if (!Context.IsMainPlayer)
                return;

            helper.Multiplayer.SendMessage(
                message: BuildPacket(),
                messageType: StateSyncMessageType,
                modIDs: new[] { modId },
                playerIDs: new[] { e.FromPlayerID });
            return;
        }

        if (e.Type != StateSyncMessageType || Context.IsMainPlayer)
            return;

        MarketPacket packet = e.ReadAs<MarketPacket>();
        ApplyPacket(packet);
        monitor.Log("Received synchronized market state from host.", LogLevel.Trace);
    }

    private MarketPacket BuildPacket()
    {
        return new MarketPacket
        {
            Demand = state.Demand.ToDictionary(pair => pair.Key, pair => pair.Value),
            Supply = state.Supply.ToDictionary(pair => pair.Key, pair => pair.Value),
            RecentSales = state.RecentSales.ToDictionary(pair => pair.Key, pair => pair.Value),
            SmoothedDemand = state.SmoothedDemand.ToDictionary(pair => pair.Key, pair => pair.Value),
            BasePriceByItem = state.BasePriceByItem.ToDictionary(pair => pair.Key, pair => pair.Value),
            PriceHistory = state.PriceHistory.ToDictionary(pair => pair.Key, pair => new List<float>(pair.Value))
        };
    }

    private void ApplyPacket(MarketPacket packet)
    {
        state.Demand = packet.Demand ?? new Dictionary<int, float>();
        state.Supply = packet.Supply ?? new Dictionary<int, float>();
        state.RecentSales = packet.RecentSales ?? new Dictionary<int, float>();
        state.SmoothedDemand = packet.SmoothedDemand ?? new Dictionary<int, float>();
        state.BasePriceByItem = packet.BasePriceByItem ?? new Dictionary<int, int>();
        state.PriceHistory = packet.PriceHistory ?? new Dictionary<int, List<float>>();
    }
}
