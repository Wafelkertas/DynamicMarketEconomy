namespace DynamicMarketEconomy;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using HarmonyLib;

public class ModEntry : Mod
{
    public static ModEntry Instance;

    private MarketState state;
    private PriceModel priceModel;
    private NpcSystem npcSystem;
    private MarketUI ui;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        var config = helper.ReadConfig<ModConfig>();

        state = new MarketState();
        priceModel = new PriceModel(config, state);
        npcSystem = new NpcSystem(state);

        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;

        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.PatchAll();
        ui = new MarketUI(state);

        helper.Events.Display.RenderedHud += OnRender;
        helper.Events.Input.ButtonPressed += OnInput;

        Monitor.Log("DynamicMarketEconomy loaded", LogLevel.Info);
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        if (!Context.IsMainPlayer) return;

        npcSystem.Simulate();
        ApplySeason();
        priceModel.DailyUpdate();

        Monitor.Log("Market updated", LogLevel.Debug);
    }

    private void OnDayEnding(object sender, DayEndingEventArgs e)
    {
        if (!Context.IsMainPlayer) return;

        foreach (var item in Game1.getFarm().shippingBin)
        {
            if (item is StardewValley.Object obj)
            {
                int id = obj.ParentSheetIndex;

                if (!state.Supply.ContainsKey(id))
                    state.Supply[id] = 1f;

                float amount = Math.Min(obj.Stack, 50) * 0.1f;
                state.Supply[id] += amount;
            }
        }
    }

    private void ApplySeason()
    {
        string season = Game1.currentSeason;

        if (season == "winter")
        {
            foreach (var key in state.Demand.Keys.ToList())
                state.Demand[key] *= 0.9f;
        }
    }

    public int GetPrice(int id, int basePrice)
        => priceModel.AdjustPrice(id, basePrice);
}