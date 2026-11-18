using MushTown.Shared;

namespace MushTown.Client;

// The detail lines of one item: the level and job it asks for, then the bonuses. The
// stat window's detail box and the item tooltip show the same lines, so they
// come from here and not from either panel.
public static class ItemLines
{
    // WZ reqJob bit mask: one bit per first-job class.
    private static readonly (int Bit, string Name)[] Jobs =
    {
        (1, "Warrior"), (2, "Magician"), (4, "Bowman"), (8, "Thief"), (16, "Pirate"),
    };

    // The six jobs of the equipment tooltip's job bar. A job lights up when
    // the item asks for it; reqJob 0 means every job, a mask has no Beginner bit.
    public static readonly string[] JobBar = { "Beginner", "Warrior", "Magician", "Bowman", "Thief", "Pirate" };

    public static bool JobLit(ItemData data, int i) =>
        data.ReqJob == 0 || (i > 0 && (data.ReqJob & (1 << (i - 1))) != 0);

    // A potion's own String.wz description already says what it restores, so
    // only a worn equip's max HP/MP (Hp/Mp are the potion heal) gets a line.
    public static List<string> Detail(ItemData data)
    {
        var lines = new List<string>();
        if (data.ReqLevel > 0) lines.Add($"Required Level Lv. {data.ReqLevel}");
        if (data.ReqJob != 0) lines.Add("Job: " + string.Join(", ", Jobs.Where(j => (data.ReqJob & j.Bit) != 0).Select(j => j.Name)));
        lines.AddRange(Bonuses(data));
        return lines;
    }

    // Only the bonus lines (the equipment tooltip prints the level and job its own way).
    public static List<string> Bonuses(ItemData data)
    {
        var lines = new List<string>();
        if (data.Atk != 0) lines.Add($"Attack +{data.Atk}");
        if (data.Mad != 0) lines.Add($"Magic Attack +{data.Mad}");
        if (data.Def != 0) lines.Add($"Defense +{data.Def}");
        if (data.Mdd != 0) lines.Add($"Magic Defense +{data.Mdd}");
        if (data.Str != 0) lines.Add($"STR +{data.Str}");
        if (data.Dex != 0) lines.Add($"DEX +{data.Dex}");
        if (data.Int != 0) lines.Add($"INT +{data.Int}");
        if (data.Luk != 0) lines.Add($"LUK +{data.Luk}");
        if (data.MaxHp != 0) lines.Add($"Max HP +{data.MaxHp}");
        if (data.MaxMp != 0) lines.Add($"Max MP +{data.MaxMp}");
        if (data.Acc != 0) lines.Add($"Accuracy +{data.Acc}");
        if (data.Eva != 0) lines.Add($"Avoidability +{data.Eva}");
        if (data.Speed != 0) lines.Add($"Speed +{data.Speed}");
        if (data.Jump != 0) lines.Add($"Jump +{data.Jump}");
        return lines;
    }
}
