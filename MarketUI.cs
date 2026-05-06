namespace DynamicMarketEconomy;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

public class MarketUI
{
    private const int RecommendationsToShow = 8;

    private readonly MarketState state;
    private readonly PriceModel priceModel;
    private readonly IMonitor monitor;

    private readonly List<int> orderedItemIds = new();
    private int selectedItemIndex;

    public bool Visible { get; set; }

    public MarketUI(MarketState state, PriceModel priceModel, IMonitor monitor)
    {
        this.state = state;
        this.priceModel = priceModel;
        this.monitor = monitor;
    }

    public void SelectNextItem()
    {
        if (!TryRefreshItems())
            return;

        selectedItemIndex = (selectedItemIndex + 1) % orderedItemIds.Count;
    }

    public void SelectPreviousItem()
    {
        if (!TryRefreshItems())
            return;

        selectedItemIndex--;
        if (selectedItemIndex < 0)
            selectedItemIndex = orderedItemIds.Count - 1;
    }

    public void Draw()
    {
        if (!Visible)
            return;

        if (!TryRefreshItems())
        {
            DrawEmptyMessage();
            return;
        }

        SpriteBatch spriteBatch = Game1.spriteBatch;

        int panelX = 48;
        int panelY = 48;
        int panelW = 860;
        int panelH = 420;

        int graphPaddingL = 56;
        int graphPaddingR = 24;
        int graphPaddingT = 90;
        int graphPaddingB = 44;

        Rectangle panelRect = new(panelX, panelY, panelW, panelH);
        Rectangle graphRect = new(
            panelX + graphPaddingL,
            panelY + graphPaddingT,
            480,
            panelH - graphPaddingT - graphPaddingB);

        Rectangle recRect = new(
            graphRect.Right + 20,
            panelY + 80,
            panelRect.Right - (graphRect.Right + 32),
            panelH - 108);

        DrawRect(spriteBatch, panelRect, Color.Black * 0.7f);
        DrawRectOutline(spriteBatch, panelRect, Color.SaddleBrown);

        int itemId = orderedItemIds[selectedItemIndex];
        List<float> history = state.PriceHistory.GetValueOrDefault(itemId, new List<float>());
        string itemName = GetItemName(itemId);

        float demand = state.Demand.GetValueOrDefault(itemId, 1f);
        float supply = state.Supply.GetValueOrDefault(itemId, 1f);
        float multiplier = demand / (supply + 1f);
        int basePrice = Math.Max(1, state.BasePriceByItem.GetValueOrDefault(itemId, 1));
        string recommendation = priceModel.GetRecommendation(itemId, basePrice);

        Game1.drawString(Game1.smallFont, $"Market Item: {itemName} (ID {itemId})", new Vector2(panelX + 12, panelY + 10), Color.White);
        Game1.drawString(Game1.smallFont, $"Multiplier: x{multiplier:0.00}", new Vector2(panelX + 12, panelY + 34), Color.LightGreen);
        Game1.drawString(Game1.smallFont, $"Demand: {demand:0.00}  Supply: {supply:0.00}", new Vector2(panelX + 220, panelY + 34), Color.LightBlue);
        Game1.drawString(Game1.smallFont, $"Action: {recommendation}", new Vector2(panelX + 500, panelY + 34), GetRecommendationColor(recommendation));
        Game1.drawString(Game1.smallFont, $"Item {selectedItemIndex + 1}/{orderedItemIds.Count}  [Left/Right or Wheel]", new Vector2(panelX + 500, panelY + 10), Color.Gainsboro);

        DrawGraphGrid(spriteBatch, graphRect);

        if (history.Count >= 2)
        {
            float min = history.Min();
            float max = history.Max();
            float range = Math.Max(0.01f, max - min);
            float pad = range * 0.1f;
            float scaledMin = min - pad;
            float scaledMax = max + pad;

            DrawGraphLabels(graphRect, scaledMin, scaledMax, history.Count);
            DrawSmoothLine(spriteBatch, graphRect, history, scaledMin, scaledMax, Color.Lime);
        }
        else
        {
            Game1.drawString(Game1.smallFont, "Not enough history yet (need 2+ points)", new Vector2(graphRect.X + 8, graphRect.Y + graphRect.Height / 2f), Color.Orange);
        }

        DrawRecommendations(recRect);
    }

    private void DrawRecommendations(Rectangle area)
    {
        DrawRect(Game1.spriteBatch, area, Color.Black * 0.20f);
        DrawRectOutline(Game1.spriteBatch, area, Color.DarkSlateGray);

        Game1.drawString(Game1.smallFont, "Recommendations", new Vector2(area.X + 8, area.Y + 8), Color.White);

        IEnumerable<int> topItems = orderedItemIds
            .OrderByDescending(id => state.Demand.GetValueOrDefault(id, 1f) / (state.Supply.GetValueOrDefault(id, 1f) + 1f))
            .Take(RecommendationsToShow);

        int line = 0;
        foreach (int id in topItems)
        {
            float demand = state.Demand.GetValueOrDefault(id, 1f);
            float supply = state.Supply.GetValueOrDefault(id, 1f);
            int basePrice = Math.Max(1, state.BasePriceByItem.GetValueOrDefault(id, 1));

            string rec = priceModel.GetRecommendation(id, basePrice);
            Color color = GetRecommendationColor(rec);
            string name = GetItemName(id);

            string row = $"{name} | D={demand:0.0} S={supply:0.0} -> {rec}";
            Vector2 position = new(area.X + 8, area.Y + 34 + line * 24);
            Game1.drawString(Game1.smallFont, row, position, color);

            line++;
        }
    }

