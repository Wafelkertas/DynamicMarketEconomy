using System.Collections.Generic;
using StardewModdingAPI;

public class MarketPacket
{
    public Dictionary<int, float> Demand;
    public Dictionary<int, float> Supply;
}

public class MultiplayerHandler
{
    private readonly IModHelper helper;
    private readonly MarketState state;
    private readonly string modId;

    public MultiplayerHandler(IModHelper helper, MarketState state, string modId)
    {
        this.helper = helper;
        this.state = state;
        this.modId = modId;

        helper.Multiplayer.ModMessageReceived += OnReceive;
    }

    public void Send()
    {
        var packet = new MarketPacket
        {
            Demand = state.Demand,
            Supply = state.Supply
        };

        helper.Multiplayer.SendMessage(packet, "sync", new[] { modId });
    }

    private void OnReceive(object sender, ModMessageReceivedEventArgs e)
    {
        if (e.Type != "sync") return;

        var data = e.ReadAs<MarketPacket>();
        state.Demand = data.Demand;
        state.Supply = data.Supply;
    }
}