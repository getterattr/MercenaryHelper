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
    private const int FriendlyMercenaryReaction = 70;
    private const string MoveSkillName = "Move";
    private const string PortalOutSkillName = "EASMercenaryPortalOut";
    private const string MercenarySkillSuffix = "Mercenary";
    private const int MinimumSignatureMatches = 2;
    private const string InfamousPrefix = "Infamous ";
    private static readonly GameStat? InfamousMapStat = TryParseStat("MapMercenariesAreInfamous");

    private static readonly Color DefaultSkillColor = new(220, 220, 220, 255);
    private static readonly Color LabelBackgroundColor = new(0, 0, 0, 200);

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
        var isInfamousMap = IsInfamousMap(gameController);

        foreach (var mercenary in monsters)
        {
            if (mercenary?.IsValid != true) continue;
            if (mercenary.IsHostile) continue;
            if (mercenary.Metadata?.StartsWith(MercenaryMetadataPrefix, StringComparison.Ordinal) != true) continue;
            if (!mercenary.TryGetComponent<Positioned>(out var positioned) || positioned.Reaction != FriendlyMercenaryReaction) continue;
            if (!mercenary.TryGetComponent<MinimapIcon>(out var minimapIcon) || minimapIcon.IsHide) continue;
            if (!mercenary.TryGetComponent<Actor>(out var actor) || actor.ActorSkills == null) continue;

            var displayableSkills = CollectDisplayableSkills(actor);
            var match = catalog.IsLoaded
                ? catalog.Classify(displayableSkills.SelectMany(s => new[] { s.DisplayName, s.FullName }), MinimumSignatureMatches)
                : null;
            var archetypeName = match?.Archetype.Name;
            var qualifiedArchetypeName = archetypeName != null && isInfamousMap
                ? InfamousPrefix + archetypeName
                : archetypeName;
            var isArchetypeWatched = archetypeName != null && enabledArchetypes.Contains(archetypeName);

            var screenPos = camera.WorldToScreen(mercenary.PosNum);
            var line = 0;

            if (!string.IsNullOrEmpty(qualifiedArchetypeName))
            {
                var label = match.Archetype.Attribute is { Length: > 0 } attr
                    ? $"[{qualifiedArchetypeName}] ({attr})"
                    : $"[{qualifiedArchetypeName}]";
                graphics.DrawTextWithBackground(
                    label,
                    screenPos + new Vector2(0, lineHeight * line),
                    isArchetypeWatched ? highlightColor : DefaultSkillColor,
                    LabelBackgroundColor);
                line++;
            }

            foreach (var (displayName, _) in displayableSkills)
            {
                graphics.DrawTextWithBackground(
                    displayName,
                    screenPos + new Vector2(0, lineHeight * line),
                    isArchetypeWatched ? highlightColor : DefaultSkillColor,
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

    private static List<(string DisplayName, string FullName)> CollectDisplayableSkills(Actor actor)
    {
        var list = new List<(string, string)>();
        foreach (var skill in actor.ActorSkills)
        {
            var fullSkillName = skill.Name;
            if (string.IsNullOrWhiteSpace(fullSkillName)) continue;
            if (fullSkillName == MoveSkillName || fullSkillName == PortalOutSkillName) continue;

            var displayName = fullSkillName.EndsWith(MercenarySkillSuffix, StringComparison.Ordinal)
                ? fullSkillName[..^MercenarySkillSuffix.Length]
                : fullSkillName;

            list.Add((displayName, fullSkillName));
        }
        return list;
    }

}
