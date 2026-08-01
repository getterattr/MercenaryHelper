using System;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace MercenaryHelper;

public partial class Main : BaseSettingsPlugin<Settings>
{
    private const string MenagerieAreaName = "The Menagerie";

    public Main()
    {
        Name = "MercenaryHelper";
    }

    public override void OnLoad()
    {
        Core.Initialize(this);

        var now = DateTime.UtcNow;
        Runtime.Initialize(now, CreateSettingsBindingTargets());

        InitializeCurrentAreaTracking(now);

        Archetypes.Load();
        if (!Archetypes.IsLoaded)
        {
            LogDebug($"Archetype catalog failed to load: {Archetypes.LoadError}");
        }
        else
        {
            LogDebug($"Archetype catalog loaded ({Archetypes.Archetypes.Count} archetypes).");
        }

        QualityRules.Load();
        LogDebug(QualityRules.IsLoaded
            ? $"Quality ruleset loaded ({QualityRules.RuleCount} rules)."
            : $"Quality ruleset failed to load: {QualityRules.LoadError}");

        UniquePrices.LoadFromDisk();
        LogDebug($"Loaded {UniquePrices.PriceCount} cached unique prices (league '{UniquePrices.LastFetchLeague ?? "unknown"}').");
        UniquePrices.QueueFetch(Settings.Configuration.League.Value);
    }

    public override void OnClose()
    {
        base.OnClose();
        Runtime.Shutdown();
        _runtime = null;
        _worldLabelService = null;
        _panelInspectorService = null;
        _archetypeCatalog = null;
        _uniquePriceCatalog = null;
        _qualityRuleset = null;
        Core.Shutdown();
    }

    public override void AreaChange(AreaInstance area)
    {
        Core.AreaChanged(area);
        _ = Runtime.AreaTransitions.Evaluate(area);
    }

    public override void Render()
    {
        if (!Settings.Enable.Value) return;

        MaybeAutoRefreshPrices();
        WorldLabels.Render();
        PanelInspector.Render();
    }

    private const int AutoRefreshIntervalMinutes = 30;
    private DateTime _lastAutoRefreshCheckUtc = DateTime.MinValue;

    private void MaybeAutoRefreshPrices()
    {
        if (UniquePrices.IsFetching) return;

        var now = DateTime.UtcNow;
        if (now - _lastAutoRefreshCheckUtc < TimeSpan.FromSeconds(30)) return;
        _lastAutoRefreshCheckUtc = now;

        if (UniquePrices.LastFetchUtc == DateTime.MinValue ||
            now - UniquePrices.LastFetchUtc >= TimeSpan.FromMinutes(AutoRefreshIntervalMinutes))
        {
            UniquePrices.QueueFetch(Settings.Configuration.League.Value);
        }
    }

    private void OnRefreshPricesPressed()
    {
        UniquePrices.QueueFetch(Settings.Configuration.League.Value);
    }

    private void InitializeCurrentAreaTracking(DateTime now)
    {
        var currentArea = GameController?.Area?.CurrentArea;
        _isCurrentAreaTrackable = IsRunnableMapArea(currentArea);
        if (_isCurrentAreaTrackable)
        {
            _activeMapAreaHash = Helpers.TryGetAreaHashText(currentArea);
            _activeMapAreaName = Helpers.TryGetAreaNameText(currentArea);
            _activeMapInstanceId = Helpers.TryGetAreaInstanceId(currentArea);
        }
    }

    private void LogDebug(string message)
    {
        if (Settings?.DebugLogging?.Value == true)
        {
            try { DebugWindow.LogMsg($"[MercenaryHelper] {message}"); } catch { }
        }
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