    private static Color GetRecommendationColor(string recommendation)
    {
        return recommendation switch
        {
            "SELL" => Color.LimeGreen,
            "HOLD" => Color.Yellow,
            "AVOID" => Color.Red,
            _ => Color.White
        };
    }

    private bool TryRefreshItems()
    {
        orderedItemIds.Clear();

        HashSet<int> ids = new();
        foreach (int id in state.PriceHistory.Keys)
            ids.Add(id);
        foreach (int id in state.Demand.Keys)
            ids.Add(id);
        foreach (int id in state.Supply.Keys)
            ids.Add(id);
        foreach (int id in state.BasePriceByItem.Keys)
            ids.Add(id);

        if (ids.Count == 0)
            return false;

        orderedItemIds.AddRange(ids.OrderBy(id => id));

        if (selectedItemIndex >= orderedItemIds.Count)
            selectedItemIndex = 0;

        return true;
    }

    private void DrawGraphGrid(SpriteBatch b, Rectangle graphRect)
    {
        DrawRect(b, graphRect, Color.Black * 0.25f);

        const int vLines = 6;
        const int hLines = 5;

        for (int i = 0; i <= vLines; i++)
        {
            float t = i / (float)vLines;
            float x = MathHelper.Lerp(graphRect.Left, graphRect.Right, t);
            DrawLine(b, x, graphRect.Top, x, graphRect.Bottom, Color.Gray * 0.35f, 1f);
        }

        for (int i = 0; i <= hLines; i++)
        {
            float t = i / (float)hLines;
            float y = MathHelper.Lerp(graphRect.Top, graphRect.Bottom, t);
            DrawLine(b, graphRect.Left, y, graphRect.Right, y, Color.Gray * 0.35f, 1f);
        }

        DrawRectOutline(b, graphRect, Color.Silver);
    }

    private void DrawGraphLabels(Rectangle graphRect, float min, float max, int pointCount)
    {
        Game1.drawString(Game1.tinyFont, $"{max:0.00}", new Vector2(graphRect.Left - 44, graphRect.Top - 8), Color.WhiteSmoke);
        Game1.drawString(Game1.tinyFont, $"{min:0.00}", new Vector2(graphRect.Left - 44, graphRect.Bottom - 12), Color.WhiteSmoke);
        Game1.drawString(Game1.tinyFont, $"{Math.Max(1, pointCount)} pts", new Vector2(graphRect.Right - 42, graphRect.Bottom + 4), Color.WhiteSmoke);
    }

    private void DrawSmoothLine(SpriteBatch b, Rectangle graphRect, IReadOnlyList<float> data, float min, float max, Color color)
    {
        float range = Math.Max(0.01f, max - min);
        int samples = data.Count;

        Vector2? prev = null;

        for (int i = 0; i < samples - 1; i++)
        {
            float start = data[i];
            float end = data[i + 1];

            const int segments = 5;
            for (int s = 0; s <= segments; s++)
            {
                float alpha = s / (float)segments;
                float value = MathHelper.Lerp(start, end, alpha);
                float x = graphRect.X + ((i + alpha) / (samples - 1f)) * graphRect.Width;
                float y = graphRect.Bottom - ((value - min) / range) * graphRect.Height;

                Vector2 current = new(x, y);
                if (prev.HasValue)
                    DrawLine(b, prev.Value.X, prev.Value.Y, current.X, current.Y, color, 2f);

                prev = current;
            }
        }
    }

    private void DrawEmptyMessage()
    {
        SpriteBatch b = Game1.spriteBatch;
        Rectangle panel = new(48, 48, 560, 120);

        DrawRect(b, panel, Color.Black * 0.7f);
        DrawRectOutline(b, panel, Color.SaddleBrown);

        Game1.drawString(Game1.smallFont, "Dynamic Market", new Vector2(panel.X + 12, panel.Y + 10), Color.White);
        Game1.drawString(Game1.smallFont, "No tracked items yet. Sell or wait for daily demand updates.", new Vector2(panel.X + 12, panel.Y + 44), Color.Gainsboro);
    }

    private string GetItemName(int itemId)
    {
        try
        {
            return ItemRegistry.GetDataOrErrorItem($"(O){itemId}").DisplayName;
        }
        catch (Exception ex)
        {
            monitor.Log($"Failed to resolve item name for {itemId}: {ex.Message}", LogLevel.Trace);
            return $"Item {itemId}";
        }
    }

    private static void DrawRect(SpriteBatch b, Rectangle rect, Color color)
    {
        b.Draw(Game1.staminaRect, rect, color);
    }

    private static void DrawRectOutline(SpriteBatch b, Rectangle rect, Color color)
    {
        DrawLine(b, rect.Left, rect.Top, rect.Right, rect.Top, color, 2f);
        DrawLine(b, rect.Right, rect.Top, rect.Right, rect.Bottom, color, 2f);
        DrawLine(b, rect.Right, rect.Bottom, rect.Left, rect.Bottom, color, 2f);
        DrawLine(b, rect.Left, rect.Bottom, rect.Left, rect.Top, color, 2f);
    }

    private static void DrawLine(SpriteBatch b, float x1, float y1, float x2, float y2, Color color, float thickness)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = (float)Math.Sqrt((dx * dx) + (dy * dy));
        float angle = (float)Math.Atan2(dy, dx);

        b.Draw(
            Game1.staminaRect,
            new Vector2(x1, y1),
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }
}
