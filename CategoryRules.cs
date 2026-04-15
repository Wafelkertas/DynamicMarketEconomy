namespace DynamicMarketEconomy;

public static class CategoryRules
{
    public static Dictionary<int, MarketCategory> Build()
    {
        return new Dictionary<int, MarketCategory>
        {
            // Vegetables
            [-75] = new MarketCategory
            {
                DemandMultiplier = 1.00f,
                SupplySensitivity = 1.25f,
                Volatility = 0.55f
            },

            // Fruits
            [-79] = new MarketCategory
            {
                DemandMultiplier = 1.20f,
                SupplySensitivity = 1.00f,
                Volatility = 1.00f
            },

            // Fish
            [-4] = new MarketCategory
            {
                DemandMultiplier = 1.05f,
                SupplySensitivity = 0.60f,
                Volatility = 1.80f
            },

            // Cooking
            [-7] = new MarketCategory
            {
                DemandMultiplier = 1.30f,
                SupplySensitivity = 1.10f,
                Volatility = 0.65f
            },

            // Artisan
            [-26] = new MarketCategory
            {
                DemandMultiplier = 1.45f,
                SupplySensitivity = 0.80f,
                Volatility = 2.20f
            }
        };
    }
}
