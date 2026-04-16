namespace DynamicMarketEconomy;

using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

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
    private readonly Dictionary<int, List<int>> itemIdsByCategory = new();
    private readonly List<int> allFoodItemIds = new();
    private readonly List<string> cachedRuntimeNpcNames = new();
    private int cachedRuntimeNpcDay = -1;

    public NpcSystem(ModConfig config, MarketState state, ItemDatabase itemDatabase, IMonitor monitor)
    {
        this.config = config;
        this.state = state;
        this.itemDatabase = itemDatabase;
        this.monitor = monitor;
        BuildItemIndexes();
    }

    public void Simulate()
    {
        state.GusFoodListings.Clear();

        bool hasCategoryPrefs = config.NpcCategoryPreferences is { Count: > 0 };
        bool hasItemPrefs = config.NpcItemPreferences is { Count: > 0 };
        bool hasItemConsumption = config.NpcConsumption is { Count: > 0 };
        bool hasCategoryConsumption = config.NpcCategoryConsumption is { Count: > 0 };

        HashSet<string> npcNames = new(DefaultNpcNames, StringComparer.OrdinalIgnoreCase);
        foreach (string runtimeNpc in DiscoverRuntimeNpcNamesCached())
            npcNames.Add(runtimeNpc);
        foreach (string name in state.Needs.Keys)
            npcNames.Add(name);

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
        float budgetWeight = Math.Max(0.20f, GetBudgetWeight(npcName));
        float amount = Math.Max(
            0.05f,
            config.NpcBaseDailyFoodBudget * demandWeight * budgetWeight * (0.75f + needs.Hunger) * GetDailyVariation());
        bool boughtFromFarm = TryBuyFoodFromFarm(amount);
        bool boughtFromGus = false;
        if (!boughtFromFarm)
            boughtFromGus = TryBuyFoodFromGus(amount);

        if (boughtFromFarm || boughtFromGus)
            needs.Hunger = Math.Max(0f, needs.Hunger - HungerRelief);
    }

    private bool TryBuyFoodFromFarm(float amount)
    {
        List<int> farmFoodIds = state.FarmFoodListings
            .Where(pair => pair.Value > 0f)
            .Select(pair => pair.Key)
            .Where(IsFoodItem)
            .ToList();

        if (farmFoodIds.Count == 0)
            return false;

        int index = rng.Next(farmFoodIds.Count);
        int itemId = farmFoodIds[index];
        float available = state.FarmFoodListings.GetValueOrDefault(itemId, 0f);
        float purchased = Math.Min(amount, available);
        if (purchased <= 0f)
            return false;

        state.FarmFoodListings[itemId] = Math.Max(0f, available - purchased);
        IncreaseDemand(itemId, purchased * 1.2f);
        ReduceSupply(itemId, purchased);
        return true;
    }

    private bool TryBuyFoodFromGus(float amount)
    {
        EnsureGusListings();
        List<int> gusFoodItems = state.GusFoodListings
            .Where(pair => pair.Value > 0f)
            .Select(pair => pair.Key)
            .ToList();

        if (gusFoodItems.Count == 0)
            return false;

        int itemId = gusFoodItems[rng.Next(gusFoodItems.Count)];
        float available = state.GusFoodListings.GetValueOrDefault(itemId, 0f);
        float purchased = Math.Min(amount, available);
        if (purchased <= 0f)
            return false;

        state.GusFoodListings[itemId] = Math.Max(0f, available - purchased);
        IncreaseDemand(itemId, purchased);
        ReduceSupply(itemId, purchased);
        return true;
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

        foreach (int category in preferredCategories)
        {
            if (!itemIdsByCategory.TryGetValue(category, out List<int>? itemIds) || itemIds.Count == 0)
                continue;

            foreach (int itemId in itemIds)
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

    private void BuildItemIndexes()
    {
        itemIdsByCategory.Clear();
        allFoodItemIds.Clear();

        foreach ((int itemId, ItemMeta meta) in itemDatabase.Items)
        {
            if (meta is null)
                continue;

            if (!itemIdsByCategory.TryGetValue(meta.Category, out List<int>? categoryItems))
            {
                categoryItems = new List<int>();
                itemIdsByCategory[meta.Category] = categoryItems;
            }

            categoryItems.Add(itemId);

            if (FoodCategories.Contains(meta.Category))
                allFoodItemIds.Add(itemId);
        }
    }

    private void EnsureGusListings()
    {
        if (state.GusFoodListings.Count > 0)
            return;

        List<int> seededFoodItems = config.NpcItemPreferences.GetValueOrDefault("Gus", new List<int>())
            .Where(IsFoodItem)
            .ToList();

        if (seededFoodItems.Count == 0)
            seededFoodItems = allFoodItemIds.Where(id => itemDatabase.Get(id).Category == -7).ToList();

        float stockPerItem = Math.Max(0.25f, config.GusDailyFoodStockPerItem);
        foreach (int id in seededFoodItems)
            state.GusFoodListings[id] = stockPerItem;
    }

    private float GetBudgetWeight(string npcName)
    {
        if (config.NpcFoodBudgetWeights.TryGetValue(npcName, out float configured))
            return configured;

        int hash = 17;
        foreach (char c in npcName.ToUpperInvariant())
            hash = (hash * 31) + c;

        hash = Math.Abs(hash);
        float normalized = (hash % 100) / 100f;
        return 0.75f + (normalized * 0.70f); // 0.75..1.45 approximate budget spread
    }

    private IEnumerable<string> DiscoverRuntimeNpcNamesCached()
    {
        if (!Context.IsWorldReady)
            return Enumerable.Empty<string>();

        int currentDay = Game1.Date.TotalDays;
        if (cachedRuntimeNpcDay == currentDay && cachedRuntimeNpcNames.Count > 0)
            return cachedRuntimeNpcNames;

        cachedRuntimeNpcNames.Clear();
        cachedRuntimeNpcNames.AddRange(Game1.locations
            .SelectMany(location => location.characters)
            .Where(character => character is not null)
            .Select(character => character.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        cachedRuntimeNpcDay = currentDay;
        return cachedRuntimeNpcNames;
    }

    private static float Clamp01(float value)
        => Math.Clamp(value, 0f, 1f);
}
