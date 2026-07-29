using System;
using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Newtonsoft.Json;
using SharpDX;
using Color = SharpDX.Color;

namespace MercenaryHelper;

public class Settings : ISettings
{
    [Menu("Enabled", "Enable or disable the MercenaryHelper plugin.")]
    public ToggleNode Enable { get; set; } = new(false);

    [Menu("Debug Logs", "Write detailed logs to aid troubleshooting.")]
    public ToggleNode DebugLogging { get; set; } = new(false);

    [Menu("Overview", "Live plugin status: current area, flagged archetype count, catalog and pricing state.")]
    [JsonIgnore]
    public CustomNode OverviewPanel { get; set; } = new();

    [Menu("Configuration")]
    [JsonIgnore]
    public CustomNode ConfigurationHeader { get; set; } = new();

    [Menu("Configuration Options", "League name, price threshold, and highlight colour.")]
    public ConfigurationSettings Configuration { get; set; } = new();

    [Menu("Pricing")]
    [JsonIgnore]
    public CustomNode PricingHeader { get; set; } = new();

    [Menu("Pricing Options", "poe.ninja unique price catalog: refresh and status.")]
    public PricingSettings Pricing { get; set; } = new();

    [Menu("Archetypes")]
    [JsonIgnore]
    public CustomNode ArchetypesHeader { get; set; } = new();

    [Menu("Archetype Groups", "Pick which mercenary archetypes to flag, grouped by attribute.")]
    public ArchetypesSettings Archetypes { get; set; } = new();

    [JsonIgnore]
    internal HashSet<string> EnabledArchetypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonProperty("EnabledArchetypes")]
    public List<string> SavedEnabledArchetypes
    {
        get => new(EnabledArchetypes);
        set => EnabledArchetypes = value != null
            ? new HashSet<string>(value, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}

[Submenu(CollapsedByDefault = true)]
public class ConfigurationSettings
{
    [Menu("League", "League name for poe.ninja lookups. Must match your current league exactly, e.g. 'Allflame', 'Mercenaries', 'Standard'.")]
    public TextNode League { get; set; } = new("Allflame");

    [Menu("High Value Chaos Threshold", "Uniques priced at or above this chaos value are highlighted in the mercenary rucksack.")]
    public RangeNode<int> ChaosThreshold { get; set; } = new(100, 1, 10000);

    [Menu("Highlight Color", "Colour used for flagged archetypes and high-value uniques.")]
    public ColorNode HighlightColor { get; set; } = new(new Color(120, 255, 120, 255));
}

[Submenu(CollapsedByDefault = true)]
public class PricingSettings
{
    [Menu("Refresh Prices", "Fetch the latest unique prices from poe.ninja for the configured league.")]
    public ButtonNode RefreshPricesAction { get; set; } = new();

    [Menu("Status", "Current price catalog status.")]
    [JsonIgnore]
    public CustomNode StatusPanel { get; set; } = new();
}

[Submenu(CollapsedByDefault = true)]
public class ArchetypesSettings
{
    [Menu("Strength")]
    public ArchetypeGroupSettings Str { get; set; } = new();

    [Menu("Dexterity")]
    public ArchetypeGroupSettings Dex { get; set; } = new();

    [Menu("Intelligence")]
    public ArchetypeGroupSettings Int { get; set; } = new();

    [Menu("Strength / Dexterity")]
    public ArchetypeGroupSettings StrDex { get; set; } = new();

    [Menu("Strength / Intelligence")]
    public ArchetypeGroupSettings StrInt { get; set; } = new();

    [Menu("Dexterity / Intelligence")]
    public ArchetypeGroupSettings DexInt { get; set; } = new();

    [Menu("Strength / Dexterity / Intelligence")]
    public ArchetypeGroupSettings StrDexInt { get; set; } = new();
}

[Submenu(CollapsedByDefault = true)]
public class ArchetypeGroupSettings
{
    [Menu("Archetypes", "Tick archetypes in this attribute group to flag matching mercenaries.")]
    [JsonIgnore]
    public CustomNode Table { get; set; } = new();
}
