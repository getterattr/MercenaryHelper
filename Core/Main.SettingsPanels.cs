using System;
using System.Linq;
using ImGuiNET;
using Vector4 = System.Numerics.Vector4;

namespace MercenaryHelper;

public partial class Main
{
    private static readonly Vector4 SummaryAccentColor = new(0.95f, 0.74f, 0.26f, 1f);
    private static readonly Vector4 SummaryOkColor = new(0.47f, 0.90f, 0.56f, 1f);
    private static readonly Vector4 SummaryMutedColor = new(0.63f, 0.66f, 0.72f, 1f);
    private static readonly Vector4 SummaryErrorColor = new(1f, 0.4f, 0.4f, 1f);
    private const ImGuiTableFlags SummaryTableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg;

    private static void DrawSectionHeader(string title)
    {
        ImGui.Dummy(new System.Numerics.Vector2(0, 12));
        ImGui.TextColored(SummaryAccentColor, title);
        ImGui.Separator();
    }

    private void DrawOverviewPanel()
    {
        var settings = Settings;
        var catalog = UniquePrices;
        if (settings == null)
        {
            ImGui.TextDisabled("Settings unavailable.");
            return;
        }

        ImGui.Dummy(new System.Numerics.Vector2(0, 8));
        ImGui.TextColored(SummaryAccentColor, "Overview");
        ImGui.Separator();

        if (ImGui.BeginTable("##MercenaryHelperOverview", 2, SummaryTableFlags))
        {
            DrawSummaryRow("Plugin state", settings.Enable.Value ? "Enabled" : "Disabled", GetStateColor(settings.Enable.Value));
            DrawSummaryRow("Flagged archetypes", settings.EnabledArchetypes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            DrawSummaryRow("Unique prices",
                catalog.IsFetching
                    ? "fetching..."
                    : catalog.LastFetchUtc == DateTime.MinValue
                        ? "no fetch yet"
                        : $"{catalog.PriceCount} cached ({catalog.LastFetchLeague ?? "?"}, {catalog.LastFetchUtc.ToLocalTime():HH:mm})",
                GetStateColor(catalog.PriceCount > 0));
            ImGui.EndTable();
        }
    }

    private void DrawPricingStatusPanel()
    {
        var catalog = UniquePrices;

        ImGui.Dummy(new System.Numerics.Vector2(0, 4));

        if (catalog.IsFetching)
        {
            ImGui.TextColored(SummaryAccentColor, "Fetching latest prices from poe.ninja...");
            return;
        }

        if (catalog.LastFetchUtc == DateTime.MinValue)
        {
            ImGui.TextColored(SummaryMutedColor, "No prices fetched yet. Press Refresh Prices above.");
        }
        else
        {
            ImGui.TextColored(SummaryMutedColor, $"League: {catalog.LastFetchLeague ?? "?"}");
            ImGui.TextColored(SummaryMutedColor, $"Last fetch: {catalog.LastFetchUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
            ImGui.TextColored(SummaryMutedColor, $"Cached prices: {catalog.PriceCount}");
        }

        if (!string.IsNullOrWhiteSpace(catalog.LastFetchError))
            ImGui.TextColored(SummaryErrorColor, $"Last error: {catalog.LastFetchError}");
    }

    private void DrawArchetypeTableForAttribute(string attributeKey)
    {
        var enabled = Settings.EnabledArchetypes;
        var catalog = Archetypes;

        if (!catalog.IsLoaded)
        {
            ImGui.TextColored(SummaryErrorColor, $"Archetype catalog not loaded ({catalog.LoadError ?? "unknown reason"}).");
            return;
        }

        var rows = catalog.Archetypes
            .Where(a => string.Equals(a.Attribute, attributeKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Name, StringComparer.Ordinal)
            .ToArray();

        if (rows.Length == 0)
        {
            ImGui.TextColored(SummaryMutedColor, "No archetypes in this group.");
            return;
        }

        if (ImGui.Button($"Select All##{attributeKey}"))
        {
            foreach (var a in rows) enabled.Add(a.Name);
            SavePersistedPickerState();
        }
        ImGui.SameLine();
        if (ImGui.Button($"Clear##{attributeKey}"))
        {
            foreach (var a in rows) enabled.Remove(a.Name);
            SavePersistedPickerState();
        }

        if (!ImGui.BeginTable($"##ArchTable_{attributeKey}", 3, SummaryTableFlags)) return;

        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 40f);
        ImGui.TableSetupColumn("Archetype", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Tribe", ImGuiTableColumnFlags.WidthFixed, 100f);
        ImGui.TableHeadersRow();

        foreach (var archetype in rows)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var isEnabled = enabled.Contains(archetype.Name);
            if (ImGui.Checkbox($"##{archetype.Name}", ref isEnabled))
            {
                if (isEnabled) enabled.Add(archetype.Name);
                else enabled.Remove(archetype.Name);
                SavePersistedPickerState();
            }
            ImGui.TableNextColumn();
            if (isEnabled) ImGui.TextColored(SummaryOkColor, archetype.Name);
            else ImGui.Text(archetype.Name);
            ImGui.TableNextColumn();
            ImGui.TextColored(SummaryMutedColor, archetype.Tribe ?? "");
        }

        ImGui.EndTable();
    }

    private void DrawQualityRulesPanel()
    {
        var ruleset = QualityRules;

        ImGui.Dummy(new System.Numerics.Vector2(0, 4));

        if (!ruleset.IsLoaded)
        {
            ImGui.TextColored(SummaryErrorColor, $"Quality ruleset not loaded ({ruleset.LoadError ?? "unknown"}).");
            return;
        }

        ImGui.TextWrapped("A flagged mercenary is only highlighted if it has ALL Required skills AND none of the Forbidden skills for its archetype. Rules with no Required entries pass automatically. Edits save immediately to MercenaryHelper_quality.json.");

        ImGui.Dummy(new System.Numerics.Vector2(0, 4));

        if (ruleset.RuleCount == 0)
        {
            ImGui.TextColored(SummaryMutedColor, "No rules defined. Press Add Rule below to create one.");
        }
        else
        {
            const ImGuiTableFlags rulesTableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg;
            if (ImGui.BeginTable("##QualityRulesTable", 4, rulesTableFlags))
            {
                ImGui.TableSetupColumn("Archetype", ImGuiTableColumnFlags.WidthFixed, 160f);
                ImGui.TableSetupColumn("Required (comma-separated)", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Forbidden (comma-separated)", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 40f);
                ImGui.TableHeadersRow();

                var removeIndex = -1;
                for (var i = 0; i < ruleset.Rules.Count; i++)
                {
                    var rule = ruleset.Rules[i];
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    var archetypeName = rule.Archetype ?? string.Empty;
                    if (ImGui.InputText($"##rule_arch_{i}", ref archetypeName, 64))
                    {
                        rule.Archetype = archetypeName;
                        ruleset.PersistEdits();
                    }

                    ImGui.TableNextColumn();
                    var requiredText = string.Join(", ", rule.Required);
                    if (ImGui.InputText($"##rule_req_{i}", ref requiredText, 512))
                    {
                        rule.Required = ParseCsv(requiredText);
                        ruleset.PersistEdits();
                    }

                    ImGui.TableNextColumn();
                    var forbiddenText = string.Join(", ", rule.Forbidden);
                    if (ImGui.InputText($"##rule_forb_{i}", ref forbiddenText, 512))
                    {
                        rule.Forbidden = ParseCsv(forbiddenText);
                        ruleset.PersistEdits();
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.Button($"×##rule_del_{i}"))
                        removeIndex = i;
                }
                ImGui.EndTable();

                if (removeIndex >= 0) ruleset.RemoveRule(removeIndex);
            }
        }

        ImGui.Dummy(new System.Numerics.Vector2(0, 4));
        if (ImGui.Button("Add Rule"))
        {
            ruleset.AddRule("");
        }
    }

    private static System.Collections.Generic.List<string> ParseCsv(string input)
    {
        var result = new System.Collections.Generic.List<string>();
        if (string.IsNullOrWhiteSpace(input)) return result;
        foreach (var part in input.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0) result.Add(trimmed);
        }
        return result;
    }

    private static void DrawSummaryRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(SummaryMutedColor, label);
        ImGui.TableNextColumn();
        ImGui.Text(value ?? string.Empty);
    }

    private static void DrawSummaryRow(string label, string value, Vector4 valueColor)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(SummaryMutedColor, label);
        ImGui.TableNextColumn();
        ImGui.TextColored(valueColor, value ?? string.Empty);
    }

    private static Vector4 GetStateColor(bool enabled) => enabled ? SummaryOkColor : SummaryMutedColor;

    private static string FormatText(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

}
