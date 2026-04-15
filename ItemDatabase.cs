namespace DynamicMarketEconomy;

using System.Text.Json;
using StardewModdingAPI;

public class ItemDatabase
{
    private static readonly ItemMeta UnknownItem = new()
    {
        Id = -1,
        Name = "Unknown",
        Category = 0,
        Price = 0
    };

    public Dictionary<int, ItemMeta> Items { get; private set; } = new();

    public void Load(IModHelper helper)
    {
        string jsonPath = Path.Combine(helper.DirectoryPath, "items.json");
        if (!File.Exists(jsonPath))
        {
            Items = new Dictionary<int, ItemMeta>();
            return;
        }

        string json = File.ReadAllText(jsonPath);
        List<ItemMeta>? parsedItems = JsonSerializer.Deserialize<List<ItemMeta>>(json);

        if (parsedItems is null)
        {
            Items = new Dictionary<int, ItemMeta>();
            return;
        }

        Items = parsedItems
            .GroupBy(p => p.Id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public ItemMeta Get(int id)
    {
        if (Items.TryGetValue(id, out ItemMeta? item))
            return item;

        return new ItemMeta
        {
            Id = id,
            Name = UnknownItem.Name,
            Category = UnknownItem.Category,
            Price = UnknownItem.Price
        };
    }
}
