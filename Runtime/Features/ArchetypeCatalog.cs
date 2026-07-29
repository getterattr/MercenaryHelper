using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExileCore;
using Newtonsoft.Json;

namespace MercenaryHelper.Runtime.Features;

internal sealed record ArchetypeDefinition(string Name, string Attribute, string Tribe, IReadOnlyList<string> Skills, IReadOnlyList<string> NormalizedSkills);

internal sealed record ArchetypeMatch(ArchetypeDefinition Archetype, int MatchedSkillCount, IReadOnlyList<string> MatchedSkills);

internal sealed class ArchetypeCatalog
{
    private const string CatalogFileName = "Archetypes.json";
    private const string CatalogDirectoryName = "Data";

    private readonly List<ArchetypeDefinition> _archetypes = new();
    private bool _loaded;
    private string _loadError;

    public IReadOnlyList<ArchetypeDefinition> Archetypes => _archetypes;
    public bool IsLoaded => _loaded;
    public string LoadError => _loadError;

    public void Load()
    {
        _archetypes.Clear();
        _loaded = false;
        _loadError = null;

        try
        {
            var path = ResolveCatalogPath();
            if (path == null || !File.Exists(path))
            {
                _loadError = $"Catalog file not found ({CatalogDirectoryName}/{CatalogFileName})";
                return;
            }

            var raw = File.ReadAllText(path);
            var doc = JsonConvert.DeserializeObject<CatalogFile>(raw);
            if (doc?.Archetypes == null)
            {
                _loadError = "Catalog file parsed but contained no archetypes.";
                return;
            }

            foreach (var entry in doc.Archetypes)
            {
                if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                var skills = (IReadOnlyList<string>)(entry.Skills ?? new List<string>());
                var normalized = skills.Select(Normalize).Where(s => s.Length > 0).ToArray();
                _archetypes.Add(new ArchetypeDefinition(entry.Name, entry.Attribute ?? "", entry.Tribe ?? "", skills, normalized));
            }

            _loaded = true;
        }
        catch (Exception ex)
        {
            _loadError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    public ArchetypeMatch Classify(IEnumerable<string> observedSkillNames, int minimumMatches = 1)
    {
        if (!_loaded || observedSkillNames == null) return null;

        var normalizedObserved = observedSkillNames
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(Normalize)
            .Where(s => s.Length > 0)
            .ToArray();
        if (normalizedObserved.Length == 0) return null;

        ArchetypeMatch best = null;
        foreach (var archetype in _archetypes)
        {
            if (archetype.NormalizedSkills.Count == 0) continue;

            var matched = new List<string>();
            foreach (var candidate in archetype.NormalizedSkills)
            {
                foreach (var observed in normalizedObserved)
                {
                    if (observed.Contains(candidate, StringComparison.Ordinal) ||
                        candidate.Contains(observed, StringComparison.Ordinal))
                    {
                        matched.Add(candidate);
                        break;
                    }
                }
            }

            if (matched.Count < minimumMatches) continue;
            if (best == null || matched.Count > best.MatchedSkillCount)
            {
                best = new ArchetypeMatch(archetype, matched.Count, matched);
            }
        }

        return best;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        Span<char> buffer = stackalloc char[value.Length];
        var i = 0;
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c)) buffer[i++] = char.ToLowerInvariant(c);
        }
        return new string(buffer[..i]);
    }

    private static string ResolveCatalogPath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(ArchetypeCatalog).Assembly.Location);
        if (string.IsNullOrWhiteSpace(assemblyDir)) return null;
        return Path.Combine(assemblyDir, CatalogDirectoryName, CatalogFileName);
    }

    private sealed class CatalogFile
    {
        [JsonProperty("archetypes")] public List<CatalogEntry> Archetypes { get; set; }
    }

    private sealed class CatalogEntry
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("attribute")] public string Attribute { get; set; }
        [JsonProperty("tribe")] public string Tribe { get; set; }
        [JsonProperty("skills")] public List<string> Skills { get; set; }
    }
}
