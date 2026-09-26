# Spec — Needs, plants, farming, items, weather (ECO)

## Time

- **ECO-01** 10 ticks = 1 game second at 1×. `TicksPerDay = 2400`. `Clock.Day = tick / 2400`.

## Needs

- **ECO-02** `hunger` and `thirst` range `0..10000` (10000 = satisfied). Start at 10000.
- **ECO-03** Decay per tick: hunger −1 (empty in ~4.2 days), thirst −2 (empty in ~2.1 days).
- **ECO-04** Thresholds: at `< 4000` the agent posts its own Eat/Drink job (JOB-07). If no storage has the item,
  it retries every 100 ticks and the HUD shows "No food" / "No water". A failed Drink/Eat job also waits 100 ticks
  before that need is tried again (ADR-043).
- **ECO-05** Consume: eating 1 berry restores 2500; 1 potato restores 4000; drinking 1 water restores 5000. The
  agent consumes one unit at a time until the need is ≥ 9000 or storage runs out.
- **ECO-06** At 0 hunger or thirst, `health` (0..1000, starts 1000) drops 1 per tick. Health regenerates 1 per
  10 ticks when both needs are > 0. At health 0 the agent dies (`DeathCause.Starved` / `Dehydrated`,
  whichever need is 0; thirst wins ties). A trapped (drowning, WAT-14) agent does not regenerate (ADR-043).
- **ECO-07** When every agent is dead, emit `ColonyLost` once. The sim keeps ticking (water still flows).

## Items (from `data/items.json`)

| id | Food value | Storable in |
|---|---|---|
| `log` | – | hub, warehouse |
| `stone` | – | hub, warehouse |
| `berries` | 2500 | hub, warehouse |
| `potato` | 4000 | hub, warehouse |
| `water` | drink 5000 | hub, pump buffer |

- **ECO-08** Item piles: at most one item type per cell, unlimited count. Dropping a different type onto an
  occupied pile cell uses the nearest free standable cell within radius 3 (deterministic spiral order).

## Plants

- **ECO-09** Trees: entity with base cell and trunk height 4. Chop drops 4 logs at the base cell. No regrowth.
- **ECO-10** Berry bushes: states `Ripe(2 berries)` → harvested → `Growing(1200 ticks)` → `Ripe`. Harvest posts
  only while the colony's total food in storage < 60.

## Farming

- **ECO-11** `DesignateFarm(area)` marks top-surface Grass/Dirt cells (the solid cell whose above is Air and
  standable) as farm tiles. The block becomes Farmland immediately (it is a designation, not construction).
- **ECO-12** Farm tile states: `Empty` → Plant job → `Growing(progress)` → `Mature` → Harvest job → 3 potatoes →
  `Empty`. Growth requires the tile to be **moist**: progress +1 per tick when moist, 0 when dry.
  Mature at 3 days (7200 moist ticks).
- **ECO-13** A growing crop that is dry for 1 continuous day (2400 ticks) withers → `Empty` (no yield).
- **ECO-14** Harvest of mature crops posts regardless of storage level (crops rot if left? no — they wait).

## Moisture

- **ECO-15** `MoistureMap` holds a `bool` per (x,z) column for the top-surface cell. Every 50 ticks recompute:
  a column is moist if any water cell with level ≥ 128 exists within horizontal Chebyshev radius 5 at
  `y ∈ [surfaceY − 2, surfaceY + 1]`. Implement as a 2D dilation over a per-column "wet at height" test,
  not per-tile radius scans.
- **ECO-16** Budget: moisture recompute ≤ 3 ms on seed 1 (Perf).

## Weather (drought)

- **ECO-17** `WeatherSystem` cycles `Wet(5 days) → Drought(2 days) → Wet …`, starting in Wet at tick 0.
  The first drought on seed 1 therefore starts at day 5. Source strength: Wet 100, Drought 0.
- **ECO-18** HUD shows current season and days until change.

## Acceptance scenarios

1. Needs decay and thresholds post jobs at the right tick.
2. An agent with no food in storage and hunger 0 dies after 1000 ticks with `Starved`.
3. Crop on a moist tile matures at 7200 ticks; on a dry tile never grows; dry-for-a-day withers.
4. Moisture: a tile 5 columns from water is moist; 6 columns is dry.
5. Full-colony survival: seed 1, scripted command log (see `testing.md` `SurvivalScript`), all 5 colonists alive
   at day 10.
