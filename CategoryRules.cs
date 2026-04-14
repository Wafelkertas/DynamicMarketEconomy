namespace DynamicMarketEconomy;

public static class CategoryRules
{
    public static Dictionary<int, MarketCategory> Build()
    {
        MarketCategory vegetables = new()
        {
            DemandMultiplier = 1.00f,
            SupplySensitivity = 1.25f,
            Volatility = 0.55f
        };

        MarketCategory fruits = new()
        {
            DemandMultiplier = 1.20f,
            SupplySensitivity = 1.00f,
            Volatility = 1.00f
        };

        MarketCategory fish = new()
        {
            DemandMultiplier = 1.05f,
            SupplySensitivity = 0.60f,
            Volatility = 1.80f
        };

        MarketCategory cooking = new()
        {
            DemandMultiplier = 1.30f,
            SupplySensitivity = 1.10f,
            Volatility = 0.65f
        };

        MarketCategory artisan = new()
        {
            DemandMultiplier = 1.45f,
            SupplySensitivity = 0.80f,
            Volatility = 2.20f
        };

        // Mapping item IDs to category behavior profiles.
        Dictionary<int, MarketCategory> categoryRules = new()
        {
            // Vegetables (stable, low volatility)
            [24] = vegetables,   // Parsnip
            [188] = vegetables,  // Green Bean
            [190] = vegetables,  // Cauliflower
            [284] = vegetables,  // Beet

            // Fruits (higher demand, medium volatility)
            [296] = fruits,      // Salmonberry
            [300] = fruits,      // Amaranth? (example fruit slot in this mapping)
            [634] = fruits,      // Apricot
            [636] = fruits,      // Peach

            // Fish (high volatility, low supply sensitivity)
            [128] = fish,
            [129] = fish,
            [130] = fish,
            [131] = fish,

            // Cooking (stable but high demand)
            [194] = cooking,
            [216] = cooking,
            [228] = cooking,

            // Artisan (high value, very volatile)
            [340] = artisan,     // Honey
            [424] = artisan,     // Cheese
            [303] = artisan      // Pale Ale
        };

        return categoryRules;
    }
}
