namespace MushTown.Shared;

// The bag a ground drop returns to. A player drop carries the bag tab of the
// hand it came from. A monster drop has no source tab, so its item kind picks
// EQUIP or USE; do not guess a cash or decorative bag from the item id.
public static class DropBagRule
{
    public static string FromSource(string source, ItemData data) =>
        source != "" ? source : ForMonster(data);

    public static string ForMonster(ItemData data) => data.Slot != null ? "equip" : "use";

    // The byte on the drop wire. Mesos have no bag: 4 keeps "" distinct from
    // the USE code 0, so every value round-trips.
    public static byte Code(string tab) => tab switch
    {
        "" => 4,
        "use" => 0,
        "equip" => 1,
        "cash" => 2,
        "dec" => 3,
        _ => throw new InvalidDataException($"bad drop bag {tab}"),
    };

    public static string Name(byte tab) => tab switch
    {
        0 => "use",
        1 => "equip",
        2 => "cash",
        3 => "dec",
        4 => "",
        _ => throw new InvalidDataException($"bad drop bag code {tab}"),
    };
}
