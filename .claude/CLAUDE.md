# MercenaryHelper

Scaffold for a Path of Exile overlay plugin focused on mercenary-related gameplay.

## Purpose

Starting point for a mercenary companion overlay. The scope (POE 1 mercenary encounters vs POE 2 mercenary companion) will be pinned down when the first concrete feature request lands. Deliberately empty for now: entrypoint, settings shell, and area-tracking runtime are wired up, and nothing renders. Everything mercenary-specific is deferred until requirements arrive - do not speculate features.

## Activation

Currently always-idle. The master `Enable` toggle gates `Render()`, but `Render()` has no body. The plugin loads, area transitions are evaluated by the runtime (state is stored but unused), and the settings panel is interactive.

## Settings

Settings live in `Core/Settings.cs` and persist to `ExileApi\config\global\MercenaryHelper_settings.json`.

| Key | Type | Meaning |
|---|---|---|
| `Enable` | `ToggleNode` | Master enable/disable. Gates the top of `Render()`. |
| `DebugLogging` | `ToggleNode` | When on, `LogDebug` writes `[MercenaryHelper] <msg>` to the ExileApi debug window. |
| `ReloadAction` | `ButtonNode` | Placeholder button - currently only emits a debug log. Wire up when a real reload/refresh action exists. |
| `OverviewPanel` | `CustomNode` | Draws the Overview summary table via `DrawSettingsOverviewPanel`. |
| `ConfigurationHeader` | `CustomNode` | Draws the "Configuration" section header via `DrawConfigurationHeader`. |
| `ActionsRow` | `CustomNode` | Draws the actions row (currently one button) via `DrawActionsRow`. |

## Game state read

Nothing yet. Area tracking uses `GameController.Area.CurrentArea` and stashes hash/instance/name via `Helpers.TryGetArea*` reflection wrappers, but no component reads are performed.

## Lifecycle hooks used

- `OnLoad()` - initialises `Core` singleton, constructs `MercenaryHelperRuntime`, binds settings delegates, seeds area-tracking state.
- `OnClose()` - reverse of OnLoad; clears runtime and Core singleton.
- `AreaChange(area)` - forwards to `Core.AreaChanged` and `Runtime.AreaTransitions.Evaluate` (decision result is currently unused - kept to demonstrate the pattern for future features).
- `Render()` - early-returns on `!Settings.Enable.Value`, then does nothing.

## Gotchas

- **No autohide.** The Beasts plugin (which this mirrors) hides overlays in hideout/town or when panels are open. That was intentionally omitted here. If it becomes needed, add an `AutoHideOverlays` `ToggleNode` to Settings plus a gate at the top of `Render()`.
- **Runtime pattern deliberately preserved.** `MercenaryHelperRuntime` + `MainSettingsBindings` + `MercenaryHelperRuntimeState` are scaffold, not overkill - concrete features should hang off `Runtime` (new services + state slots) rather than sprawl across `Main`.
- **Area evaluation is a side effect.** `Runtime.AreaTransitions.Evaluate(area)` mutates `Runtime.State.Map.*` even though the returned decision is discarded. Reading state after `AreaChange` is safe; do not rely on the decision object outside `AreaChange`.

## Out of scope

- Any mercenary detection, rendering, capture, pricing, or clipboard logic - to be added feature-by-feature.
- Custom settings-file persistence (Beasts serialises HashSet state manually) - not needed until we have HashSet-style state.
- `Features/` and `Runtime/Features/` service folders - add them when the first real service arrives.

## Build and run

See `/exileapi` for the standard build invocation and runtime setup. The plugin is auto-compiled by `Loader.exe` on startup because `Plugins/Source` is a junction to `Repositories/`.
