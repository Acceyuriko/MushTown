namespace MushTown.Shared;

// The macros of a character: five of them, each holds three skill ids in the order they
// cast (0 = an empty slot). A macro with no skill in it is empty and cannot be picked up.
public static class MacroBook
{
    public const int Count = 5;
    public const int Slots = 3;

    // A character without the field (the old save) starts with five empty macros.
    public static void Ensure(CharaSlot chara) =>
        chara.Macros ??= Enumerable.Range(0, Count).Select(_ => new List<int>(new int[Slots])).ToList();

    // A skill goes into a slot. A skill that was taken from another slot moves: that slot empties
    // (the origin goes first, so a drop back on the same slot keeps the skill).
    public static void Place(CharaSlot chara, int macro, int slot, int skill, (int Macro, int Slot)? from = null)
    {
        Ensure(chara);
        if (from is { } origin) chara.Macros![origin.Macro][origin.Slot] = 0;
        chara.Macros![macro][slot] = skill;
    }

    public static bool IsEmpty(CharaSlot chara, int macro) =>
        chara.Macros == null || chara.Macros[macro].All(id => id == 0);
}
