using System;
using ExileCore;
using MercenaryHelper.Runtime.State;

namespace MercenaryHelper.Runtime.Lifecycle;

internal enum AreaTransitionKind
{
    EnteredNonTrackableArea,
    ReenteredActiveMap,
    EnteredNewTrackableMap,
}

internal sealed record AreaTransitionDecision(
    AreaTransitionKind Kind,
    string PreviousAreaHash,
    string PreviousAreaName,
    int PreviousAreaInstanceId,
    string NewAreaHash,
    string NewAreaName,
    int NewAreaInstanceId);

internal sealed class AreaTransitionCoordinator
{
    private const string MenagerieAreaName = "The Menagerie";
    private readonly MercenaryHelperRuntimeState _state;

    public AreaTransitionCoordinator(MercenaryHelperRuntimeState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public AreaTransitionDecision Evaluate(AreaInstance area)
    {
        var map = _state.Map;

        var previousAreaHash = map.ActiveMapAreaHash;
        var previousAreaName = map.ActiveMapAreaName;
        var previousAreaInstanceId = map.ActiveMapInstanceId;
        var newAreaHash = Helpers.TryGetAreaHashText(area) ?? string.Empty;
        var newAreaName = Helpers.TryGetAreaNameText(area) ?? string.Empty;
        var newAreaInstanceId = Helpers.TryGetAreaInstanceId(area);
        var newAreaTrackable = IsRunnableMapArea(area);

        if (!newAreaTrackable)
        {
            map.IsCurrentAreaTrackable = false;
            return new AreaTransitionDecision(
                AreaTransitionKind.EnteredNonTrackableArea,
                previousAreaHash, previousAreaName, previousAreaInstanceId,
                newAreaHash, newAreaName, newAreaInstanceId);
        }

        var hashMatches = !string.IsNullOrWhiteSpace(previousAreaHash) &&
                          !string.IsNullOrWhiteSpace(newAreaHash) &&
                          string.Equals(newAreaHash, previousAreaHash, StringComparison.Ordinal);
        var instanceMatches = previousAreaInstanceId >= 0 && newAreaInstanceId >= 0 &&
                              newAreaInstanceId == previousAreaInstanceId;

        map.ActiveMapAreaHash = newAreaHash;
        map.ActiveMapAreaName = newAreaName;
        map.ActiveMapInstanceId = newAreaInstanceId;
        map.IsCurrentAreaTrackable = true;

        return new AreaTransitionDecision(
            hashMatches || instanceMatches
                ? AreaTransitionKind.ReenteredActiveMap
                : AreaTransitionKind.EnteredNewTrackableMap,
            previousAreaHash, previousAreaName, previousAreaInstanceId,
            newAreaHash, newAreaName, newAreaInstanceId);
    }

    private static bool IsHideoutLikeArea(AreaInstance area)
    {
        return area?.IsHideout == true ||
               area?.Name.EqualsIgnoreCase(MenagerieAreaName) == true;
    }

    private static bool IsRunnableMapArea(AreaInstance area)
    {
        return area is { IsTown: false } && !IsHideoutLikeArea(area);
    }
}
