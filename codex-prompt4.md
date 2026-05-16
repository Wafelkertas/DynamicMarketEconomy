Refactor and polish the DynamicMarketEconomy MarketUI UX and layout based on the current implementation screenshot.

The current UI direction is GOOD, but the interface still feels visually crowded and difficult to scan quickly.

GOAL:
- Improve readability
- Improve spacing and hierarchy
- Make the UI feel polished and “official”
- Reduce visual clutter
- Make market data easier to understand at a glance
- Keep Stardew Valley visual style

Use:
- IClickableMenu
- Game1.menuTexture
- drawTextureBox()
- SpriteText.drawString()

Reference:
- Stardew Valley collections menu
- Stardew Valley shop UI
- modern dashboard UX

Do NOT redesign from scratch.
This is a polish/refinement pass.

---

CURRENT PROBLEMS TO FIX

1. Information density is too high
- sections feel cramped
- little breathing room
- everything competes for attention

2. Insights section is unreadable
Current:
Hot: Herring Crash: Amethyst Stone Volatile...

Feels like one giant text blob.

3. Category tabs are cramped
- buttons overlap visually
- difficult to distinguish active filter

4. Table columns feel compressed
- headers collide visually
- difficult to scan rows

5. Graph legend overlaps graph
- creates clutter
- graph area feels cramped

6. Search bar is too small
- visually weak
- disconnected from table

7. Graph labels are clipped
- top-left labels overlap graph edge

8. Layout lacks hierarchy
- headers, rows, and metadata feel visually equal

---

UX REFINEMENT REQUIREMENTS

1. IMPROVE SPACING + PADDING

Add:
- larger vertical spacing between sections
- more internal panel padding
- cleaner separation between graph/table/insights

Target:
- cozy dashboard feel
- not spreadsheet feel

---

2. REDESIGN MARKET INSIGHTS PANEL

Replace inline sentence layout with mini information cards.

OLD:
Hot: Herring Crash: Amethyst Stone Volatile...

NEW:

+-----------------------------+
| 🔥 Hottest                  |
| Herring (+1.6%)             |
+-----------------------------+

+-----------------------------+
| 💥 Biggest Crash            |
| Amethyst Stone (-6.0%)      |
+-----------------------------+

+-----------------------------+
| 📈 Most Volatile            |
| Amethyst Stone              |
+-----------------------------+

+-----------------------------+
| 🛡 Most Stable              |
| Herring                     |
+-----------------------------+

Cards should:
- have spacing
- align cleanly
- use Stardew-style panel backgrounds

---

3. FIX CATEGORY FILTER TABS

Current tabs:
All Crops Fish Mineral Artisan Food

Problems:
- cramped
- hard to distinguish
- weak active state

NEW:
- proper clickable buttons
- spacing between tabs
- hover highlight
- selected tab highlight

Use:
- small Stardew-style button boxes

---

4. IMPROVE TABLE LAYOUT

Refactor item table:

Current columns are too wide and uneven.

NEW COLUMNS:

| Icon | Item | Price | D | S | Action | Trend |

Where:
D = Demand
S = Supply

Use:
- fixed-width columns
- aligned values
- consistent spacing

Rows should:
- highlight on hover
- highlight selected row
- support mouse scrolling

---

5. MOVE GRAPH LEGEND

Current legend overlaps graph.

Move legend:
- BELOW graph
OR
- RIGHT SIDEBAR

Legend should:
- show line color
- item name
- current trend %

Do NOT draw legend directly inside graph area.

---

6. IMPROVE GRAPH READABILITY

Add:
- proper margins
- left/right graph padding
- top margin for labels
- smoother lines
- lighter grid lines

Fix clipped labels:
- no text should overlap graph border

Add:
- hover tooltip:
    "Herring | Day 14 | +1.6%"

---

7. ENLARGE SEARCH BAR

Current search box is too small.

NEW:
- full-width search bar above table
- clear visual prominence
- placeholder text:
    "Search items..."

Search should:
- filter table live
- support case-insensitive matching

---

8. IMPROVE VISUAL HIERARCHY

Headers:
- larger font
- darker color

Metadata:
- smaller font
- muted color

Recommendations:
- green = SELL
- red = AVOID
- yellow = HOLD

Important numbers should visually stand out.

---

9. WINDOW SIZE + STRUCTURE

Slightly increase menu height:
+10–15%

Reduce visual crowding.

Ensure:
- graph area feels balanced
- table has enough space
- insights section breathes properly

---

10. ADD EMPTY STATES

If section has no data:

Show:
- "No significant gainers today"
- "No significant losers today"

Never leave panels empty.

---

11. KEEP EXISTING SYSTEMS

Do NOT rewrite:
- market simulation
- demand/supply logic
- pricing logic
- save logic

Only improve:
- UI layout
- spacing
- rendering
- UX
- readability

---

12. PERFORMANCE

- cache text layouts
- cache graph geometry
- avoid rebuilding UI every frame

Support hundreds of items smoothly.

---

OUTPUT:

Provide:
- polished MarketUI layout
- improved spacing
- redesigned insights cards
- fixed graph layout
- improved table spacing
- better category tabs
- cleaner visual hierarchy
- Stardew Valley style polish

---

START TASK:
Polish and refine the current MarketUI into a cleaner, more readable, and more professional Stardew Valley styled economy dashboard.