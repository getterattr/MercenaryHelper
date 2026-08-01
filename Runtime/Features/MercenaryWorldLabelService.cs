using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ImGuiNET;
using SharpDX;
using Color = SharpDX.Color;
using Vector2 = System.Numerics.Vector2;

namespace MercenaryHelper.Runtime.Features;

internal sealed class MercenaryWorldLabelService
{
    private const string MercenaryMetadataPrefix = "Metadata/Monsters/Mercenaries/";
    private const string HiredMercenaryMetadataMarker = "Allied";
    private const int FriendlyMercenaryReaction = 70;
    private const string MoveSkillName = "Move";
    private const string PortalOutSkillName = "EASMercenaryPortalOut";
    private const string MercenarySkillSuffix = "Mercenary";
    private const int MinimumSignatureMatches = 2;
    private const string InfamousPrefix = "Infamous ";
    private static readonly GameStat? InfamousMapStat = TryParseStat("MapMercenariesAreInfamous");

    private static readonly Color DefaultSkillColor = new(220, 220, 220, 255);
    private static readonly Color LabelBackgroundColor = new(0, 0, 0, 200);
    // Kill-order palette: supports die first (green), primary last (yellow).
    private static readonly Color KillFirstColor = new(120, 255, 120, 255);
    private static readonly Color KillLastColor = new(255, 220, 90, 255);

    private readonly Main _plugin;

    public MercenaryWorldLabelService(Main plugin)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    public void Render()
    {
        var settings = _plugin.Settings;

        var gameController = _plugin.GameController;
        if (gameController == null) return;

        var ui = gameController.IngameState?.IngameUi;
        if (ui != null)
        {
            if (ui.LargePanels.Any(p => p.IsVisible)) return;
            if (ui.FullscreenPanels.Any(p => p.IsVisible)) return;
        }

        if (!gameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var monsters))
            return;

        var camera = gameController.IngameState.Camera;
        var graphics = _plugin.Graphics;
        var lineHeight = ImGui.GetTextLineHeight();
        var highlightColor = settings.Configuration.HighlightColor.Value;
        var enabledArchetypes = settings.EnabledArchetypes;
        var catalog = _plugin.Archetypes;
        var qualityRules = _plugin.QualityRules;
        var isInfamousMap = IsInfamousMap(gameController);

