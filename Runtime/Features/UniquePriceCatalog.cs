using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ExileCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MercenaryHelper.Runtime.Features;

internal sealed record PricedUnique(string Name, double ChaosValue);

internal sealed class UniquePriceCatalog
{
    private const string SettingsFileName = "MercenaryHelper_uniques.json";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private static readonly string[] EndpointTypes = new[]
    {
        "UniqueWeapon",
        "UniqueArmour",
        "UniqueAccessory",
    };

    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    static UniquePriceCatalog()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    private readonly Dictionary<string, double> _prices = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public bool IsFetching { get; private set; }
    public string LastFetchLeague { get; private set; }
    public DateTime LastFetchUtc { get; private set; } = DateTime.MinValue;
    public string LastFetchError { get; private set; }
    public int PriceCount { get { lock (_lock) return _prices.Count; } }

    public bool TryGetPrice(string uniqueName, out double chaos)
    {
        chaos = 0;
        if (string.IsNullOrWhiteSpace(uniqueName)) return false;
        lock (_lock)
        {
            return _prices.TryGetValue(uniqueName.Trim(), out chaos);
        }
    }

    public IReadOnlyList<PricedUnique> HighValueUniques(double thresholdChaos)
    {
        lock (_lock)
        {
            return _prices
                .Where(kvp => kvp.Value >= thresholdChaos)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new PricedUnique(kvp.Key, kvp.Value))
                .ToArray();
        }
    }

    public void LoadFromDisk()
    {
        try
        {
            var path = GetPersistedPath();
            if (!File.Exists(path)) return;

            var doc = JsonConvert.DeserializeObject<PersistedCatalog>(File.ReadAllText(path));
            if (doc?.Prices == null) return;

            lock (_lock)
            {
                _prices.Clear();
                foreach (var kvp in doc.Prices)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
                    _prices[kvp.Key.Trim()] = kvp.Value;
                }
                LastFetchLeague = doc.League;
                LastFetchUtc = doc.LastFetchUtc;
            }
        }
        catch (Exception ex)
        {
            LastFetchError = $"load: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public void QueueFetch(string league)
    {
        if (IsFetching) return;
        if (string.IsNullOrWhiteSpace(league))
        {
            LastFetchError = "League name is empty.";
            return;
        }
        _ = Task.Run(() => FetchAsync(league));
    }

    private async Task FetchAsync(string league)
    {
        IsFetching = true;
        LastFetchError = null;
        try
        {
            var aggregated = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in EndpointTypes)
            {
                var url = $"https://poe.ninja/poe1/api/economy/stash/current/item/overview?league={Uri.EscapeDataString(league)}&type={type}";
                string body;
                try
                {
                    body = await HttpClient.GetStringAsync(url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LastFetchError = $"{type}: {ex.GetType().Name}: {ex.Message}";
                    continue;
                }

                var doc = JObject.Parse(body);
                if (doc["lines"] is not JArray lines) continue;

                foreach (var line in lines)
                {
                    var name = (string)line["name"];
                    var chaos = (double?)line["chaosValue"] ?? 0;
                    if (string.IsNullOrWhiteSpace(name) || chaos <= 0) continue;

                    // Skip linked (5/6L) and corrupted variants - mercenaries drop base rolls only.
                    var links = (int?)line["links"];
                    if (links.HasValue && links.Value > 0) continue;
                    if ((bool?)line["corrupted"] == true) continue;

                    var key = name.Trim();
                    if (!aggregated.TryGetValue(key, out var existing) || chaos < existing)
                    {
                        aggregated[key] = chaos;
                    }
                }
            }

            if (aggregated.Count == 0)
            {
                if (string.IsNullOrEmpty(LastFetchError))
                    LastFetchError = "poe.ninja returned no unique prices for that league.";
                return;
            }

            lock (_lock)
            {
                _prices.Clear();
                foreach (var kvp in aggregated) _prices[kvp.Key] = kvp.Value;
                LastFetchLeague = league;
                LastFetchUtc = DateTime.UtcNow;
            }

            SaveToDisk();
        }
        catch (Exception ex)
        {
            LastFetchError = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsFetching = false;
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var path = GetPersistedPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            PersistedCatalog doc;
            lock (_lock)
            {
                doc = new PersistedCatalog
                {
                    League = LastFetchLeague,
                    LastFetchUtc = LastFetchUtc,
                    Prices = new Dictionary<string, double>(_prices, StringComparer.OrdinalIgnoreCase),
                };
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(doc, Formatting.Indented));
        }
        catch (Exception ex)
        {
            LastFetchError = $"save: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string GetPersistedPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), "config", "global", SettingsFileName);

    private sealed class PersistedCatalog
    {
        public string League { get; set; }
        public DateTime LastFetchUtc { get; set; }
        public Dictionary<string, double> Prices { get; set; }
    }
}
