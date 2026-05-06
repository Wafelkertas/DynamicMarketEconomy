namespace DynamicMarketEconomy;

using StardewModdingAPI;
using StardewValley;

/// <summary>Handles UI input/render interactions for the market panel.</summary>
public class MarketUiController
{
    private readonly MarketUI ui;
    private readonly IMonitor monitor;

    public MarketUiController(MarketUI ui, IMonitor monitor)
    {
        this.ui = ui;
        this.monitor = monitor;
    }

    public void Draw()
    {
        ui.Draw();
    }

    public void OnButtonPressed(SButton button)
    {
        if (!Context.IsWorldReady)
            return;

        if (button == SButton.F6)
        {
            ui.Visible = !ui.Visible;
            monitor.Log($"Market UI {(ui.Visible ? "enabled" : "disabled")}", LogLevel.Trace);
            return;
        }

        if (!ui.Visible)
            return;

        if (button == SButton.Right || button == SButton.DPadRight)
            ui.SelectNextItem();
        else if (button == SButton.Left || button == SButton.DPadLeft)
            ui.SelectPreviousItem();
    }
}
