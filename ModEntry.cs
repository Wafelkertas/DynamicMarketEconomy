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
    private MultiplayerHandler mp;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        var config = helper.ReadConfig<ModConfig>();

        state = new MarketState();
        priceModel = new PriceModel(config, state);
        npcSystem = new NpcSystem(state);
        ui = new MarketUI(state);
        mp = new MultiplayerHandler(helper, state, ModManifest.UniqueID);

        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Display.RenderedHud += OnRender;
        helper.Events.Input.ButtonPressed += OnInput;
        helper.Events.Player.InventoryChanged += OnInventoryChanged;

        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.PatchAll();
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        if (!Context.IsMainPlayer) return;

        npcSystem.Simulate();
        ApplySeason();
        priceModel.DailyUpdate();

        mp.Send();
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

    private void OnRender(object sender, RenderedHudEventArgs e)
    {
        ui.Draw();
    }

    private void OnInput(object sender, ButtonPressedEventArgs e)
    {
        if (e.Button == SButton.F6)
            ui.Visible = !ui.Visible;
    }

    private void OnInventoryChanged(object sender, InventoryChangedEventArgs e)
    {
        foreach (var item in e.Removed)
        {
            if (item is StardewValley.Object obj)
            {
                if (!state.Supply.ContainsKey(obj.ParentSheetIndex))
                    state.Supply[obj.ParentSheetIndex] = 1f;

                state.Supply[obj.ParentSheetIndex] += obj.Stack * 0.05f;
            }
        }
    }

    public int GetPrice(int id, int basePrice)
        => priceModel.AdjustPrice(id, basePrice);
}