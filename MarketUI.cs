namespace DynamicMarketEconomy;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System.Linq;

public class MarketUI
{
    private readonly MarketState state;
    public bool Visible = true;

    private Texture2D pixel;

    public MarketUI(MarketState state)
    {
        this.state = state;
    }

    private void InitPixel()
    {
        if (pixel != null) return;

        pixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
    }

    public void Draw()
    {
        if (!Visible) return;

        InitPixel();

        int x = 20;
        int y = 20;

        var topItems = state.Demand.Take(3).Select(p => p.Key);

        foreach (var itemId in topItems)
        {
            DrawGraph(itemId, x, y);
            y += 100;
        }
    }

    private void DrawGraph(int itemId, int x, int y)
    {
        if (!state.PriceHistory.ContainsKey(itemId))
            return;

        var data = state.PriceHistory[itemId].ToArray();
        if (data.Length < 2) return;

        float max = data.Max();
        float min = data.Min();
        float range = max - min;
        if (range == 0) range = 1;

        int width = 200;
        int height = 60;

        // draw background
        Game1.spriteBatch.Draw(pixel, new Rectangle(x, y, width, height), Color.Black * 0.5f);

        for (int i = 1; i < data.Length; i++)
        {
            float prev = (data[i - 1] - min) / range;
            float curr = (data[i] - min) / range;

            int x1 = x + (i - 1) * width / data.Length;
            int x2 = x + i * width / data.Length;

            int y1 = y + height - (int)(prev * height);
            int y2 = y + height - (int)(curr * height);

            DrawLine(x1, y1, x2, y2, Color.Lime);
        }

        // label
        Game1.spriteBatch.DrawString(
            Game1.smallFont,
            $"Item {itemId}",
            new Vector2(x, y - 15),
            Color.White
        );
    }

    private void DrawLine(int x1, int y1, int x2, int y2, Color color)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = (float)System.Math.Sqrt(dx * dx + dy * dy);

        float angle = (float)System.Math.Atan2(dy, dx);

        Game1.spriteBatch.Draw(
            pixel,
            new Vector2(x1, y1),
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, 2),
            SpriteEffects.None,
            0
        );
    }
}