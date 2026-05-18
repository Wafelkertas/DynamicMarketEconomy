namespace DynamicMarketEconomy;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using Microsoft.Xna.Framework.Input;
using StardewValley.ItemTypeDefinitions;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

public class MarketUI
{
    private const int GraphDays = 30;
    private const int TopMoverCount = 3;
    private static readonly Color HeaderTextColor = new(92, 45, 20);
    private static readonly Color BodyTextColor = new(72, 52, 36);
    private static readonly Color MutedTextColor = new(108, 82, 58);
    private static readonly Color DividerColor = new(143, 93, 54);
    private static readonly Color GainTextColor = new(22, 104, 42);
    private static readonly Color LossTextColor = new(150, 32, 28);
    private static readonly Color GraphGridColor = new(132, 86, 50);
    private static readonly Color GraphAxisColor = new(88, 56, 34);
    private static readonly Color ShadowColor = Color.Black * 0.35f;
    private readonly MarketState state;
    private readonly PriceModel priceModel;
    private readonly IMonitor monitor;

    private readonly TextBox searchBox;
    private readonly List<MarketRow> cachedRows = new();
    private readonly List<MarketRow> filteredRows = new();
    private readonly Dictionary<int, List<Vector2>> linePointCache = new();
    private readonly Dictionary<int, List<float>> lineValueCache = new();

    private string lastSearch = string.Empty;
    private string selectedCategory = "All";
    private int tableScrollIndex;
    private bool dragging;
    private Point dragOffset;
    private int highlightedItemId = -1;
    private HoverPoint? hoveredPoint;
    private long cacheTick = -1;
    private float graphMinPercent;
    private float graphMaxPercent;

    private static readonly string[] CategoryTabs = { "All", "Crops", "Fish", "Minerals", "Artisan", "Food" };

    private int panelX = 64;
    private int panelY = 64;
    private readonly int panelW = 1180;
    private readonly int panelH = 900;

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

