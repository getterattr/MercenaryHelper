using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using Newtonsoft.Json;

namespace MercenaryHelper;

public partial class Main : BaseSettingsPlugin<Settings>
{
    private const string MenagerieAreaName = "The Menagerie";
    private const string PersistedSettingsFileName = "MercenaryHelper_settings.json";

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

        LoadPersistedPickerState();
    }

    public override void OnClose()
    {
        base.OnClose();
        SavePersistedPickerState();
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

    private void LoadPersistedPickerState()
    {
        try
        {
            var path = GetPersistedSettingsPath();
            if (!File.Exists(path))
            {
                LogDebug($"No persisted settings at {path}; keeping default archetype picks.");
                SavePersistedPickerState();
                return;
            }

            var doc = JsonConvert.DeserializeObject<PersistedPickerState>(File.ReadAllText(path));
            if (doc?.EnabledArchetypes == null) return;

            Settings.EnabledArchetypes = new HashSet<string>(
                doc.EnabledArchetypes.Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
            LogDebug($"Loaded {Settings.EnabledArchetypes.Count} persisted archetype selections.");
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to load persisted picker state: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal void SavePersistedPickerState()
    {
        try
        {
            var path = GetPersistedSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var doc = new PersistedPickerState
            {
                EnabledArchetypes = Settings.EnabledArchetypes
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(doc, Formatting.Indented));
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to save persisted picker state: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string GetPersistedSettingsPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "config", "global", PersistedSettingsFileName);

    private sealed class PersistedPickerState
    {
        public List<string> EnabledArchetypes { get; set; }
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
