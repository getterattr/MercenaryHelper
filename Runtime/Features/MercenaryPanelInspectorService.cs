using System;
using System.Collections.Generic;
using System.Globalization;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;
using Vector2 = System.Numerics.Vector2;

namespace MercenaryHelper.Runtime.Features;

internal sealed class MercenaryPanelInspectorService
{
    // Display rules:
    //   Rucksack border + total label -> always yellow (identifies the container /
    //   summary, never competes with per-item value).
    //   Unique item box + label      -> highlight colour when chaos >= threshold,
    //                                   subdued orange otherwise. Threshold is the
    //                                   single source of truth for "flag me".
    //   Highest-value unique(s)      -> label is prepended with a star so the eye
    //                                   catches the standout piece regardless of
    //                                   threshold. Ties share the star.
    private const int RucksackBorderThickness = 3;
    private const int HighlightBorderThickness = 2;
    private const int SubduedBorderThickness = 1;
    private const double MaxValueTolerance = 0.001;
    private const string StarGlyph = "★";

    private static readonly Color RucksackColor = new(255, 200, 60, 220);
    private static readonly Color SubduedUniqueColor = new(255, 140, 0, 100);
    private static readonly Color PriceLabelBackground = new(0, 0, 0, 200);

    private readonly Main _plugin;

    public MercenaryPanelInspectorService(Main plugin)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    public void Render()
    {
        var settings = _plugin.Settings;

        var window = _plugin.GameController?.IngameState?.IngameUi?.MercenaryEncounterWindow;
        if (window == null || !window.IsVisible) return;

        var inventories = window.Inventories;
        if (inventories == null || inventories.Count == 0) return;

        var graphics = _plugin.Graphics;
        var priceCatalog = _plugin.UniquePrices;
        var highlightColor = settings.Configuration.HighlightColor.Value;
        var chaosThreshold = (double)settings.Configuration.ChaosThreshold.Value;

        var rucksack = FindRucksack(inventories);
        RectangleF? rucksackRect = rucksack != null ? TryGetRect(rucksack) : null;

        // Collect priced uniques and sum the rucksack in one pass, then compute
        // the top-value unique for the star glyph before drawing.
        var uniqueDraws = new List<UniqueDraw>();
        double rucksackTotalChaos = 0;
        var rucksackPriced = 0;

        foreach (var vendorInventory in inventories)
        {
            if (vendorInventory == null) continue;
            var items = vendorInventory.VisibleInventoryItems;
            if (items == null) continue;
            var isRucksack = ReferenceEquals(vendorInventory, rucksack);

            foreach (var slot in items)
            {
                var item = slot?.Item;
                if (item == null) continue;

                var name = TryGetItemName(item);
                double? unitChaos = null;
                if (name != null && priceCatalog.TryGetPrice(name, out var chaos))
                    unitChaos = chaos;

                if (isRucksack && unitChaos.HasValue)
                {
                    rucksackTotalChaos += unitChaos.Value * TryGetStackSize(item);
                    rucksackPriced++;
                }

                if (TryGetRarity(item) != ItemRarity.Unique) continue;

                var slotRect = TryGetRect(slot);
                if (!slotRect.HasValue) continue;

                uniqueDraws.Add(new UniqueDraw(slotRect.Value, unitChaos));
            }
        }

        double? topUniqueChaos = null;
        foreach (var d in uniqueDraws)
            if (d.Chaos is { } v && (!topUniqueChaos.HasValue || v > topUniqueChaos.Value))
                topUniqueChaos = v;

        foreach (var d in uniqueDraws)
        {
            var isHighValue = d.Chaos.HasValue && d.Chaos.Value >= chaosThreshold;
            var color = isHighValue ? highlightColor : SubduedUniqueColor;
            var thickness = isHighValue ? HighlightBorderThickness : SubduedBorderThickness;

            graphics.DrawFrame(d.Rect, color, thickness);

            if (d.Chaos.HasValue)
            {
                var isTop = topUniqueChaos.HasValue &&
                            Math.Abs(d.Chaos.Value - topUniqueChaos.Value) < MaxValueTolerance;
                var label = isTop
                    ? $"{StarGlyph} {d.Chaos.Value.ToString("N0", CultureInfo.InvariantCulture)}c"
                    : $"{d.Chaos.Value.ToString("N0", CultureInfo.InvariantCulture)}c";
                var textPos = new Vector2(d.Rect.X, d.Rect.Y);
                graphics.DrawTextWithBackground(label, textPos, color, PriceLabelBackground);
            }
        }

        if (rucksackRect.HasValue)
        {
            graphics.DrawFrame(rucksackRect.Value, RucksackColor, RucksackBorderThickness);

            if (rucksackPriced > 0)
            {
                var label = $"{rucksackTotalChaos.ToString("N0", CultureInfo.InvariantCulture)}c";
                var textPos = new Vector2(rucksackRect.Value.X, rucksackRect.Value.Y);
                graphics.DrawTextWithBackground(label, textPos, RucksackColor, PriceLabelBackground);
            }
        }
    }

    private static int TryGetStackSize(Entity item)
    {
        if (item == null) return 1;
        try
        {
            if (item.TryGetComponent<Stack>(out var stack))
            {
                var size = stack.Size;
                if (size > 0) return size;
            }
        }
        catch { }
        return 1;
    }

    private static VendorInventory FindRucksack(IReadOnlyList<VendorInventory> inventories)
    {
        VendorInventory best = null;
        long bestItemCount = 1;
        foreach (var vendorInventory in inventories)
        {
            if (vendorInventory == null) continue;
            long itemCount;
            try { itemCount = vendorInventory.ItemCount; }
            catch { itemCount = vendorInventory.VisibleInventoryItems?.Count ?? 0; }

            if (itemCount > bestItemCount)
            {
                best = vendorInventory;
                bestItemCount = itemCount;
            }
        }

        if (best != null) return best;

        long bestRowWidth = 0;
        foreach (var vendorInventory in inventories)
        {
            if (vendorInventory == null) continue;
            long width;
            try { width = vendorInventory.TotalBoxesInInventoryRow; }
            catch { width = 0; }

            if (width > bestRowWidth)
            {
                best = vendorInventory;
                bestRowWidth = width;
            }
        }
        return best;
    }

    private static RectangleF? TryGetRect(ExileCore.PoEMemory.Element element)
    {
        if (element == null) return null;
        try
        {
            var rect = element.GetClientRect();
            if (rect.Width > 0 && rect.Height > 0) return rect;
        }
        catch { }
        return null;
    }

    private static ItemRarity? TryGetRarity(Entity item)
    {
        if (item == null) return null;
        try
        {
            if (item.TryGetComponent<Mods>(out var mods)) return mods.ItemRarity;
        }
        catch { }
        return null;
    }

    private static string TryGetItemName(Entity item)
    {
        if (item == null) return null;
        try
        {
            if (item.TryGetComponent<Mods>(out var mods) && !string.IsNullOrWhiteSpace(mods.UniqueName))
                return mods.UniqueName;
        }
        catch { }
        try
        {
            if (item.TryGetComponent<Base>(out var baseComp) && !string.IsNullOrWhiteSpace(baseComp.Name))
                return baseComp.Name;
        }
        catch { }
        return null;
    }

    private readonly record struct UniqueDraw(RectangleF Rect, double? Chaos);
}
