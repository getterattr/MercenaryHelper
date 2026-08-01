using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MercenaryHelper.Runtime.Features;

internal sealed class MutableQualityRule
{
    public string Archetype { get; set; } = string.Empty;
    public List<string> Required { get; set; } = new();
    public List<string> Forbidden { get; set; } = new();
}

internal enum QualityVerdict
{
    /// <summary>No rule exists for this archetype; treat as pass.</summary>
    NoRule,
    /// <summary>Matches all required-or-forbidden constraints. Worth hiring.</summary>
    Ideal,
    /// <summary>Fails at least one constraint. Do not hire.</summary>
    Bricked,
}

// Loads captainlance9-style quality gates keyed by archetype.
//
// Load order:
//   1. Sidecar file in ExileApi/config/global/ - user edits.
//   2. Data/QualityRules.json shipped with the plugin - authoritative defaults.
//   3. Hardcoded Kineticist / Manyshot rules if both files are missing.
//
// User edits made via the settings panel are written back to the sidecar
// only, so the shipped defaults stay clean.
internal sealed class QualityRuleset
{
    private const string SidecarFileName = "MercenaryHelper_quality.json";
    private const string DefaultsFileName = "QualityRules.json";
    private const string DefaultsDirectoryName = "Data";

    private readonly List<MutableQualityRule> _rules = new();
    private bool _loaded;
    private string _loadError;

    public bool IsLoaded => _loaded;
    public string LoadError => _loadError;
    public int RuleCount => _rules.Count;

    public IReadOnlyList<MutableQualityRule> Rules => _rules;

    public void Load()
    {
        _rules.Clear();
        _loaded = false;
        _loadError = null;

        try
        {
            var loaded = TryLoadFromPath(GetSidecarPath())
                         ?? TryLoadFromPath(GetShippedDefaultsPath())
                         ?? BuildHardcodedFallback();

            foreach (var rule in loaded)
            {
                if (string.IsNullOrWhiteSpace(rule.Archetype)) continue;
                _rules.Add(rule);
            }

            _loaded = true;
        }
        catch (Exception ex)
        {
            _loadError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    public void AddRule(string archetypeName)
    {
        _rules.Add(new MutableQualityRule
        {
            Archetype = archetypeName ?? string.Empty,
            Required = new List<string>(),
            Forbidden = new List<string>(),
        });
        SaveToSidecar();
    }

    public void RemoveRule(int index)
    {
        if (index < 0 || index >= _rules.Count) return;
        _rules.RemoveAt(index);
        SaveToSidecar();
    }

    public void PersistEdits() => SaveToSidecar();

    public QualityVerdict Evaluate(string archetypeName, IEnumerable<string> observedSkillNames)
    {
        if (!_loaded) return QualityVerdict.NoRule;
        if (string.IsNullOrWhiteSpace(archetypeName)) return QualityVerdict.NoRule;

        var rule = _rules.FirstOrDefault(r => string.Equals(r.Archetype, archetypeName, StringComparison.OrdinalIgnoreCase));
        if (rule == null) return QualityVerdict.NoRule;

        var normalized = observedSkillNames?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(Normalize)
            .Where(s => s.Length > 0)
            .ToArray() ?? Array.Empty<string>();

        if (normalized.Length == 0) return QualityVerdict.Bricked;

        var normalizedRequired = rule.Required.Select(Normalize).Where(s => s.Length > 0).ToArray();
        var normalizedForbidden = rule.Forbidden.Select(Normalize).Where(s => s.Length > 0).ToArray();

        // ALL required skills must be present (AND). Nero's Ice Shot guide keys
        // on "Ice Shot AND Returning Projectiles" - any-one-of would let a merc
        // with Ice Shot but no Return slip through as Ideal.
        var hasAllRequired = normalizedRequired.All(req =>
            normalized.Any(obs => obs.Contains(req, StringComparison.Ordinal) || req.Contains(obs, StringComparison.Ordinal)));
        if (!hasAllRequired) return QualityVerdict.Bricked;

        var hasForbidden = normalizedForbidden.Any(forb =>
            normalized.Any(obs => obs.Contains(forb, StringComparison.Ordinal) || forb.Contains(obs, StringComparison.Ordinal)));
        if (hasForbidden) return QualityVerdict.Bricked;

        return QualityVerdict.Ideal;
    }

    private List<MutableQualityRule> TryLoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            var doc = JsonConvert.DeserializeObject<RulesFile>(File.ReadAllText(path));
            if (doc?.Rules == null) return null;

            var loaded = new List<MutableQualityRule>();
            foreach (var entry in doc.Rules)
            {
                if (string.IsNullOrWhiteSpace(entry.Archetype)) continue;
                loaded.Add(new MutableQualityRule
                {
                    Archetype = entry.Archetype,
                    Required = new List<string>(entry.Required ?? new List<string>()),
                    Forbidden = new List<string>(entry.Forbidden ?? new List<string>()),
                });
            }
            return loaded;
        }
        catch (Exception ex)
        {
            _loadError = $"{path}: {ex.GetType().Name}: {ex.Message}";
            return null;
        }
    }

    private static List<MutableQualityRule> BuildHardcodedFallback() => new()
    {
        new MutableQualityRule
        {
            Archetype = "Kineticist",
            Required = new List<string> { "Kinetic Blast of Clustering" },
            Forbidden = new List<string> { "Kinetic Bolt" },
        },
        new MutableQualityRule
        {
            Archetype = "Manyshot",
            Required = new List<string> { "Vaal Ice Shot" },
            Forbidden = new List<string> { "Icicle Rain" },
        },
    };

    private void SaveToSidecar()
    {
        try
        {
            var path = GetSidecarPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var doc = new RulesFile
            {
                Rules = _rules.Select(r => new RuleEntry
                {
                    Archetype = r.Archetype,
                    Required = new List<string>(r.Required),
                    Forbidden = new List<string>(r.Forbidden),
                }).ToList(),
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(doc, Formatting.Indented));
        }
        catch (Exception ex)
        {
            _loadError = $"save: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        Span<char> buffer = stackalloc char[value.Length];
        var i = 0;
        foreach (var c in value)
            if (char.IsLetterOrDigit(c)) buffer[i++] = char.ToLowerInvariant(c);
        return new string(buffer[..i]);
    }

    private static string GetSidecarPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "config", "global", SidecarFileName);

    private static string GetShippedDefaultsPath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(QualityRuleset).Assembly.Location);
        return string.IsNullOrWhiteSpace(assemblyDir)
            ? null
            : Path.Combine(assemblyDir, DefaultsDirectoryName, DefaultsFileName);
    }

    private sealed class RulesFile
    {
        [JsonProperty("rules")] public List<RuleEntry> Rules { get; set; }
    }

    private sealed class RuleEntry
    {
        [JsonProperty("archetype")] public string Archetype { get; set; }
        [JsonProperty("required")] public List<string> Required { get; set; }
        [JsonProperty("forbidden")] public List<string> Forbidden { get; set; }
    }
}
