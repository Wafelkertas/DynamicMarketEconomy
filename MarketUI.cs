namespace DynamicMarketEconomy;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

public class MarketUI
{
    private readonly MarketState state;

    public bool Visible = false;

    public MarketUI(MarketState state)
    {
        this.state = state;
    }

    public void Draw()
    {
        if (!Visible) return;

        var b = Game1.spriteBatch;

        int startX = 50;
        int startY = 50;
        int width = 400;
        int height = 200;

        // background
        b.Draw(Game1.staminaRect, new Rectangle(startX, startY, width, height), Color.Black * 0.6f);

        int itemId = 24; // example: Parsnip

        if (!state.PriceHistory.ContainsKey(itemId))
            return;

        var history = state.PriceHistory[itemId];

        if (history.Count < 2)
            return;

        float max = history.Max();
        float min = history.Min();

        for (int i = 1; i < history.Count; i++)
        {
            float prev = history[i - 1];
            float curr = history[i];

            float x1 = startX + (i - 1) * (width / (float)history.Count);
            float x2 = startX + i * (width / (float)history.Count);

            float y1 = startY + height - ((prev - min) / (max - min + 0.01f)) * height;
            float y2 = startY + height - ((curr - min) / (max - min + 0.01f)) * height;

            DrawLine(b, x1, y1, x2, y2, Color.Lime);
        }

        Game1.drawString(Game1.smallFont, "Parsnip Market", new Vector2(startX, startY - 20), Color.White);
    }

    private void DrawLine(SpriteBatch b, float x1, float y1, float x2, float y2, Color color)
    {
        var tex = Game1.staminaRect;

        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = (float)Math.Sqrt(dx * dx + dy * dy);
        float angle = (float)Math.Atan2(dy, dx);

        b.Draw(tex,
            new Vector2(x1, y1),
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, 2),
            SpriteEffects.None,
            0f);
    }
}