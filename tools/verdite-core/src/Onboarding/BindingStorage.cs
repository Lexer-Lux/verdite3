using System.Globalization;
using System.Reflection;
using RecompOne.Runtime.Config;

namespace Verdite.Core;

/// <summary>Setup choices can edit the same player-one binding tables as Input.
/// Values are read from settings.json, never mirrored into interface.ini.</summary>
internal static class BindingStorage
{
    const string Prefix = "input.";

    static (object Table, PropertyInfo Field)? Field(string key)
    {
        if (!key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var path = key[Prefix.Length..].Split('.');
        if (path.Length != 2) throw new InvalidDataException($"Invalid binding key: {key}");
        object table = path[0].ToLowerInvariant() switch
        {
            "keys" => ConfigManager.Game.Keys,
            "pad" => ConfigManager.Game.Pad,
            "padanalog" => ConfigManager.Game.PadAnalog,
            _ => throw new InvalidDataException($"Unknown binding table: {key}"),
        };
        var field = table.GetType().GetProperty(path[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field is null || !field.CanWrite || field.PropertyType != typeof(string)
            && field.PropertyType != typeof(int[]) && field.PropertyType != typeof(int))
            throw new InvalidDataException($"Unknown binding field: {key}");
        return (table, field);
    }

    public static bool IsKey(string key) => key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string Read(string key)
    {
        var (table, field) = Field(key)!.Value;
        return field.GetValue(table) switch
        {
            string text => text,
            int[] ids => string.Join(',', ids),
            int axis => axis.ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidDataException($"No binding value: {key}"),
        };
    }

    static object Parse(PropertyInfo field, string value) => field.PropertyType == typeof(string) ? value
        : field.PropertyType == typeof(int) ? int.Parse(value, CultureInfo.InvariantCulture)
        : value.Length == 0 ? Array.Empty<int>() : value.Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();

    public static void Validate(string key, string? value)
    {
        var (_, field) = Field(key)!.Value;
        if (value is null) throw new InvalidDataException($"Setup binding has no default: {key}");
        _ = Parse(field, value);
    }

    public static void Write(string key, string value)
    {
        var (table, field) = Field(key)!.Value;
        field.SetValue(table, Parse(field, value));
    }
}
