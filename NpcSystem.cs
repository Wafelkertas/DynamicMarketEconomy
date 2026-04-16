namespace DynamicMarketEconomy;

using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;

public class NpcSystem
{
    private const float BaseDemandPerCategoryItem = 0.10f;
    private const float BaseDemandPerExplicitItem = 0.18f;
    private const float BaseCategoryConsumptionPerItem = 0.03f;
    private const float MinSupply = 0.10f;
    private const float NeedThreshold = 0.50f;
    private const float HungerRelief = 0.40f;
    private const float CraftingRelief = 0.30f;
    private static readonly HashSet<int> FoodCategories = new() { -7, -4 };
    private static readonly HashSet<int> MaterialItemIds = new() { 330, 378, 380, 382, 384, 386, 388, 390, 709, 771 };
    private static readonly HashSet<string> DefaultNpcNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Dwarf", "Elliott", "Emily", "Evelyn",
        "George", "Gus", "Haley", "Harvey", "Jas", "Jodi", "Kent", "Krobus", "Leah", "Leo", "Lewis",
        "Linus", "Marnie", "Maru", "Pam", "Penny", "Pierre", "Robin", "Sam", "Sebastian", "Shane",
        "Sandy", "Vincent", "Willy", "Wizard"
    };

    private readonly ModConfig config;
    private readonly MarketState state;
    private readonly ItemDatabase itemDatabase;
    private readonly IMonitor monitor;
    private readonly Random rng = new();

    public NpcSystem(ModConfig config, MarketState state, ItemDatabase itemDatabase, IMonitor monitor)
    {
        this.config = config;
        this.state = state;
        this.itemDatabase = itemDatabase;
        this.monitor = monitor;
    }

    public void Simulate()
    {
        bool hasCategoryPrefs = config.NpcCategoryPreferences is { Count: > 0 };
        bool hasItemPrefs = config.NpcItemPreferences is { Count: > 0 };
        bool hasItemConsumption = config.NpcConsumption is { Count: > 0 };
        bool hasCategoryConsumption = config.NpcCategoryConsumption is { Count: > 0 };

        HashSet<string> npcNames = new(DefaultNpcNames, StringComparer.OrdinalIgnoreCase);

        if (hasCategoryPrefs)
        {
            foreach (string npcName in config.NpcCategoryPreferences.Keys)
                npcNames.Add(npcName);
        }

        if (hasItemPrefs)
        {
            foreach (string npcName in config.NpcItemPreferences.Keys)
                npcNames.Add(npcName);
        }

        if (hasItemConsumption)
        {
            foreach (string npcName in config.NpcConsumption.Keys)
                npcNames.Add(npcName);
        }

        if (hasCategoryConsumption)
        {
            foreach (string npcName in config.NpcCategoryConsumption.Keys)
                npcNames.Add(npcName);
        }

        foreach (string npcName in npcNames)
        {
            NpcNeeds needs = GetOrCreateNeeds(npcName);
            List<int> preferredCategories = config.NpcCategoryPreferences.GetValueOrDefault(npcName, new List<int>());
            List<int> preferredItems = config.NpcItemPreferences.GetValueOrDefault(npcName, new List<int>());

            if (preferredCategories.Count > 0 || preferredItems.Count > 0)
            {
                float dailyVariation = GetDailyVariation();
                float weight = config.NpcDemandWeights.GetValueOrDefault(npcName, 1f);
                float categoryDemandPerItem = BaseDemandPerCategoryItem * weight * dailyVariation;
                float explicitDemandPerItem = BaseDemandPerExplicitItem * weight * dailyVariation;

                ApplyCategoryDemand(preferredCategories, categoryDemandPerItem);
                ApplyItemDemand(preferredItems, explicitDemandPerItem);
            }

            UpdateNeeds(npcName, needs);

            if (needs.Hunger > NeedThreshold)
                BuyAndConsumeFood(npcName, needs);

            if (needs.Crafting > NeedThreshold)
                ConsumeMaterials(npcName, needs);

            ApplyConfiguredConsumption(npcName);
        }

        monitor.Log($"NPC simulation applied for {npcNames.Count} villagers.", LogLevel.Trace);
    }

    private void ApplyConfiguredConsumption(string npcName)
    {
        if (string.IsNullOrWhiteSpace(npcName))
            return;

        float weight = config.NpcDemandWeights.GetValueOrDefault(npcName, 1f);

        Dictionary<int, float>? perItemConsumption = config.NpcConsumption.GetValueOrDefault(npcName);
        if (perItemConsumption is not null)
        {
            foreach ((int itemId, float configuredAmount) in perItemConsumption)
            {
                if (configuredAmount <= 0f)
                    continue;

                float amount = configuredAmount * weight * GetDailyVariation();
                ReduceSupply(itemId, amount);
            }
        }

        List<int>? categoryConsumption = config.NpcCategoryConsumption.GetValueOrDefault(npcName);
        if (categoryConsumption is null || categoryConsumption.Count == 0 || itemDatabase.Items.Count == 0)
            return;

        HashSet<int> categorySet = new(categoryConsumption);
        float perItemCategoryAmount = BaseCategoryConsumptionPerItem * weight;

        foreach ((int itemId, ItemMeta meta) in itemDatabase.Items)
        {
            if (meta is null || !categorySet.Contains(meta.Category))
                continue;

            float amount = perItemCategoryAmount * GetDailyVariation();
            ReduceSupply(itemId, amount);
        }
    }

    private void UpdateNeeds(string npcName, NpcNeeds needs)
    {
        float hungerRate = Math.Max(0f, config.NpcBaseHungerRate * config.NpcHungerRate.GetValueOrDefault(npcName, 1f));
        float craftingRate = Math.Max(0f, config.NpcBaseCraftingRate * config.NpcCraftingRate.GetValueOrDefault(npcName, 1f));

        needs.Hunger = Clamp01(needs.Hunger + hungerRate);
        needs.Crafting = Clamp01(needs.Crafting + craftingRate);
        needs.Luxury = Clamp01(needs.Luxury + (0.5f * hungerRate));
    }

    private void BuyAndConsumeFood(string npcName, NpcNeeds needs)
    {
        float demandWeight = config.NpcDemandWeights.GetValueOrDefault(npcName, 1f);
        float amount = Math.Max(0.05f, 0.22f * demandWeight * GetDailyVariation());
        bool boughtFromFarm = TryBuyFoodFromFarm(amount);
        if (!boughtFromFarm)
            TryBuyFoodFromGus(amount);

        needs.Hunger = Math.Max(0f, needs.Hunger - HungerRelief);
    }

    private bool TryBuyFoodFromFarm(float amount)
    {
        List<int> farmFoodIds = state.RecentSales
            .Where(pair => pair.Value > 0f)
            .Select(pair => pair.Key)
            .Where(IsFoodItem)
            .ToList();

        if (farmFoodIds.Count == 0)
            return false;

        int index = rng.Next(farmFoodIds.Count);
        int itemId = farmFoodIds[index];
        IncreaseDemand(itemId, amount * 1.2f);
        ReduceSupply(itemId, amount);
        return true;
    }

    private void TryBuyFoodFromGus(float amount)
    {
        List<int> gusFoodItems = config.NpcItemPreferences.GetValueOrDefault("Gus", new List<int>())
            .Where(IsFoodItem)
            .ToList();

        if (gusFoodItems.Count == 0)
            gusFoodItems = itemDatabase.Items
                .Where(pair => pair.Value is not null && pair.Value.Category == -7)
                .Select(pair => pair.Key)
                .ToList();

        if (gusFoodItems.Count == 0)
            return;

        int itemId = gusFoodItems[rng.Next(gusFoodItems.Count)];
        IncreaseDemand(itemId, amount);
        ReduceSupply(itemId, amount);
    }

    private void ConsumeMaterials(string npcName, NpcNeeds needs)
    {
        float demandWeight = config.NpcDemandWeights.GetValueOrDefault(npcName, 1f);
        float amount = Math.Max(0.05f, 0.18f * demandWeight * GetDailyVariation());

        foreach (int itemId in MaterialItemIds)
            ReduceSupply(itemId, amount);

        needs.Crafting = Math.Max(0f, needs.Crafting - CraftingRelief);
    }

    private NpcNeeds GetOrCreateNeeds(string npcName)
    {
        if (string.IsNullOrWhiteSpace(npcName))
            return new NpcNeeds();

        if (!state.Needs.TryGetValue(npcName, out NpcNeeds? needs) || needs is null)
        {
            needs = new NpcNeeds();
            state.Needs[npcName] = needs;
        }

        return needs;
    }

    private void ApplyCategoryDemand(List<int> preferredCategories, float demandPerItem)
    {
        if (preferredCategories.Count == 0 || itemDatabase.Items.Count == 0)
            return;

        HashSet<int> categorySet = new(preferredCategories);
        foreach ((int itemId, ItemMeta meta) in itemDatabase.Items)
        {
            if (meta is null)
                continue;

            if (categorySet.Contains(meta.Category))
                IncreaseDemand(itemId, demandPerItem);
        }
    }

    private bool IsFoodItem(int itemId)
    {
        ItemMeta item = itemDatabase.Get(itemId);
        return FoodCategories.Contains(item.Category);
    }

    private void ApplyItemDemand(List<int> preferredItems, float demandPerItem)
    {
        if (preferredItems.Count == 0)
            return;

        foreach (int itemId in preferredItems)
            IncreaseDemand(itemId, demandPerItem);
    }

    private void IncreaseDemand(int id, float amount)
    {
        if (id <= 0 || amount <= 0f)
            return;

        if (!state.Demand.ContainsKey(id))
            state.Demand[id] = 1f;

        state.Demand[id] += amount;
    }

    private void ReduceSupply(int id, float amount)
    {
        if (id <= 0 || amount <= 0f)
            return;

        float currentSupply = state.Supply.GetValueOrDefault(id, 1f);
        float nextSupply = Math.Max(MinSupply, currentSupply - amount);
        state.Supply[id] = nextSupply;
    }

    private float GetDailyVariation()
    {
        float variationSpan = Math.Clamp(config.NpcDemandRandomness, 0f, 0.45f) * 2f;
        return 1f + ((float)rng.NextDouble() - 0.5f) * variationSpan;
    }

    private static float Clamp01(float value)
        => Math.Clamp(value, 0f, 1f);
}