        int visibleRows = GetVisibleTableRowCount();
        tableScrollIndex = Math.Clamp(tableScrollIndex - Math.Sign(delta), 0, Math.Max(0, filteredRows.Count - visibleRows));
    }

    public void OnLeftClick(int x, int y)
    {
        if (!Visible)
            return;

        Rectangle titleBar = new(panelX, panelY, panelW, 64);
        Rectangle searchRect = GetSearchRect();

        searchBox.Selected = searchRect.Contains(x, y);

        if (TrySelectCategoryTab(x, y))
        {
            searchBox.Selected = false;
            ApplyFilter();
            return;
        }

        if (titleBar.Contains(x, y))
        {
            dragging = true;
            dragOffset = new Point(x - panelX, y - panelY);
            return;
        }

        Rectangle tableRect = GetTableRect();
        if (!tableRect.Contains(x, y))
            return;

        int rowHeight = GetTableRowHeight();
        int row = (y - tableRect.Y - GetTableHeaderHeight()) / rowHeight;
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

        Rectangle graphRect = GetGraphRect();
        DrawOverviewGraph(b, graphRect);

        Rectangle moversRect = GetMoversRect();
        DrawTopMovers(b, moversRect);

        DrawSearch(b, GetSearchRect());
        DrawInsights(b, GetInsightsRect());
        DrawItemTable(b, GetTableRect());
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
            float change = CalculateChangePercent(history);
            ParsedItemData data = GetItemData(id);

            cachedRows.Add(new MarketRow(id, data.DisplayName, mult, demand, supply, rec, change, history, data.Category));
        }

        ApplyFilter();
        BuildGraphCache();
    }

    private void ApplyFilter()
    {
        filteredRows.Clear();
        string filter = (searchBox.Text ?? string.Empty).Trim();
        IEnumerable<MarketRow> query = cachedRows.OrderByDescending(p => p.Multiplier);
        if (!string.Equals(selectedCategory, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => GetCategoryTabName(p.Category) == selectedCategory);

        if (!string.IsNullOrEmpty(filter))
            query = query.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        filteredRows.AddRange(query);
        tableScrollIndex = Math.Clamp(tableScrollIndex, 0, Math.Max(0, filteredRows.Count - GetVisibleTableRowCount()));
    }

    private void BuildGraphCache()
    {
        linePointCache.Clear();
        lineValueCache.Clear();
        List<MarketRow> movers = GetTopRising(5).Concat(GetTopFalling(5)).ToList();
        if (!movers.Any())
            movers = cachedRows.OrderByDescending(r => Math.Abs(r.Change)).Take(5).ToList();

        if (!movers.Any())
            return;

        Dictionary<int, List<float>> percentHistoryByItem = new();
        foreach (MarketRow row in movers)
        {
            List<float> percentHistory = ToPercentHistory(row.History);
            if (percentHistory.Count >= 2)
                percentHistoryByItem[row.Id] = percentHistory;
        }

        if (!percentHistoryByItem.Any())
            return;

        float min = percentHistoryByItem.SelectMany(pair => pair.Value).DefaultIfEmpty(0f).Min();
        float max = percentHistoryByItem.SelectMany(pair => pair.Value).DefaultIfEmpty(0f).Max();
        float padding = Math.Max(1f, (max - min) * 0.12f);
        graphMinPercent = min - padding;
        graphMaxPercent = max + padding;
        float range = Math.Max(0.01f, graphMaxPercent - graphMinPercent);

        foreach (MarketRow row in movers)
        {
            if (!percentHistoryByItem.TryGetValue(row.Id, out List<float>? hist))
                continue;

            List<Vector2> points = new();
            for (int i = 0; i < hist.Count; i++)
            {
                float x = i / (float)(hist.Count - 1);
                float y = (hist[i] - graphMinPercent) / range;
                points.Add(new Vector2(x, y));
            }

            linePointCache[row.Id] = points;
            lineValueCache[row.Id] = hist;
        }
    }

    private void DrawOverviewGraph(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        Rectangle plotRect = GetPlotRect(rect);
        DrawShadowedText(b, Game1.smallFont, "Economy Overview", new Vector2(rect.X + 16, rect.Y + 10), HeaderTextColor);

        if (!linePointCache.Any())
        {
            DrawCenteredText(b, Game1.smallFont, "Not enough market history yet", plotRect, MutedTextColor);
            return;
        }

        b.Draw(Game1.staminaRect, plotRect, new Color(255, 244, 214) * 0.35f);
        DrawGrid(b, plotRect);
        DrawLine(b, plotRect.X, plotRect.Bottom, plotRect.Right, plotRect.Bottom, GraphAxisColor, 2f);
        DrawLine(b, plotRect.X, plotRect.Y, plotRect.X, plotRect.Bottom, GraphAxisColor, 2f);
        DrawGraphLabels(b, rect, plotRect);

        Color[] palette =
        {
            new(0, 122, 62),
            new(202, 89, 0),
            new(0, 92, 170),
            new(176, 35, 122),
            new(128, 100, 0),
            new(184, 35, 35),
            new(100, 55, 160),
            new(0, 116, 128),
            new(105, 70, 34),
            new(70, 70, 70)
        };
        int colorIndex = 0;

        foreach (var kvp in linePointCache)
        {
            Color color = palette[colorIndex++ % palette.Length];
            float thickness = kvp.Key == highlightedItemId ? 5f : 3f;
            DrawLineSeries(b, plotRect, kvp.Value, Color.White * 0.75f, thickness + 2f);
            DrawLineSeries(b, plotRect, kvp.Value, color, thickness);
        }

        DrawGraphLegend(b, rect, palette);
    }

    private void DrawTopMovers(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        int padding = 18;
        int gutter = 28;
        int columnWidth = (rect.Width - (padding * 2) - gutter) / 2;
        Rectangle risingRect = new(rect.X + padding, rect.Y + 10, columnWidth, rect.Height - 20);
        Rectangle fallingRect = new(risingRect.Right + gutter, rect.Y + 10, columnWidth, rect.Height - 20);
        int dividerX = risingRect.Right + (gutter / 2);

        DrawLine(b, dividerX, rect.Y + 14, dividerX, rect.Bottom - 14, DividerColor * 0.6f, 2f);
        DrawMoverColumn(b, risingRect, "Top Rising Items", GetTopRising(3), GainTextColor, true);
        DrawMoverColumn(b, fallingRect, "Top Falling Items", GetTopFalling(3), LossTextColor, false);
    }

    private void DrawSearch(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        DrawShadowedText(b, Game1.smallFont, "Search Market", new Vector2(rect.X + 18, rect.Y + 14), HeaderTextColor);
        searchBox.X = rect.X + 190;
        searchBox.Y = rect.Y + 9;
        searchBox.Width = rect.Width - 212;

        Rectangle boxRect = new(searchBox.X, searchBox.Y, searchBox.Width, 44);
        IClickableMenu.drawTextureBox(b, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height, searchBox.Selected ? Color.White : Color.White * 0.92f);
        if (searchBox.Selected)
            DrawLine(b, boxRect.X + 8, boxRect.Bottom - 7, boxRect.Right - 8, boxRect.Bottom - 7, DividerColor, 2f);

        string searchText = searchBox.Text ?? string.Empty;
        string visibleText = string.IsNullOrEmpty(searchText) ? "Search items..." : TruncateText(Game1.smallFont, searchText, boxRect.Width - 24);
        DrawShadowedText(b, Game1.smallFont, visibleText, new Vector2(boxRect.X + 12, boxRect.Y + 10), string.IsNullOrEmpty(searchText) ? MutedTextColor : BodyTextColor);
    }

    private void DrawInsights(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        DrawShadowedText(b, Game1.smallFont, "Market Insights", new Vector2(rect.X + 16, rect.Y + 10), HeaderTextColor);

        if (!cachedRows.Any())
        {
            Rectangle emptyRect = new(rect.X + 16, rect.Y + 44, rect.Width - 32, rect.Height - 58);
            DrawCenteredText(b, Game1.smallFont, "No market data yet", emptyRect, MutedTextColor);
            return;
        }

        MarketRow hottest = cachedRows.MaxBy(r => r.Change)!;
        MarketRow crash = cachedRows.MinBy(r => r.Change)!;
        MarketRow volatileItem = cachedRows.MaxBy(r => StdDev(r.History))!;
        MarketRow stable = cachedRows.MinBy(r => StdDev(r.History))!;

        int gap = 10;
        int cardWidth = (rect.Width - 42 - gap) / 2;
        int cardHeight = (rect.Height - 58 - gap) / 2;
        int startX = rect.X + 16;
        int startY = rect.Y + 44;

        DrawInsightCard(b, new Rectangle(startX, startY, cardWidth, cardHeight), "Hottest", $"{hottest.Name} ({FormatChangePercent(hottest.Change, includeDirectionWord: false)})", GainTextColor);
        DrawInsightCard(b, new Rectangle(startX + cardWidth + gap, startY, cardWidth, cardHeight), "Biggest Crash", $"{crash.Name} ({FormatChangePercent(crash.Change, includeDirectionWord: false)})", LossTextColor);
        DrawInsightCard(b, new Rectangle(startX, startY + cardHeight + gap, cardWidth, cardHeight), "Most Volatile", volatileItem.Name, HeaderTextColor);
        DrawInsightCard(b, new Rectangle(startX + cardWidth + gap, startY + cardHeight + gap, cardWidth, cardHeight), "Most Stable", stable.Name, HeaderTextColor);
    }

    private void DrawItemTable(SpriteBatch b, Rectangle rect)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        DrawShadowedText(b, Game1.smallFont, "Item List", new Vector2(rect.X + 12, rect.Y + 8), HeaderTextColor);
        DrawCategoryTabs(b, rect);

        int iconX = rect.X + 22;
        int nameX = rect.X + 64;
        int priceX = rect.X + 430;
        int demandX = rect.X + 545;
        int supplyX = rect.X + 620;
        int recX = rect.X + 710;
        int trendX = rect.Right - 160;
        int headerY = rect.Y + 62;

        DrawShadowedText(b, Game1.tinyFont, "Icon", new Vector2(iconX, headerY), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, "Name", new Vector2(nameX, headerY), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, "Price", new Vector2(priceX, headerY), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, "D", new Vector2(demandX, headerY), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, "S", new Vector2(supplyX, headerY), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, "Action", new Vector2(recX, headerY), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, "Trend", new Vector2(trendX, headerY), MutedTextColor);
        DrawLine(b, rect.X + 12, rect.Y + GetTableHeaderHeight() - 6, rect.Right - 12, rect.Y + GetTableHeaderHeight() - 6, DividerColor * 0.6f, 2f);

        int rowHeight = GetTableRowHeight();
        int visibleRows = GetVisibleTableRowCount();
        int nameWidth = priceX - nameX - 18;

        if (!filteredRows.Any())
        {
            Rectangle emptyRect = new(rect.X + 12, rect.Y + GetTableHeaderHeight(), rect.Width - 24, rect.Height - GetTableHeaderHeight() - 12);
            DrawCenteredText(b, Game1.smallFont, "No market items match this view", emptyRect, MutedTextColor);
            return;
        }

        for (int i = 0; i < visibleRows; i++)
        {
            int idx = tableScrollIndex + i;
            if (idx >= filteredRows.Count)
                break;
            MarketRow row = filteredRows[idx];
            int y = rect.Y + GetTableHeaderHeight() + (i * rowHeight);

            Color rowColor = row.Recommendation switch
            {
                "SELL" => Color.DarkGreen,
                "BUY" => Color.ForestGreen,
                "AVOID" => Color.DarkRed,
                _ => Color.Goldenrod
            };

            if (i % 2 == 0)
                b.Draw(Game1.staminaRect, new Rectangle(rect.X + 8, y, rect.Width - 16, rowHeight - 2), Color.White * 0.08f);

            Rectangle rowRect = new(rect.X + 8, y, rect.Width - 16, rowHeight - 2);
            if (rowRect.Contains(Game1.getMouseX(), Game1.getMouseY()))
                b.Draw(Game1.staminaRect, rowRect, Color.Goldenrod * 0.12f);

            if (row.Id == highlightedItemId)
                b.Draw(Game1.staminaRect, new Rectangle(rect.X + 4, y - 2, rect.Width - 8, rowHeight), Color.CornflowerBlue * 0.22f);

            DrawItemIcon(b, row.Id, new Vector2(iconX, y + 5));
            DrawShadowedText(b, Game1.smallFont, TruncateText(Game1.smallFont, row.Name, nameWidth), new Vector2(nameX, y + 3), BodyTextColor);
            DrawShadowedText(b, Game1.smallFont, $"x{row.Multiplier:0.00}", new Vector2(priceX, y + 3), rowColor);
            DrawShadowedText(b, Game1.smallFont, row.Demand.ToString("0.0"), new Vector2(demandX, y + 3), BodyTextColor);
            DrawShadowedText(b, Game1.smallFont, row.Supply.ToString("0.0"), new Vector2(supplyX, y + 3), BodyTextColor);
            DrawShadowedText(b, Game1.smallFont, row.Recommendation, new Vector2(recX, y + 3), rowColor);
            DrawShadowedText(b, Game1.smallFont, FormatTrend(row.Change), new Vector2(trendX, y + 3), row.Change >= 0 ? GainTextColor : LossTextColor);
        }
    }

    private void DrawHoverTooltip(SpriteBatch b)
    {
        if (hoveredPoint is null)
            return;

        string text = $"{hoveredPoint.Name} | Day {hoveredPoint.Day} | {FormatChangePercent(hoveredPoint.Value, includeDirectionWord: false)}";
        Vector2 size = Game1.smallFont.MeasureString(text);
        Rectangle r = new(Game1.getMouseX() + 16, Game1.getMouseY() + 16, (int)size.X + 20, (int)size.Y + 12);
        IClickableMenu.drawTextureBox(b, r.X, r.Y, r.Width, r.Height, Color.White);
        b.DrawString(Game1.smallFont, text, new Vector2(r.X + 10, r.Y + 6), Color.Black);
    }

    private void UpdateHover()
    {
        hoveredPoint = null;
        Rectangle graphRect = GetPlotRect(GetGraphRect());
        Vector2 mouse = new(Game1.getMouseX(), Game1.getMouseY());
        if (!graphRect.Contains(mouse))
            return;

        float bestDistance = 12f;
        HoverPoint? bestPoint = null;
        foreach ((int itemId, List<Vector2> points) in linePointCache)
        {
            if (!lineValueCache.TryGetValue(itemId, out List<float>? values))
                continue;

            MarketRow? row = cachedRows.FirstOrDefault(r => r.Id == itemId);
            if (row is null)
                continue;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 screenPoint = ToScreenPoint(graphRect, points[i]);
                float distance = Vector2.Distance(mouse, screenPoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = new HoverPoint(row.Name, i + 1, values[i]);
                    highlightedItemId = itemId;
                }
            }
        }

        hoveredPoint = bestPoint;
    }

    private Rectangle GetGraphRect() => new(panelX + 24, panelY + 84, panelW - 48, 300);

    private Rectangle GetMoversRect()
    {
        Rectangle graphRect = GetGraphRect();
        int gap = 14;
        int width = (panelW - 48 - gap) / 2;
        return new Rectangle(panelX + 24, graphRect.Bottom + 14, width, GetMoversHeight());
    }

    private Rectangle GetSearchRect()
    {
        Rectangle moversRect = GetMoversRect();
        return new Rectangle(panelX + 24, moversRect.Bottom + 14, panelW - 48, 62);
    }

    private Rectangle GetInsightsRect()
    {
        Rectangle moversRect = GetMoversRect();
        return new Rectangle(moversRect.Right + 14, moversRect.Y, panelW - 48 - moversRect.Width - 14, moversRect.Height);
    }

    private Rectangle GetTableRect()
    {
        Rectangle searchRect = GetSearchRect();
        int y = searchRect.Bottom + 12;
        return new Rectangle(panelX + 24, y, panelW - 48, (panelY + panelH) - y - 24);
    }

    private int GetTableHeaderHeight() => 84;

    private int GetTableRowHeight() => Game1.smallFont.LineSpacing + 10;

    private int GetMoversHeight()
    {
        int titleHeight = Game1.smallFont.LineSpacing;
        int rowHeight = Game1.tinyFont.LineSpacing + 6;
        return 28 + titleHeight + 18 + (TopMoverCount * rowHeight) + 24;
    }

    private int GetVisibleTableRowCount()
    {
        Rectangle tableRect = GetTableRect();
        return Math.Max(1, (tableRect.Height - GetTableHeaderHeight()) / GetTableRowHeight());
    }

    private void DrawMoverColumn(SpriteBatch b, Rectangle rect, string title, IEnumerable<MarketRow> rows, Color accentColor, bool rising)
    {
        List<MarketRow> rowList = rows.ToList();
        DrawShadowedText(b, Game1.smallFont, title, new Vector2(rect.X, rect.Y), HeaderTextColor);
        int dividerY = rect.Y + Game1.smallFont.LineSpacing + 6;
        DrawLine(b, rect.X, dividerY, rect.Right, dividerY, DividerColor * 0.45f, 2f);

        float lineHeight = Game1.tinyFont.LineSpacing + 6;
        float startY = dividerY + 10;
        float valuePadding = 18f;

        if (!rowList.Any())
        {
            string emptyText = rising ? "No significant gainers today" : "No significant losers today";
            DrawShadowedText(b, Game1.tinyFont, emptyText, new Vector2(rect.X, startY + 4), MutedTextColor);
            return;
        }

        int index = 0;
        foreach (MarketRow row in rowList)
        {
            float y = startY + (index++ * lineHeight);
            string valueText = FormatChangePercent(row.Change, includeDirectionWord: false);
            float valueWidth = Game1.tinyFont.MeasureString(valueText).X;
            float valueX = rect.Right - Math.Max(valueWidth, 88f);
            float maxNameWidth = valueX - rect.X - valuePadding;
            string name = TruncateText(Game1.tinyFont, row.Name, maxNameWidth);

            DrawShadowedText(b, Game1.tinyFont, name, new Vector2(rect.X, y), BodyTextColor);
            DrawShadowedText(b, Game1.tinyFont, valueText, new Vector2(rect.Right - valueWidth, y), accentColor);
        }
    }

    private IEnumerable<MarketRow> GetTopRising(int count) => cachedRows.Where(r => r.Change > 0.05f).OrderByDescending(r => r.Change).Take(count);
    private IEnumerable<MarketRow> GetTopFalling(int count) => cachedRows.Where(r => r.Change < -0.05f).OrderBy(r => r.Change).Take(count);

    private static float CalculateChangePercent(List<float> history)
    {
        List<float> visibleHistory = history.TakeLast(GraphDays).ToList();
        if (visibleHistory.Count < 2)
            return 0f;

        float start = Math.Max(0.01f, visibleHistory[0]);
        return ((visibleHistory[^1] - start) / start) * 100f;
    }

    private static List<float> ToPercentHistory(List<float> history)
    {
        List<float> visibleHistory = history.TakeLast(GraphDays).ToList();
        if (visibleHistory.Count < 2)
            return new List<float>();

        float start = Math.Max(0.01f, visibleHistory[0]);
        return visibleHistory.Select(value => ((value - start) / start) * 100f).ToList();
    }

    private static string FormatTrend(float change)
    {
        string direction = change >= 0f ? "Up" : "Down";
        return $"{direction} {FormatAbsPercent(change)}";
    }

    private static string FormatChangePercent(float change, bool includeDirectionWord)
    {
        string sign = change >= 0f ? "+" : "-";
        return includeDirectionWord ? $"{sign}{FormatAbsPercent(change).TrimEnd('%')} percent" : $"{sign}{FormatAbsPercent(change)}";
    }

    private static string FormatAbsPercent(float change)
    {
        float abs = Math.Abs(change);
        return (abs < 10f ? abs.ToString("0.0") : abs.ToString("0")) + "%";
    }

    private static float StdDev(List<float> values)
    {
        if (values.Count < 2) return 0f;
        float avg = values.Average();
        return MathF.Sqrt(values.Sum(v => (v - avg) * (v - avg)) / values.Count);
    }

    private string GetItemName(int itemId)
    {
        try { return GetItemData(itemId).DisplayName; }
        catch { return $"Item {itemId}"; }
    }

    private static ParsedItemData GetItemData(int itemId) => ItemRegistry.GetDataOrErrorItem($"(O){itemId}");

    private static void DrawGrid(SpriteBatch b, Rectangle rect)
    {
        for (int i = 1; i < 6; i++)
        {
            int x = rect.X + (rect.Width * i / 6);
            DrawLine(b, x, rect.Y, x, rect.Bottom, GraphGridColor * 0.32f, 1f);
        }

        for (int i = 1; i < 5; i++)
        {
            int y = rect.Y + (rect.Height * i / 5);
            DrawLine(b, rect.X, y, rect.Right, y, GraphGridColor * 0.32f, 1f);
        }
    }

    private static Rectangle GetPlotRect(Rectangle rect) => new(rect.X + 78, rect.Y + 58, rect.Width - 106, rect.Height - 122);

    private void DrawGraphLabels(SpriteBatch b, Rectangle rect, Rectangle plotRect)
    {
        DrawShadowedText(b, Game1.tinyFont, "30-day % change", new Vector2(plotRect.X, rect.Y + 32), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, FormatChangePercent(graphMaxPercent, includeDirectionWord: false), new Vector2(rect.X + 18, plotRect.Y + 2), MutedTextColor);
        DrawShadowedText(b, Game1.tinyFont, FormatChangePercent(graphMinPercent, includeDirectionWord: false), new Vector2(rect.X + 18, plotRect.Bottom - Game1.tinyFont.LineSpacing - 2), MutedTextColor);
    }

    private void DrawGraphLegend(SpriteBatch b, Rectangle rect, Color[] palette)
    {
        Rectangle plotRect = GetPlotRect(rect);
        int legendX = plotRect.X;
        int legendY = plotRect.Bottom + 16;
        int index = 0;

        foreach (int itemId in linePointCache.Keys)
        {
            MarketRow? row = cachedRows.FirstOrDefault(r => r.Id == itemId);
            if (row is null)
                continue;

            Color color = palette[index++ % palette.Length];
            int column = (index - 1) % 5;
            int rowIndex = (index - 1) / 5;
            int x = legendX + (column * 196);
            int y = legendY + (rowIndex * 22);
            DrawLine(b, x, y + 8, x + 24, y + 8, color, itemId == highlightedItemId ? 5f : 3f);
            string label = $"{row.Name} {FormatChangePercent(row.Change, includeDirectionWord: false)}";
            DrawShadowedText(b, Game1.tinyFont, TruncateText(Game1.tinyFont, label, 160f), new Vector2(x + 32, y), itemId == highlightedItemId ? HeaderTextColor : BodyTextColor);
        }
    }

    private static void DrawInsightCard(SpriteBatch b, Rectangle rect, string title, string value, Color accentColor)
    {
        IClickableMenu.drawTextureBox(b, rect.X, rect.Y, rect.Width, rect.Height, Color.White * 0.95f);
        DrawShadowedText(b, Game1.tinyFont, title, new Vector2(rect.X + 10, rect.Y + 8), MutedTextColor);
        DrawShadowedText(b, Game1.smallFont, TruncateText(Game1.smallFont, value, rect.Width - 20), new Vector2(rect.X + 10, rect.Y + 30), accentColor);
    }

    private void DrawCategoryTabs(SpriteBatch b, Rectangle rect)
    {
        foreach (string tab in CategoryTabs)
        {
            Rectangle tabRect = GetCategoryTabRect(rect, tab);
            bool selected = string.Equals(selectedCategory, tab, StringComparison.OrdinalIgnoreCase);
            bool hovered = tabRect.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color fill = selected ? new Color(232, 160, 76) * 0.65f : hovered ? Color.Goldenrod * 0.18f : Color.White * 0.10f;
            Color border = selected ? DividerColor : hovered ? DividerColor * 0.7f : DividerColor * 0.4f;

            b.Draw(Game1.staminaRect, tabRect, fill);
            DrawLine(b, tabRect.X, tabRect.Y, tabRect.Right, tabRect.Y, border, 1f);
            DrawLine(b, tabRect.X, tabRect.Bottom, tabRect.Right, tabRect.Bottom, border, 1f);
            DrawLine(b, tabRect.X, tabRect.Y, tabRect.X, tabRect.Bottom, border, 1f);
            DrawLine(b, tabRect.Right, tabRect.Y, tabRect.Right, tabRect.Bottom, border, 1f);
            DrawCenteredText(b, Game1.tinyFont, tab, tabRect, selected ? HeaderTextColor : MutedTextColor);
        }
    }

    private bool TrySelectCategoryTab(int x, int y)
    {
        Rectangle tableRect = GetTableRect();
        foreach (string tab in CategoryTabs)
        {
            if (!GetCategoryTabRect(tableRect, tab).Contains(x, y))
                continue;

            selectedCategory = tab;
            return true;
        }

        return false;
    }

    private static Rectangle GetCategoryTabRect(Rectangle tableRect, string tab)
    {
        int index = Array.IndexOf(CategoryTabs, tab);
        int tabWidth = 108;
        int gap = 10;
        int startX = tableRect.Right - ((tabWidth + gap) * CategoryTabs.Length) - 4;
        return new Rectangle(startX + (index * (tabWidth + gap)), tableRect.Y + 10, tabWidth, 28);
    }

    private static string GetCategoryTabName(int category)
    {
        return category switch
        {
            -75 or -79 => "Crops",
            -4 => "Fish",
            -2 or -12 or -15 => "Minerals",
            -26 => "Artisan",
            -7 => "Food",
            _ => "All"
        };
    }

    private static void DrawItemIcon(SpriteBatch b, int itemId, Vector2 position)
    {
        try
        {
            ParsedItemData data = GetItemData(itemId);
            Texture2D texture = data.GetTexture();
            Rectangle source = data.GetSourceRect(0);
            b.Draw(texture, new Rectangle((int)position.X, (int)position.Y, 32, 32), source, Color.White);
        }
        catch
        {
            b.Draw(Game1.staminaRect, new Rectangle((int)position.X + 6, (int)position.Y + 6, 20, 20), MutedTextColor * 0.45f);
        }
    }

    private static void DrawCenteredText(SpriteBatch b, SpriteFont font, string text, Rectangle rect, Color color)
    {
        Vector2 size = font.MeasureString(text);
        Vector2 position = new(rect.X + ((rect.Width - size.X) / 2f), rect.Y + ((rect.Height - size.Y) / 2f));
        DrawShadowedText(b, font, text, position, color);
    }

    private static Vector2 ToScreenPoint(Rectangle rect, Vector2 normalized)
    {
        return new Vector2(rect.X + normalized.X * rect.Width, rect.Bottom - normalized.Y * rect.Height);
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

    private static void DrawShadowedText(SpriteBatch b, SpriteFont font, string text, Vector2 position, Color color)
    {
        b.DrawString(font, text, position + new Vector2(1f, 1f), ShadowColor);
        b.DrawString(font, text, position, color);
    }

    private static string TruncateText(SpriteFont font, string text, float maxWidth)
    {
        if (font.MeasureString(text).X <= maxWidth)
            return text;

        const string ellipsis = "...";
        while (text.Length > 0 && font.MeasureString(text + ellipsis).X > maxWidth)
            text = text[..^1];

        return string.IsNullOrEmpty(text) ? ellipsis : text + ellipsis;
    }

    private record MarketRow(int Id, string Name, float Multiplier, float Demand, float Supply, string Recommendation, float Change, List<float> History, int Category);
    private record HoverPoint(string Name, int Day, float Value);
}
