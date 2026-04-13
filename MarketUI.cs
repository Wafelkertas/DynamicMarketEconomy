using Microsoft.Xna.Framework;
using StardewValley;
using System.Linq;

public class MarketUI
{
    private readonly MarketState state;
    public bool Visible = true;

    public MarketUI(MarketState state)
    {
        this.state = state;
    }

    public void Draw()
    {
        if (!Visible) return;

        int x = 20;
        int y = 20;

        foreach (var pair in state.Demand.Take(10))
        {
            string text = $"Item {pair.Key}: D={pair.Value:F2} S={state.GetSupply(pair.Key):F2}";
            Game1.spriteBatch.DrawString(Game1.smallFont, text, new Vector2(x, y), Color.White);
            y += 20;
        }
    }
}