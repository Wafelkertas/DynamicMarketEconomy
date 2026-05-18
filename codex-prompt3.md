Refactor and polish the DynamicMarketEconomy MarketUI to make it feel like a complete Stardew Valley in-game menu instead of a prototype/debug screen.

CURRENT PROBLEMS:

1. The graph area feels empty and unfinished
- too much unused space
- graph lines look disconnected
- axis labels overlap
- no graph legend
- no hover tooltip
- graph scaling feels awkward

2. Top Rising Items section can become empty
- creates visual imbalance
- looks buggy

3. Search bar feels disconnected
- no visible item table under it
- no clear purpose

4. Layout has poor information density
- large empty spaces
- not enough useful market information

5. No item icons
- UI feels like spreadsheet software
- not like Stardew Valley

6. No category organization
- fish/minerals/crops mixed together
- difficult to scan quickly

7. Text hierarchy is weak
- headers and item rows have similar visual weight

---

GOAL:

Transform the MarketUI into a polished “Town Market Board” inspired by Stardew Valley menus.

STYLE:
Use:
- IClickableMenu patterns :contentReference[oaicite:0]{index=0}
- Game1.menuTexture
- drawTextureBox()
- SpriteText.drawString()
- Stardew Valley color palette
- cozy readable layout

Avoid:
- debug overlay appearance
- overlapping text
- giant empty panels
- raw transparent black rectangles

---

NEW LAYOUT

TOP:
- economy overview graph
- multiple trend lines
- graph legend
- hover tooltip

MIDDLE:
- Top Rising Items
- Top Falling Items
- Market Insights

BOTTOM:
- searchable market table
- scrollable item list
- category filters

---

1. IMPROVE GRAPH

Replace current graph with a more informative economy chart.

REQUIREMENTS:
- smooth line rendering
- subtle background grid
- proper margins/padding
- dynamic Y-axis scaling
- graph legend
- hover tooltip:
    "Tuna | Day 14 | +42%"

Show:
- Top 5 rising items
- Top 5 falling items

Each line should:
- have unique color
- show item name in legend
- support hover highlight

If not enough data:
- display:
    "Not enough market history yet"

instead of empty graph.

---

2. EMPTY STATE HANDLING

If no rising items exist:
show:
    "No significant gainers today"

If no falling items exist:
show:
    "No significant losers today"

Do NOT leave sections empty.

---

3. ADD MARKET TABLE

Under search bar, add scrollable table:

Columns:
- icon
- item name
- multiplier
- demand
- supply
- trend
- recommendation

Example:

[FishIcon] Tuna        x1.42   ↑   SELL
[CropIcon] Wheat       x0.82   ↓   HOLD
[GemIcon] Quartz       x0.71   ↓   AVOID

---

4. ADD ITEM ICONS

Use Stardew object sprites.

Every item row should display:
- 16x16 item icon
- item name

Icons should align vertically with text.

---

5. SEARCH + FILTERING

Search bar should:
- filter item table live
- case-insensitive

Add category tabs:
- All
- Crops
- Fish
- Minerals
- Artisan Goods
- Food

---

6. MARKET INSIGHTS PANEL

Add compact insights section:

Show:
- hottest item
- biggest crash
- most volatile
- strongest category
- weakest category

Example:

Hottest: Coffee (+42%)
Crash: Sardine (-18%)
Most Volatile: Quartz
Strongest Category: Fish

---

7. TEXT HIERARCHY

Improve typography:

- larger headers
- smaller metadata
- stronger spacing
- aligned columns

Use:
- darker section headers
- colored recommendations:
    green = SELL
    red = AVOID
    yellow = HOLD

---

8. WINDOW SIZE + SPACING

Reduce wasted space.

UI should:
- feel dense but readable
- avoid giant empty areas
- maintain Stardew menu proportions

---

9. INTERACTION

- click row → highlight graph line
- hover graph line → tooltip
- mouse wheel scrolls table
- ESC closes UI
- F6 toggles UI
- draggable window

---

10. PERFORMANCE

- cache graph geometry
- cache filtered item rows
- avoid rebuilding every frame
- support hundreds of items

---

11. KEEP EXISTING SYSTEMS

Do NOT rewrite:
- market simulation
- supply/demand
- price logic
- save logic

Only refactor UI and visualization.

---

OUTPUT:

Provide:
- polished MarketUI refactor
- economy graph improvements
- market table
- category filtering
- item icons
- hover tooltips
- improved layout spacing
- Stardew Valley visual styling

---

START TASK:
Transform the current MarketUI into a polished Stardew Valley styled market dashboard with a dense and informative economy overview.