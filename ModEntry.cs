namespace DynamicMarketEconomy;

using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

public class ModEntry : Mod
{
    public static ModEntry Instance = null!;

    private ModConfig config = null!;
    private MarketState state = null!;
    private PriceModel priceModel = null!;
    private NpcSystem npcSystem = null!;
    private MarketUI ui = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        config = helper.ReadConfig<ModConfig>();
        helper.WriteConfig(config);

        state = new MarketState();
        priceModel = new PriceModel(config, state);
        npcSystem = new NpcSystem(state, Monitor);
        ui = new MarketUI(state, Monitor);

        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Input.ButtonPressed += OnButtonPressed;

        Harmony harmony = new(ModManifest.UniqueID);
        harmony.PatchAll();

        Monitor.Log("DynamicMarketEconomy loaded", LogLevel.Info);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;

        npcSystem.Simulate();
        ApplySeason();
        priceModel.DailyUpdate();

        Monitor.Log("Market updated", LogLevel.Debug);
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;

        Dictionary<int, float> dailySupplyByItem = new();

        foreach (Item item in Game1.getFarm().shippingBin)
        {
            if (item is StardewValley.Object obj)
            {
                int id = obj.ParentSheetIndex;
                float amount = Math.Min(obj.Stack, 50) * 0.1f;
                float currentToday = dailySupplyByItem.GetValueOrDefault(id, 0f);
                float remainingCap = Math.Max(0f, config.SupplyCapPerDay - currentToday);
                float cappedAmount = Math.Min(amount, remainingCap);

                if (cappedAmount <= 0f)
                    continue;

                dailySupplyByItem[id] = currentToday + cappedAmount;
            }
        }

        foreach ((int id, float amount) in dailySupplyByItem)
        {
            if (!state.Supply.ContainsKey(id))
                state.Supply[id] = 1f;

            state.Supply[id] += amount;
        }
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        ui.Draw();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (e.Button == SButton.F6)
        {
            ui.Visible = !ui.Visible;
            Monitor.Log($"Market UI {(ui.Visible ? "enabled" : "disabled")}", LogLevel.Trace);
            return;
        }

        if (!ui.Visible)
            return;

        if (e.Button == SButton.Right || e.Button == SButton.DPadRight || e.Button == SButton.MouseWheelDown)
            ui.SelectNextItem();
        else if (e.Button == SButton.Left || e.Button == SButton.DPadLeft || e.Button == SButton.MouseWheelUp)
            ui.SelectPreviousItem();
    }

    private void ApplySeason()
    {
        if (Game1.currentSeason != Season.Winter)
            return;

        foreach (int key in state.Demand.Keys.ToList())
            state.Demand[key] *= 0.9f;
    }

    public int GetPrice(int id, int basePrice)
        => priceModel.AdjustPrice(id, basePrice);
}
