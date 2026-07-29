using System;
using System.Collections.Generic;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

namespace MercenaryHelper.Runtime.Features;

internal sealed class MercenaryPanelInspectorService
{
    private const int BorderThickness = 3;
    private static readonly Color RucksackBorderColor = new(255, 200, 60, 220);
    private static readonly Color SubduedUniqueColor = new(255, 140, 0, 255);
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
        var chaosThreshold = settings.Configuration.ChaosThreshold.Value;

        var rucksack = FindRucksack(inventories);
        if (rucksack != null)
        {
            var rect = TryGetRect(rucksack);
            if (rect.HasValue)
                graphics.DrawFrame(rect.Value, RucksackBorderColor, BorderThickness);
        }

        foreach (var vendorInventory in inventories)
        {
            if (vendorInventory == null) continue;
            var items = vendorInventory.VisibleInventoryItems;
            if (items == null) continue;

            foreach (var slot in items)
            {
                var item = slot?.Item;
                if (item == null) continue;
                if (TryGetRarity(item) != ItemRarity.Unique) continue;

                var uniqueName = TryGetUniqueName(item);
                double? priceChaos = null;
                if (uniqueName != null && priceCatalog.TryGetPrice(uniqueName, out var chaos))
                    priceChaos = chaos;

                var isHighValue = priceChaos.HasValue && priceChaos.Value >= chaosThreshold;
                var color = isHighValue ? highlightColor : SubduedUniqueColor;

                var itemRect = TryGetRect(slot);
                if (!itemRect.HasValue) continue;

                graphics.DrawFrame(itemRect.Value, color, BorderThickness);

                if (priceChaos.HasValue)
                {
                    var label = $"{priceChaos.Value:N0}c";
                    var textPos = new System.Numerics.Vector2(itemRect.Value.X + 4, itemRect.Value.Y + 4);
                    graphics.DrawTextWithBackground(label, textPos, color, PriceLabelBackground);
                }
            }
        }
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

    private static string TryGetUniqueName(Entity item)
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
}
