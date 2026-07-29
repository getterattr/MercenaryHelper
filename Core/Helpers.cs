using System;

namespace MercenaryHelper;

internal static class Helpers
{
    public static bool EqualsIgnoreCase(this string value, string other) =>
        string.Equals(value, other, StringComparison.OrdinalIgnoreCase);

    public static string TryGetAreaHashText(object area)
    {
        if (area == null)
        {
            return null;
        }

        static string TryReadPropertyString(object value, string propertyName) =>
            value.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();

        return TryReadPropertyString(area, "AreaHash") ?? TryReadPropertyString(area, "Hash");
    }

    public static int TryGetAreaInstanceId(object area)
    {
        if (area == null) return -1;
        var val = area.GetType().GetProperty("InstanceId")?.GetValue(area);
        if (val is int id) return id;
        if (val != null && int.TryParse(val.ToString(), out var parsed)) return parsed;
        return -1;
    }

    public static string TryGetAreaNameText(object area)
    {
        if (area == null)
        {
            return string.Empty;
        }

        static string TryReadPropertyString(object value, string propertyName) =>
            value.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();

        return TryReadPropertyString(area, "Name")
               ?? TryReadPropertyString(area, "DisplayName")
               ?? TryReadPropertyString(area, "RawName")
               ?? string.Empty;
    }
}
