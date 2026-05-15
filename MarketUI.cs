namespace DynamicMarketEconomy;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using xTile.Dimensions;

public class MarketUI
{
    private const int GraphDays = 30;
    private readonly MarketState state;
    private readonly PriceModel priceModel;
    private readonly IMonitor monitor;

    private readonly TextBox searchBox;
    private readonly List<MarketRow> cachedRows = new();
    private readonly List<MarketRow> filteredRows = new();
    private readonly Dictionary<int, List<Vector2>> linePointCache = new();

    private string lastSearch = string.Empty;
    private int tableScrollIndex;
    private bool dragging;
    private Point dragOffset;
    private int highlightedItemId = -1;
    private HoverPoint? hoveredPoint;
    private long cacheTick = -1;

    private int panelX = 64;
    private int panelY = 64;
    private readonly int panelW = 1180;
    private readonly int panelH = 680;

    public bool Visible { get; set; }

    public MarketUI(MarketState state, PriceModel priceModel, IMonitor monitor)
    {
        this.state = state;
        this.priceModel = priceModel;
        this.monitor = monitor;
        this.searchBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
        {
            Text = string.Empty,
            Selected = false
        };
    }

    public void OnButtonPressed(SButton button)
    {
        if (!Visible)
            return;

        if (button == SButton.Escape)
        {
            Visible = false;
            return;
        }

        searchBox.RecieveTextInput(button.TryGetKeyboard(out Keys key) ? key.ToString() : string.Empty);
    }

    public void OnMouseWheelScrolled(int delta)
    {
        if (!Visible || !filteredRows.Any())
            return;

        tableScrollIndex = Math.Clamp(tableScrollIndex - Math.Sign(delta), 0, Math.Max(0, filteredRows.Count - 10));
    }

    public void OnLeftClick(int x, int y)
    {
        if (!Visible)
            return;

        Rectangle titleBar = new(panelX, panelY, panelW, 64);
        Rectangle searchRect = new(panelX + 20, panelY + 478, 400, 52);

        searchBox.Selected = searchRect.Contains(x, y);

        if (titleBar.Contains(x, y))
        {
            dragging = true;
            dragOffset = new Point(x - panelX, y - panelY);
            return;
        }

        Rectangle tableRect = new(panelX + 20, panelY + 540, panelW - 40, panelH - 560);
        if (!tableRect.Contains(x, y))
            return;

        int rowHeight = 34;
        int row = (y - tableRect.Y - 34) / rowHeight;
        int idx = tableScrollIndex + row;
        if (row >= 0 && idx >= 0 && idx < filteredRows.Count)
            highlightedItemId = filteredRows[idx].Id;
    }

    public void OnLeftReleased() => dragging = false;

    public void Update()
    {
        if (!Visible)
            return;

        if (dragging)
        {
            panelX = Game1.getMouseX() - dragOffset.X;
            panelY = Game1.getMouseY() - dragOffset.Y;
        }

        searchBox.Update();
        RefreshCacheIfNeeded();
        UpdateHover();
    }

    public void Draw()
    {
        if (!Visible)
            return;

        Update();
        SpriteBatch b = Game1.spriteBatch;

        IClickableMenu.drawTextureBox(b, panelX, panelY, panelW, panelH, Color.White);
        SpriteText.drawString(b, "Dynamic Market Economy", panelX + 20, panelY + 20, 999999, -1, 999, 1f, 0.1f, false, -1, "");

        Rectangle graphRect = new(panelX + 20, panelY + 84, panelW - 40, 260);
        DrawOverviewGraph(b, graphRect);

        Rectangle moversRect = new(panelX + 20, panelY + 356, panelW - 40, 108);
        DrawTopMovers(b, moversRect);

        DrawSearch(b, new Rectangle(panelX + 20, panelY + 478, 400, 52));
        DrawInsights(b, new Rectangle(panelX + 440, panelY + 478, panelW - 460, 52));
        DrawItemTable(b, new Rectangle(panelX + 20, panelY + 540, panelW - 40, panelH - 560));
        DrawHoverTooltip(b);
    }

    private void RefreshCacheIfNeeded()
    {
        long stamp = state.PriceHistory.Count + state.Demand.Count + state.Supply.Count + state.BasePriceByItem.Count;
        bool searchChanged = !string.Equals(lastSearch, searchBox.Text, StringComparison.OrdinalIgnoreCase);
        if (stamp == cacheTick && !searchChanged)
            return;

        cacheTick = stamp;
        lastSearch = searchBox.Text ?? string.Empty;
        cachedRows.Clear();

        HashSet<int> ids = new();
        foreach (int id in state.PriceHistory.Keys) ids.Add(id);
        foreach (int id in state.Demand.Keys) ids.Add(id);
        foreach (int id in state.Supply.Keys) ids.Add(id);
        foreach (int id in state.BasePriceByItem.Keys) ids.Add(id);

        foreach (int id in ids)
        {
            float demand = state.Demand.GetValueOrDefault(id, 1f);
            float supply = state.Supply.GetValueOrDefault(id, 1f);
            float mult = demand / (supply + 1f);
            int basePrice = Math.Max(1, state.BasePriceByItem.GetValueOrDefault(id, 1));
            string rec = priceModel.GetRecommendation(id, basePrice);
            List<float> history = state.PriceHistory.GetValueOrDefault(id, new List<float>());
            float change = history.Count > 1 ? history[^1] - history[Math.Max(0, history.Count - GraphDays)] : 0f;

            cachedRows.Add(new MarketRow(id, GetItemName(id), mult, demand, supply, rec, change, history));
        }

        ApplyFilter();
        BuildGraphCache();
    }

