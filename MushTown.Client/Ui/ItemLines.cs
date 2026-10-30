using MushTown.Shared;

namespace MushTown.Client;

// The detail lines of one item: the level it asks for, then the bonuses. The
// stat window's detail box and the item tooltip show the same lines, so they
// come from here and not from either panel.
public static class ItemLines
{
    // No HP/MP line: v1 takes no incHP/incMP from gear, and a potion's own
    // String.wz description already says what it restores.
    public static List<string> Detail(ItemData data)
    {
        var lines = new List<string>();
        if (data.ReqLevel > 0) lines.Add($"Required Level Lv. {data.ReqLevel}");
        if (data.Atk != 0) lines.Add($"Attack +{data.Atk}");
        if (data.Def != 0) lines.Add($"Defense +{data.Def}");
        if (data.Str != 0) lines.Add($"STR +{data.Str}");
        if (data.Dex != 0) lines.Add($"DEX +{data.Dex}");
        return lines;
    }
}
