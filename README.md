# MercenaryHelper

An ExileApi overlay plugin for Path of Exile. Identifies valuable mercenaries and their rucksack loot as you clear maps.

- Detects primary mercenaries in the world and classifies each against a scraped archetype catalog (36 archetypes from poedb.tw).
- Draws an in-world label above each primary showing `[Archetype] (Attribute)` and its skill rotation.
- Highlights user-flagged archetypes with a chosen colour (attribute-grouped picker under Str, Dex, Int, and hybrid combos).
- Prefixes labels with `Infamous` automatically on maps that roll the Infamous mercenary mod.
- Reads the Mercenary Encounter panel: outlines the rucksack and boxes every unique inside it.
- Annotates each unique with its current poe.ninja chaos value; uniques at or above your chaos threshold are drawn in the flagged colour.

## Install

### Requirements

- Path of Exile should be running in Windowed or Windowed Fullscreen mode.
- [ExileApi](https://github.com/exApiTools/ExileApi-Compiled).
- .NET 10 SDK.

### Install With PluginUpdater

1. Open [ExileApi](https://github.com/exApiTools/ExileApi-Compiled).
2. Open the `PluginUpdater` plugin.
3. Click the Add tab.
4. Paste `https://github.com/getterattr/MercenaryHelper` into Repository URL.
5. Click Clone.
6. Either restart ExileApi, or open ExileApi Core settings, scroll down, and press Reload Plugins.

### Install From Source Folder

1. Download or clone this repository.
2. Place the `MercenaryHelper` folder inside your `Plugins/Source/` directory.
3. Launch ExileApi.
4. Let the host compile the plugin.
5. Enable MercenaryHelper in the plugin settings.

## Setup

1. Open the MercenaryHelper settings panel in the ExileApi menu and toggle **Enabled**.
2. Under **Configuration**, set your current league name (must match exactly, e.g. `Allflame`) and pick a **High Value Chaos Threshold** (default `100`).
3. Under **Pricing**, press **Refresh Prices** to pull the latest unique values from poe.ninja. Prices auto-refresh every 30 minutes thereafter.
4. Under **Archetypes**, expand the attribute group you care about (Str, Dex, Int, or a hybrid) and tick the mercenaries you want flagged. Selections persist across sessions.
5. Approach a mercenary encounter to see the label appear above the primary. Interact with the mercenary to open its Encounter panel; the rucksack is outlined and any unique inside is boxed - green (or your chosen highlight colour) for anything at or above the chaos threshold.