    private void ApplyFilter()
    {
        filteredRows.Clear();
        string filter = (searchBox.Text ?? string.Empty).Trim();
        IEnumerable<MarketRow> query = cachedRows.OrderByDescending(p => p.Multiplier);
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        filteredRows.AddRange(query);
        tableScrollIndex = Math.Clamp(tableScrollIndex, 0, Math.Max(0, filteredRows.Count - 10));
    }

    private void BuildGraphCache()
    {
        linePointCache.Clear();
        List<MarketRow> movers = GetTopRising(5).Concat(GetTopFalling(5)).ToList();
        if (!movers.Any())
            return;

        float min = movers.SelectMany(r => r.History.TakeLast(GraphDays)).DefaultIfEmpty(0.5f).Min();
        float max = movers.SelectMany(r => r.History.TakeLast(GraphDays)).DefaultIfEmpty(1.5f).Max();
        float range = Math.Max(0.01f, max - min);

        foreach (MarketRow row in movers)
        {
            List<float> hist = row.History.TakeLast(GraphDays).ToList();
            if (hist.Count < 2)
                continue;

            List<Vector2> points = new();
            for (int i = 0; i < hist.Count; i++)
            {
                float x = i / (float)(hist.Count - 1);
                float y = (hist[i] - min) / range;
                points.Add(new Vector2(x, y));
            }

            linePointCache[row.Id] = points;
        }
    }

    private void DrawOverviewGraph(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        DrawGrid(b, rect);

        Color[] palette = { Color.LimeGreen, Color.Orange, Color.DeepSkyBlue, Color.HotPink, Color.Gold, Color.Red, Color.Crimson, Color.IndianRed, Color.Purple, Color.CadetBlue };
        int colorIndex = 0;

        foreach (var kvp in linePointCache)
        {
            Color color = palette[colorIndex++ % palette.Length];
            float thickness = kvp.Key == highlightedItemId ? 4f : 2f;
            DrawLineSeries(b, rect, kvp.Value, color, thickness);
        }
    }

    private void DrawTopMovers(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        SpriteText.drawString(b, "Top Rising Items", rect.X + 16, rect.Y + 12);
        SpriteText.drawString(b, "Top Falling Items", rect.X + rect.Width / 2 + 16, rect.Y + 12);

        int i = 0;
        foreach (MarketRow row in GetTopRising(3))
            b.DrawString(Game1.smallFont, $"{row.Name}  +{row.Change:0.##}% ↑", new Vector2(rect.X + 16, rect.Y + 42 + (i++ * 20)), Color.LimeGreen);
        i = 0;
        foreach (MarketRow row in GetTopFalling(3))
            b.DrawString(Game1.smallFont, $"{row.Name}  {row.Change:0.##}% ↓", new Vector2(rect.X + rect.Width / 2 + 16, rect.Y + 42 + (i++ * 20)), Color.IndianRed);
    }

    private void DrawSearch(SpriteBatch b, Rectangle rect)
    {
        SpriteText.drawString(b, "Search:", rect.X, rect.Y + 8);
        searchBox.X = rect.X + 110;
        searchBox.Y = rect.Y + 6;
        searchBox.Width = rect.Width - 120;
        searchBox.Draw(b);
    }

    private void DrawInsights(SpriteBatch b, Rectangle rect)
    {
        if (!cachedRows.Any())
            return;
        MarketRow hottest = cachedRows.MaxBy(r => r.Change)!;
        MarketRow crash = cachedRows.MinBy(r => r.Change)!;
        MarketRow volatileItem = cachedRows.MaxBy(r => StdDev(r.History))!;
        MarketRow stable = cachedRows.MinBy(r => StdDev(r.History))!;
        b.DrawString(Game1.tinyFont, $"Hot: {hottest.Name} | Crash: {crash.Name} | Volatile: {volatileItem.Name} | Stable: {stable.Name}", new Vector2(rect.X, rect.Y + 18), Color.SaddleBrown);
    }

