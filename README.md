# 🌱 ReadySetGrow

### 🎮 Created by Jack, age 11

A 3D, first-person *grow-a-garden* game built in **C# / Godot 4.7 (.NET)**.

Walk around a fenced farm, open the gate, plant seeds on open dirt fields, watch
them grow into crops that look like the real thing, harvest them into your basket,
and sell at the market. Reinvest into rarer seeds, ride out weather events, and
unlock the **Magical Tree**, the **Hidden Grove**, and a **Pets** shop.

## How to play

- **WASD** move · **mouse** look · **Space** jump
- **E** / **left-click** — use whatever the crosshair is on (plant, harvest, open the gate, sell at the market)
- **Mouse-wheel** — switch the selected seed while walking
- Hold **Left-Shift** — free the cursor to browse/scroll the seed shop (right) and pets shop (left)
- **H** — harvest all · **G** — "Grow All" math-quiz · **T** — toggle Auto
- **Esc** — Main Menu (Resume · Auto · Restart · Quit) — pauses the game
- **F11** — borderless fullscreen

Plant on the two dirt fields, harvest ripe crops into your **🧺 basket**, then walk
to the **Market** and sell for coins. Progress autosaves and crops keep growing
while the game is closed (up to 8 hours offline).

## Progression

| Unlock | Cost | What you get |
|--------|-----:|--------------|
| 🌳 **Magical Tree** | 100 B | 30 god-tier seeds: Divine · Ultra · Titan · Entity · Eternal · Admin |
| 🐾 **Pets Shop** | 100 Qa | 14 pets (Legendary→Eternal), each a permanent +yield or +growth boost |
| 🌌 **Hidden Grove** | 1 Qi | Hidden · Alpha · Strange · Celestial · Infinite, plus the 🎁 **Centurnial pack** |

The seed shop shows **everything** at once; locked tiers are greyed with their
unlock price. Money scales all the way into the **Qi/Sx** range.

**Crops look like their namesakes** — carrots are orange cones with leafy tops,
tomatoes/berries grow on bushes, melons & pumpkins are gourds, apples hang on
little trees, grapes in clusters, corn on a stalk, plus glowing crystals, stars,
flowers, a haloed Genesis orb, spiral **Bendboo** bamboo, a 4-tile **Toxikit**
mushroom, and **Snapdragon** spikes.

**Mutations** multiply value: Wet, Gold, Frozen, Rainbow, the storm-only **Shocked
(48×)**, **Giant (12×)**, and **Rainbow Giant (300×)**. Plants also roll a random
**size** that scales the model and the payout. The **Centurnial** pack crops are
always Rainbow, Giant, or both.

**Weather events** fire about every 2 minutes: **⛈ Storm** (dark sky, rolling
clouds, rain, lightning — crops can turn Shocked) and **🌈 Rainbow** (clouds and
rainbow arcs — crops often turn Rainbow).

Everything is built from primitive 3D meshes and sounds are synthesised at
runtime — **no external assets**, so it runs anywhere the Godot .NET build does.

## Run it

**Standalone (no Godot needed):** download **`ReadySetGrow.exe`** from the
[Releases](../../releases) page and double-click it. It's one self-contained file.

**From source:** double-click **`Play ReadySetGrow.bat`**, or open `project.godot`
in Godot 4.7 (.NET) and press **F5**.

## Build the standalone .exe

```sh
"<path>\Godot_v4.7-stable_mono_win64_console.exe" --headless --path . \
  --export-release "Windows Desktop" "build/ReadySetGrow.exe"
```
Requires the Godot 4.7 **mono** export templates.

## Project layout

```
project.godot          Godot config (main scene = ReadySetGrow.tscn)
ReadySetGrow.tscn      One-node 3D scene; everything else is built in C#
GrowDaGarden.csproj    .NET project (net8.0, Godot.NET.Sdk 4.7.0)
scripts/
  Game3D.cs            Game root: world, FP player, shop/pets/menu UI, economy, events, save/load
  Plot3D.cs            One planting spot — grows the crop model, multi-tile support
  Plant.cs             Builds a stylised 3D model per crop shape
  Catalog.cs           Seed catalogs (base, Magical Tree, Hidden Grove, Centurnial) + Pets
  Sfx.cs               Procedural sound engine (no audio files)
  PlotState.cs, SeedType.cs, Mutation.cs, Num.cs, MathQuiz.cs
```

The original 2D version is still here (`Main.tscn`) if you set it as the main scene.

## Credits

**Created by Jack — age 11.** 🌱🎮

Designed and directed by Jack: the idea, the seeds and rarities, the Magical Tree,
the Hidden Grove and Centurnial pack, the weather events, and how it all plays.