        foreach (var mercenary in monsters)
        {
            if (mercenary?.IsValid != true) continue;
            if (mercenary.IsHostile) continue;
            if (mercenary.Metadata?.StartsWith(MercenaryMetadataPrefix, StringComparison.Ordinal) != true) continue;
            // Once hired the mercenary follows the player; the encounter overlay
            // becomes noise. GGG marks hired mercs by inserting "Allied" into the
            // metadata path.
            if (mercenary.Metadata.Contains(HiredMercenaryMetadataMarker, StringComparison.Ordinal)) continue;
            if (!mercenary.TryGetComponent<Positioned>(out var positioned) || positioned.Reaction != FriendlyMercenaryReaction) continue;
            if (!mercenary.TryGetComponent<MinimapIcon>(out var minimapIcon)) continue;
            // MinimapIcon.IsHide == true marks accompanying supports; false marks the primary.
            var isPrimary = !minimapIcon.IsHide;
            if (!mercenary.TryGetComponent<Actor>(out var actor) || actor.ActorSkills == null) continue;

            var displayableSkills = CollectDisplayableSkills(actor);
            var screenPos = camera.WorldToScreen(mercenary.PosNum);
            var line = 0;

            // Supports die first; give them a compact green tag and skip
            // archetype/quality work - it's not useful for a merc you can't hire.
            if (!isPrimary)
            {
                graphics.DrawTextWithBackground(
                    "[1] Kill first",
                    screenPos,
                    KillFirstColor,
                    LabelBackgroundColor);
                continue;
            }

            var match = catalog.IsLoaded
                ? catalog.Classify(displayableSkills.SelectMany(s => new[] { s.DisplayName, s.FullName }), MinimumSignatureMatches)
                : null;
            var archetypeName = match?.Archetype.Name;
            var qualifiedArchetypeName = archetypeName != null && isInfamousMap
                ? InfamousPrefix + archetypeName
                : archetypeName;

            var qualityFilterEnabled = settings.QualityFilter.Enable.Value;
            var quality = archetypeName != null && qualityFilterEnabled
                ? qualityRules.Evaluate(archetypeName,
                    displayableSkills.SelectMany(s => new[] { s.DisplayName, s.FullName }))
                : QualityVerdict.NoRule;

            var isArchetypeWatched = archetypeName != null &&
                                     enabledArchetypes.Contains(archetypeName) &&
                                     quality != QualityVerdict.Bricked;

            // Flagged + ideal primary keeps the user's highlight colour so it
            // pops against the kill-order palette. Everything else uses the
            // yellow "kill last" tone.
            var primaryColor = isArchetypeWatched ? highlightColor : KillLastColor;

            graphics.DrawTextWithBackground(
                "[2] Kill last",
                screenPos + new Vector2(0, lineHeight * line),
                KillLastColor,
                LabelBackgroundColor);
            line++;

            if (!string.IsNullOrEmpty(qualifiedArchetypeName))
            {
                var label = match.Archetype.Attribute is { Length: > 0 } attr
                    ? $"[{qualifiedArchetypeName}] ({attr})"
                    : $"[{qualifiedArchetypeName}]";
                graphics.DrawTextWithBackground(
                    label,
                    screenPos + new Vector2(0, lineHeight * line),
                    primaryColor,
                    LabelBackgroundColor);
                line++;
            }

            foreach (var (displayName, _) in displayableSkills)
            {
                graphics.DrawTextWithBackground(
                    displayName,
                    screenPos + new Vector2(0, lineHeight * line),
                    primaryColor,
                    LabelBackgroundColor);
                line++;
            }
        }
    }

    private static bool IsInfamousMap(ExileCore.GameController gameController)
    {
        if (InfamousMapStat is not { } stat) return false;
        var stats = gameController?.IngameState?.Data?.MapStats;
        if (stats == null) return false;
        return stats.TryGetValue(stat, out var value) && value != 0;
    }

    private static GameStat? TryParseStat(string name) =>
        Enum.TryParse<GameStat>(name, out var stat) ? stat : null;

    private static List<(string DisplayName, string FullName)> CollectDisplayableSkills(ExileCore.PoEMemory.Components.Actor actor)
    {
        var list = new List<(string, string)>();
        foreach (var skill in actor.ActorSkills)
        {
            var fullSkillName = skill?.Name;
            if (string.IsNullOrWhiteSpace(fullSkillName)) continue;
            if (fullSkillName == MoveSkillName || fullSkillName == PortalOutSkillName) continue;

            var displayName = ResolveSkillDisplayName(skill, fullSkillName);
            list.Add((displayName, fullSkillName));
        }
        return list;
    }

    // Resolve a mercenary's internal skill name (e.g. "ViperStrikeAltX") to the
    // in-game display name (e.g. "Viper Strike of the Mamba") using the
    // SkillGems file that ExileApi already parses. Falls back to the raw name
    // with the "Mercenary" suffix stripped if the wrapper isn't populated.
    private static string ResolveSkillDisplayName(ExileCore.PoEMemory.MemoryObjects.ActorSkill skill, string fullSkillName)
    {
        string resolved = null;
        try
        {
            var wrapper = skill?.EffectsPerLevel?.SkillGemWrapper;
            resolved = wrapper?.ActiveSkill?.DisplayName;
            if (string.IsNullOrWhiteSpace(resolved))
                resolved = wrapper?.Name;
        }
        catch { }

        var name = !string.IsNullOrWhiteSpace(resolved) ? resolved : fullSkillName;
        return name.EndsWith(MercenarySkillSuffix, StringComparison.Ordinal)
            ? name[..^MercenarySkillSuffix.Length]
            : name;
    }

}