    private void DrawItemTable(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        b.DrawString(Game1.smallFont, "Item List", new Vector2(rect.X + 12, rect.Y + 8), Color.Black);
        b.DrawString(Game1.tinyFont, "Name        Mult   Demand Supply Rec", new Vector2(rect.X + 60, rect.Y + 24), Color.DarkSlateGray);

        int rowHeight = 34;
        for (int i = 0; i < 10; i++)
        {
            int idx = tableScrollIndex + i;
            if (idx >= filteredRows.Count)
                break;
            MarketRow row = filteredRows[idx];
            int y = rect.Y + 44 + (i * rowHeight);

            Color rowColor = row.Recommendation switch
            {
                "SELL" => Color.DarkGreen,
                "BUY" => Color.ForestGreen,
                "AVOID" => Color.DarkRed,
                _ => Color.Goldenrod
            };

            if (row.Id == highlightedItemId)
                b.Draw(Game1.staminaRect, new Rectangle(rect.X + 4, y - 2, rect.Width - 8, rowHeight), Color.CornflowerBlue * 0.22f);

            b.DrawString(Game1.smallFont, row.Name, new Vector2(rect.X + 60, y), Color.Black);
            b.DrawString(Game1.smallFont, $"x{row.Multiplier:0.00}", new Vector2(rect.X + 340, y), rowColor);
            b.DrawString(Game1.smallFont, row.Demand.ToString("0.0"), new Vector2(rect.X + 430, y), Color.Black);
            b.DrawString(Game1.smallFont, row.Supply.ToString("0.0"), new Vector2(rect.X + 520, y), Color.Black);
            b.DrawString(Game1.smallFont, row.Recommendation, new Vector2(rect.X + 620, y), rowColor);
            b.DrawString(Game1.smallFont, row.Change >= 0 ? "↑" : "↓", new Vector2(rect.X + 760, y), row.Change >= 0 ? Color.Green : Color.Red);
        }
    }

    private void DrawHoverTooltip(SpriteBatch b)
    {
        if (hoveredPoint is null)
            return;

        string text = $"{hoveredPoint.Name} - Day {hoveredPoint.Day} - x{hoveredPoint.Value:0.00}";
        Vector2 size = Game1.smallFont.MeasureString(text);
        Rectangle r = new(Game1.getMouseX() + 16, Game1.getMouseY() + 16, (int)size.X + 20, (int)size.Y + 12);
        IClickableMenu.drawTextureBox(b, r.X, r.Y, r.Width, r.Height, Color.White);
        b.DrawString(Game1.smallFont, text, new Vector2(r.X + 10, r.Y + 6), Color.Black);
    }

    private void UpdateHover()
    {
        hoveredPoint = null;
        Rectangle graphRect = new(panelX + 20, panelY + 84, panelW - 40, 260);
        Vector2 mouse = new(Game1.getMouseX(), Game1.getMouseY());
        if (!graphRect.Contains(mouse))
            return;
    }

    private IEnumerable<MarketRow> GetTopRising(int count) => cachedRows.OrderByDescending(r => r.Change).Take(count);
    private IEnumerable<MarketRow> GetTopFalling(int count) => cachedRows.OrderBy(r => r.Change).Take(count);

    private static float StdDev(List<float> values)
    {
        if (values.Count < 2) return 0f;
        float avg = values.Average();
        return MathF.Sqrt(values.Sum(v => (v - avg) * (v - avg)) / values.Count);
    }

    private string GetItemName(int itemId)
    {
        try { return ItemRegistry.GetDataOrErrorItem($"(O){itemId}").DisplayName; }
        catch { return $"Item {itemId}"; }
    }

    private static void DrawGrid(SpriteBatch b, Rectangle rect)
    {
        for (int i = 1; i < 6; i++)
        {
            int x = rect.X + (rect.Width * i / 6);
            DrawLine(b, x, rect.Y + 8, x, rect.Bottom - 8, Color.BurlyWood * 0.4f, 1f);
        }

        for (int i = 1; i < 5; i++)
        {
            int y = rect.Y + (rect.Height * i / 5);
            DrawLine(b, rect.X + 8, y, rect.Right - 8, y, Color.BurlyWood * 0.4f, 1f);
        }
    }

    private static void DrawLineSeries(SpriteBatch b, Rectangle rect, List<Vector2> normPoints, Color color, float thickness)
    {
        for (int i = 1; i < normPoints.Count; i++)
        {
            Vector2 p1 = new(rect.X + normPoints[i - 1].X * rect.Width, rect.Bottom - normPoints[i - 1].Y * rect.Height);
            Vector2 p2 = new(rect.X + normPoints[i].X * rect.Width, rect.Bottom - normPoints[i].Y * rect.Height);
            DrawLine(b, p1.X, p1.Y, p2.X, p2.Y, color, thickness);
        }
    }

    private static void DrawLine(SpriteBatch b, float x1, float y1, float x2, float y2, Color color, float thickness)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        float angle = MathF.Atan2(dy, dx);
        b.Draw(Game1.staminaRect, new Vector2(x1, y1), null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private record MarketRow(int Id, string Name, float Multiplier, float Demand, float Supply, string Recommendation, float Change, List<float> History);
    private record HoverPoint(string Name, int Day, float Value);
}
