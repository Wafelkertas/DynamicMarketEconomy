namespace DynamicMarketEconomy;

using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

public class ModEntry : Mod
{
    public static ModEntry Instance = null!;

    private const string MarketStateDataKey = "market-state";

    private MarketState state = null!;
    private PriceModel priceModel = null!;
    private EconomyCoordinator economy = null!;
    private MarketUiController uiController = null!;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        ModConfig config = helper.ReadConfig<ModConfig>();
        helper.WriteConfig(config);

        state = new MarketState();

        ItemDatabase itemDatabase = new();
        itemDatabase.Load(helper);

        Dictionary<int, MarketCategory> categoryRules = CategoryRules.Build();
        priceModel = new PriceModel(config, state, itemDatabase, categoryRules);
        NpcSystem npcSystem = new(config, state, itemDatabase, Monitor);
        MarketUI marketUi = new(state, priceModel, itemDatabase, Monitor);
        MultiplayerHandler multiplayer = new(helper, Monitor, ModManifest, state);
        economy = new EconomyCoordinator(config, state, priceModel, npcSystem, multiplayer, Monitor);
        uiController = new MarketUiController(marketUi, Monitor);

        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Input.ButtonPressed += OnButtonPressed;

        Harmony harmony = new(ModManifest.UniqueID);
        harmony.PatchAll();

        Monitor.Log("DynamicMarketEconomy loaded", LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;

        MarketState? loadedState = Helper.Data.ReadSaveData<MarketState>(MarketStateDataKey);
        ApplyLoadedState(loadedState ?? new MarketState());

        economy.OnSaveLoaded();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;

        Helper.Data.WriteSaveData(MarketStateDataKey, state);
    }

    private void ApplyLoadedState(MarketState loadedState)
    {
        state.Demand = loadedState.Demand ?? new();
        state.Supply = loadedState.Supply ?? new();
        state.CategoryDemandHistory = loadedState.CategoryDemandHistory ?? new();
        state.CategorySupplyHistory = loadedState.CategorySupplyHistory ?? new();
        state.FarmFoodListings = loadedState.FarmFoodListings ?? new();
        state.GusFoodListings = loadedState.GusFoodListings ?? new();
        state.PendingPlayerSales = loadedState.PendingPlayerSales ?? new();
        state.Needs = loadedState.Needs ?? new(StringComparer.OrdinalIgnoreCase);
        state.RecentSales = loadedState.RecentSales ?? new();
        state.SmoothedDemand = loadedState.SmoothedDemand ?? new();
        state.BasePriceByItem = loadedState.BasePriceByItem ?? new();
        state.PriceHistory = loadedState.PriceHistory ?? new();
    }

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
