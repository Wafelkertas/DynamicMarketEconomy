namespace DynamicMarketEconomy;

using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

public class ModEntry : Mod
{
    public static ModEntry Instance = null!;

    private PriceModel priceModel = null!;
    private EconomyCoordinator economy = null!;
    private MarketUiController uiController = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        ModConfig config = helper.ReadConfig<ModConfig>();
        helper.WriteConfig(config);

        MarketState state = new();
        Dictionary<int, MarketCategory> categoryRules = CategoryRules.Build();
        priceModel = new PriceModel(config, state, categoryRules);
        NpcSystem npcSystem = new(state, Monitor);
        MarketUI marketUi = new(state, Monitor);
        MultiplayerHandler multiplayer = new(helper, Monitor, ModManifest, state);
        economy = new EconomyCoordinator(config, state, priceModel, npcSystem, multiplayer, Monitor);
        uiController = new MarketUiController(marketUi, Monitor);

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Input.ButtonPressed += OnButtonPressed;

        Harmony harmony = new(ModManifest.UniqueID);
        harmony.PatchAll();

        Monitor.Log("DynamicMarketEconomy loaded", LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        => economy.OnSaveLoaded();

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
        => economy.OnDayStarted();

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
        => economy.OnDayEnding();

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        => uiController.Draw();

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        => uiController.OnButtonPressed(e.Button);

    public int GetPrice(int id, int basePrice)
        => priceModel.AdjustPrice(id, basePrice);
}
