using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MushTown.Shared;

// The default account passives of a Lv.200 character (Heroic, Maple Warrior,
// Will of the Alliance, Hero's Echo, every Link, the full Union and the six
// traits at Lv.100), folded into stat modifiers. The numbers live in
// Stats/default-passives.json; the aggregator treats them like any other
// source.
public static class DefaultPassives
{
    private sealed class Table
    {
        public List<Row> Modifiers { get; set; } = new();
    }

    private sealed class Row
    {
        public string Source { get; set; } = "";
        public List<StatKey> Keys { get; set; } = new();
        public StatValueType Type { get; set; }
        public double Value { get; set; }
    }

    public static IReadOnlyList<StatModifier> All { get; } = Load();

    private static List<StatModifier> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("default-passives.json")!;
        var table = JsonSerializer.Deserialize<Table>(stream, new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() },
        })!;
        return table.Modifiers
            .SelectMany(row => row.Keys.Select(key => new StatModifier(key, row.Type, row.Value, row.Source)))
            .ToList();
    }
}
