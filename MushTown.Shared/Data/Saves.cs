using System.Text.Json;

namespace MushTown.Shared;

// Local saves: one file per PC (data/saves.json). No accounts, no passwords:
// your saves stay on your PC and travel with you to any host you join.
// One save holds one playthrough's heroes. The two consts below are the only
// caps, so raising them later needs no format change (short files grow with
// fresh saves). Every slot always holds a save: the constructor fills all
// three and Load heals nulls from old files, so readers use ! on Saves[i].
public sealed class SaveStore
{
    public const int MaxSaves = 3;
    public const int MaxHeroes = 60; // 12 per page x 5 pages, like the official select

    public List<SaveData?> Saves { get; set; } = new();

    public SaveStore()
    {
        for (int i = 0; i < MaxSaves; i++) Saves.Add(new SaveData());
    }

    private static void Pad<T>(List<T?> list, int n) where T : class
    {
        while (list.Count < n) list.Add(null);
    }

    // Missing file means first run: start empty. Bad JSON fails loud.
    public static SaveStore Load(string path)
    {
        if (!File.Exists(path)) return new SaveStore();
        SaveStore store;
        try
        {
            store = JsonSerializer.Deserialize<SaveStore>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"corrupt save file: {path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"corrupt save file: {path}", ex);
        }
        if (store.Saves == null || store.Saves.Count > MaxSaves)
            throw new InvalidDataException($"corrupt save file: {path}");
        Pad(store.Saves, MaxSaves);
        for (int i = 0; i < store.Saves.Count; i++)
        {
            if (store.Saves[i] == null) { store.Saves[i] = new SaveData(); continue; } // old files: nulls heal
            var save = store.Saves[i]!;
            if (save.Slots == null || save.Slots.Count > MaxHeroes)
                throw new InvalidDataException($"corrupt save file: {path}");
            Pad(save.Slots, MaxHeroes);
        }
        return store;
    }

    // Atomic write: a crash leaves the old file or the new one, never half.
    // Trailing empty slots trim off (60-wide files stay small); Load pads back.
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var slim = new SaveStore
        {
            Saves = Saves.Select(s => s == null ? null : new SaveData { Slots = Trimmed(s.Slots) }).ToList(),
        };
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(slim));
        File.Move(tmp, path, true);
    }

    private static List<CharaSlot?> Trimmed(List<CharaSlot?> slots)
    {
        var kept = slots.ToList();
        while (kept.Count > 0 && kept[^1] == null) kept.RemoveAt(kept.Count - 1);
        return kept;
    }

    public CharaSlot CreateHero(int save, int slot, string name)
    {
        CheckId(name, "character name");
        if ((uint)save >= MaxSaves)
            throw new InvalidDataException($"bad save slot {save}");
        if ((uint)slot >= MaxHeroes)
            throw new InvalidDataException($"bad slot {slot}");
        foreach (var s in Saves)
        {
            foreach (var h in s!.Slots)
                if (h?.Name == name)
                    throw new InvalidDataException($"character name {name} is taken");
        }
        var heroes = Saves[save]!.Slots;
        if (heroes[slot] != null)
            throw new InvalidDataException($"slot {slot} is taken");
        var chara = new CharaSlot
        {
            Name = name,
            Sp = Progression.SpPerLevel, // creation banks the first level's points
            // Starter kit: the only potion source until W13 drops (no shop until W18).
            Use = new Dictionary<int, int> { { ItemData.RedPotion, 20 }, { ItemData.BluePotion, 10 } },
        };
        heroes[slot] = chara;
        return chara;
    }

    public void DeleteHero(int save, int slot)
    {
        if ((uint)save >= MaxSaves || (uint)slot >= MaxHeroes)
            throw new InvalidDataException($"bad slot {save}:{slot}");
        Saves[save]!.Slots[slot] = null; // an empty save here is a programmer bug (menus always pass a live save)
        CompactHeroes(save); // the rest slide left: heroes always pack from slot 0, in order
    }

    // Stable pack: filled slots move to the front, order kept.
    private void CompactHeroes(int save)
    {
        var heroes = Saves[save]!.Slots;
        int next = 0;
        for (int i = 0; i < heroes.Count; i++)
            if (heroes[i] != null) heroes[next++] = heroes[i];
        for (int i = next; i < heroes.Count; i++) heroes[i] = null;
    }

    // One charset for character names: the client reuses this while typing
    // so the two can never drift apart.
    public static bool IsIdChar(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';

    private static void CheckId(string id, string what)
    {
        if (id.Length is < 1 or > 12 || !id.All(IsIdChar))
            throw new InvalidDataException($"bad {what} {id} (1-12 letters, digits, _ or -)");
    }
}

// One save: one playthrough's heroes (null = empty hero slot).
public sealed class SaveData
{
    public List<CharaSlot?> Slots { get; set; } = new();

    // New saves start fully padded: shot poses index Slots directly.
    // (Load replaces this list with the file content, then pads again.)
    public SaveData()
    {
        while (Slots.Count < SaveStore.MaxHeroes) Slots.Add(null);
    }
}

// One character. X/Y null means never played: spawn at the map portal.
// Equips null means the beginner set (nothing worn); EquipBag null means
// an empty equip bag (pre-W17 saves never had one).
// Use null means an empty bag (pre-W10 saves never got the starter kit).
// SkillLv maps skill -> level (lv 0 = unlearned, no entry); Sp is unspent
// skill points (pre-W12 saves start at 0 and earn fresh).
public sealed class CharaSlot
{
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public int Map { get; set; } = 10000;
    public int? X { get; set; }
    public int? Y { get; set; }
    public int Mesos { get; set; }
    public int Sp { get; set; }
    public int Hp { get; set; } = Progression.MaxHp(1);
    public int Mp { get; set; } = Progression.MaxMp(1);
    public int Str { get; set; } = 4;
    public int Dex { get; set; } = 4;
    public int Int { get; set; } = 4;
    public int Luk { get; set; } = 4;
    public Dictionary<int, int>? Use { get; set; } // itemId -> count (USE tab)
    public Dictionary<int, int>? EquipBag { get; set; } // itemId -> count (EQUIP tab bag)
    public List<Dictionary<string, string>>? KeyPresets { get; set; } // three key -> target maps (null = defaults)
    public int ActivePreset { get; set; } // which of the three is live (0..2)
    public List<int>? Equips { get; set; } // worn equip ids (null = beginner set)
    public Dictionary<int, int> SkillLv { get; set; } = new();
    public List<int> Quests { get; set; } = new(); // accepted quest ids
    public List<int> QuestsDone { get; set; } = new(); // turned-in quest ids
    public Dictionary<string, int> QuestKills { get; set; } = new(); // "qid:index" -> capped kill count
}
