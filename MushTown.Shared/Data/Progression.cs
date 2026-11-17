namespace MushTown.Shared;

// Leveling and vitals: pure math shared by the client (EXP bar, potions) and
// the server (damage judging). The attack panel lives in DamageChain.
// W10 plays a fixed warrior line: auto stat points on level-up (M4).
public static class Progression
{
    // CLI/no-save placeholder attack. There is no character or equipment
    // behind this value; 32 is unrelated to any item. Real characters use
    // their worn weapon.
    public const int WeaponAtk = 32;

    // EXP to climb from this level to the next. Linear v1 curve: levels 1-10
    // cost 1035 total, and one training center pays about that much per
    // clear, so grinding the training mobs always outruns the cap.
    public static long ExpNext(int level) => 20L * level + 15;

    // A new hero starts at level 200 with every AP spent on the class's main stat.
    // Total AP follows the v271 4th-job formula 14 + 5 per level (.dsh/research/character-baseline.md).
    public const int StartLevel = 200;
    public const int StartAp = 14 + 5 * StartLevel;

    public static void GiveStartAp(CharaSlot chara, StatKey main)
    {
        switch (main)
        {
            case StatKey.STR: chara.Str += StartAp; break;
            case StatKey.DEX: chara.Dex += StartAp; break;
            case StatKey.INT: chara.Int += StartAp; break;
            case StatKey.LUK: chara.Luk += StartAp; break;
        }
    }

    // A new hero is born with full pools. The real max (with passives) lives in the game's stat
    // snapshot, so the save holds this "full" value and the first frame in the game clamps it down.
    public const long FullPool = long.MaxValue;

    // Pools never exceed the max (a skill expiring or a new hero's FullPool shrinks them; never heals).
    public static void ClampPools(CharaSlot chara, long maxHp, long maxMp)
    {
        chara.Hp = Math.Min(chara.Hp, maxHp);
        chara.Mp = Math.Min(chara.Mp, maxMp);
    }

    // Vitals grow per level; a level-up refills both pools (official does too).
    public static long MaxHp(int level) => 50 + 30L * (level - 1);
    public static long MaxMp(int level) => 5 + 5L * (level - 1);

    // Combat stats the client sends the server (Build message) so damage is
    // judged on the real attacker. Beginner is the default for clients that
    // never sent one (CLI sessions). DmgPct/CritPct/FinalPct fold every
    // skill source client-side (passives, buffs, orbs stay in SkillCast).
    public static readonly Build Beginner = new(4, 4, WeaponAtk, 0, 0, 0);

    // Active damage pct at a skill level: lv 1 deals DmgLo, max deals DmgHi,
    // linear between. Single-level skills (max 1) always deal DmgLo.
    public static int SkillDamage(SkillData data, int lv) =>
        data.MaxLevel <= 1 ? data.DmgLo : data.DmgLo + (data.DmgHi - data.DmgLo) * (lv - 1) / (data.MaxLevel - 1);

    // The SP one skill level cost (the SP Reset coupon still refunds with it).
    public const int SkillSpCost = 1;

    // Adds EXP, levels up while the bar is full. Each level: auto stat points
    // (+4 STR +1 DEX warrior ratio), pools refilled to the max supplied
    // by the caller. Returns levels gained (0 when the bar is not full yet).
    public static int ApplyExp(CharaSlot chara, long exp, long maxHp)
    {
        chara.Exp += exp;
        int ups = 0;
        while (chara.Exp >= ExpNext(chara.Level))
        {
            chara.Exp -= ExpNext(chara.Level);
            chara.Level++;
            chara.Str += 4;
            chara.Dex += 1;
            chara.Hp = maxHp;
            chara.Mp = MaxMp(chara.Level);
            ups++;
        }
        return ups;
    }

    // Drinks one potion: needs a bottle in the bag and a pool that is not
    // full. Returns false when nothing happens (empty or already full).
    public static bool UsePotion(CharaSlot chara, int itemId, long healHp, long healMp, long maxHp, long maxMp)
    {
        var use = chara.Use;
        if (use == null || !use.TryGetValue(itemId, out int count) || count <= 0)
            return false;
        bool need = (healHp > 0 && chara.Hp < maxHp) || (healMp > 0 && chara.Mp < maxMp);
        if (!need) return false;
        Inventory.Remove(chara, "use", itemId, 1);
        chara.Hp = Math.Min(chara.Hp + healHp, maxHp);
        chara.Mp = Math.Min(chara.Mp + healMp, maxMp);
        return true;
    }

    // Damage skin storage: the WZ canvas has 7x5 cells; a switch costs
    // 100,000 mesos (Etc/DamageSkinSaveInfo.img change_fee).
    public const int DamageSkinSlots = 35;
    public const long DamageSkinChangeFee = 100_000;

    // Saves the current skin into the storage. False only when a saved cell
    // is needed and all 35 are used; the basic skin and a duplicate need no
    // cell and return true.
    public static bool SaveCurrentDamageSkin(CharaSlot chara)
    {
        if (chara.DamageSkin == 0) return true;
        var skins = chara.DamageSkins ??= new List<int>();
        if (skins.Contains(chara.DamageSkin)) return true;
        if (skins.Count >= DamageSkinSlots) return false;
        skins.Add(chara.DamageSkin);
        return true;
    }

    // Deletes one storage cell. The current skin is separate from the list,
    // so deleting its saved copy leaves the effect in use. False for a bad
    // cell (an old save has no list).
    public static bool DeleteDamageSkin(CharaSlot chara, int index)
    {
        if (chara.DamageSkins is not { } skins || index < 0 || index >= skins.Count) return false;
        skins.RemoveAt(index);
        return true;
    }

    // Uses one damage skin coupon: the old unsaved skin goes to the storage
    // first, then one coupon leaves the USE bag and the new skin takes over.
    // False when the bag has no coupon or the old skin cannot be saved.
    public static bool UseDamageSkinCoupon(CharaSlot chara, ItemData item)
    {
        if (item.DamageSkin == 0 || Inventory.Count(chara, "use", item.ItemId) <= 0) return false;
        if (!SaveCurrentDamageSkin(chara)) return false;
        Inventory.Remove(chara, "use", item.ItemId, 1);
        chara.DamageSkin = item.DamageSkin;
        return true;
    }

    // Switches to a saved skin for 100,000 mesos. The old unsaved skin goes
    // to the storage first. False when the fee is short, the storage is full,
    // or the skin is already current.
    public static bool SwitchDamageSkin(CharaSlot chara, int skin)
    {
        if (skin == 0 || skin == chara.DamageSkin) return false;
        if (chara.Mesos < DamageSkinChangeFee) return false;
        if (!SaveCurrentDamageSkin(chara)) return false;
        chara.Mesos -= DamageSkinChangeFee;
        chara.DamageSkin = skin;
        return true;
    }

    // Wears one from the EQUIP bag. Same slot replaces same slot; an overall
    // occupies the top and bottom area, so it swaps out a top and pants, and a
    // top or pants swaps out an overall. True when worn.
    public static bool WearEquip(CharaSlot chara, int id, IReadOnlyDictionary<int, ItemData> defs)
    {
        var def = defs[id]; // callers only pass loaded equips (client boot proves their slots)
        chara.Equips ??= new List<int>();
        var old = new List<int>();
        foreach (int worn in chara.Equips)
            if (defs.TryGetValue(worn, out var wd) && SameBodyArea(wd.Slot, def.Slot))
                old.Add(worn);
        // Removing the new item frees one cell; the old pieces need one each.
        if (Inventory.Used(chara, "equip") - 1 + old.Count > Inventory.Capacity) return false;
        if (!Inventory.Remove(chara, "equip", id, 1)) return false;
        foreach (int oldId in old)
        {
            chara.Equips.Remove(oldId);
            Inventory.Add(chara, "equip", oldId, 1); // the precheck above guarantees room
        }
        chara.Equips.Add(id);
        return true;
    }

    public static bool SameBodyArea(string? a, string? b) =>
        a == b
        || (b == "MaPn" && a is "Ma" or "Pn")
        || (a == "MaPn" && b is "Ma" or "Pn");

    // Takes one off, back to the bag. Unworn slots do nothing. A hand-made
    // save may wear unknown ids (they draw and grant nothing); the tab draws
    // no cell for them, so they sit stuck but harmless.
    public static bool TakeOffEquip(CharaSlot chara, int id)
    {
        if (chara.Equips == null || !chara.Equips.Contains(id)) return false;
        if (!Inventory.CanAdd(chara, "equip", id, 1)) return false; // bag full: stay worn
        chara.Equips.Remove(id);
        Inventory.Add(chara, "equip", id, 1);
        return true;
    }

    // Wears one decoration from DEC. A fashion swaps out the same body area;
    // a hair or face coupon swaps out the coupon matching the current look.
    // Returns false when the bag has no copy or there is no room for the old
    // piece that has to come back.
    public static bool WearCash(CharaSlot chara, int id, IReadOnlyDictionary<int, ItemData> defs)
    {
        if (!defs.TryGetValue(id, out var data) || Inventory.Count(chara, "dec", id) <= 0) return false;
        Inventory.Ensure(chara);
        chara.CashWorn ??= new List<int>();
        if (data.Slot != null) return WearCashFashion(chara, chara.CashWorn, id, data, defs);
        if (data.Cosmetic == 0) return false;
        string? coupon = CouponSlot(id);
        if (coupon == null) return false;
        bool hair = coupon == "hair";
        bool face = coupon == "face";

        int old = 0;
        foreach (int worn in chara.CashWorn)
        {
            if (!defs.TryGetValue(worn, out var wornData) || wornData.Cosmetic == 0) continue;
            if ((hair && wornData.Cosmetic == chara.Hair) || (face && wornData.Cosmetic == chara.Face))
            {
                old = worn;
                break;
            }
        }
        int freed = Inventory.Count(chara, "dec", id) == 1 ? 1 : 0;
        int addCells = old != 0 && Inventory.Count(chara, "dec", old) == 0 ? 1 : 0;
        if (Inventory.Used(chara, "dec") - freed + addCells > Inventory.Capacity) return false;
        if (!Inventory.Remove(chara, "dec", id, 1)) return false;
        if (old != 0)
        {
            chara.CashWorn.Remove(old);
            Inventory.Add(chara, "dec", old, 1); // the precheck above guarantees room
        }
        chara.CashWorn.Add(id);
        if (hair) chara.Hair = data.Cosmetic;
        else chara.Face = data.Cosmetic;
        return true;
    }

    private static bool WearCashFashion(CharaSlot chara, List<int> cashWorn, int id, ItemData data, IReadOnlyDictionary<int, ItemData> defs)
    {
        var old = new List<int>();
        foreach (int worn in cashWorn)
            if (defs.TryGetValue(worn, out var wornData) && wornData.Slot != null && SameBodyArea(wornData.Slot, data.Slot))
                old.Add(worn);
        int freed = Inventory.Count(chara, "dec", id) == 1 ? 1 : 0;
        int addCells = old.Count(worn => Inventory.Count(chara, "dec", worn) == 0);
        if (Inventory.Used(chara, "dec") - freed + addCells > Inventory.Capacity) return false;
        if (!Inventory.Remove(chara, "dec", id, 1)) return false;
        foreach (int worn in old)
        {
            cashWorn.Remove(worn);
            Inventory.Add(chara, "dec", worn, 1); // the precheck above guarantees room
        }
        cashWorn.Add(id);
        return true;
    }

    // Coupon ids split by kind: 5157xxx is hair, 5158xxx is face.
    public static string? CouponSlot(int id) =>
        id / 1000 == 5157 ? "hair" : id / 1000 == 5158 ? "face" : null;

    // Takes one fashion off, back to DEC. A hair or face coupon comes off
    // too: the look falls back to the default one.
    public static bool TakeOffCashFashion(CharaSlot chara, int id, IReadOnlyDictionary<int, ItemData> defs)
    {
        if (chara.CashWorn == null || !chara.CashWorn.Contains(id)) return false;
        if (!defs.TryGetValue(id, out var data)) return false;
        string? coupon = CouponSlot(id);
        if (data.Slot == null && coupon == null) return false;
        if (!Inventory.CanAdd(chara, "dec", id, 1)) return false; // bag full: stay worn
        chara.CashWorn.Remove(id);
        Inventory.Add(chara, "dec", id, 1);
        if (coupon == "hair") chara.Hair = ItemData.DefaultHair;
        else if (coupon == "face") chara.Face = ItemData.DefaultFace;
        return true;
    }

    // The attack action name for the current appearance: a cash weapon wins,
    // then the worn gear weapon, then the beginner one-handed sword.
    public static string AttackAction(CharaSlot? chara, IReadOnlyDictionary<int, ItemData> defs)
    {
        int? weapon = null;
        if (chara?.CashWorn != null)
            foreach (int id in chara.CashWorn)
                if (defs.TryGetValue(id, out var data) && data.Slot == "Wp") { weapon = data.Attack; break; }
        if (weapon == null && chara?.Equips != null)
            foreach (int id in chara.Equips)
                if (defs.TryGetValue(id, out var data) && data.Slot == "Wp") { weapon = data.Attack; break; }
        return (weapon ?? 1) switch
        {
            2 => "swingT1",
            3 => "swingP1",
            4 => "stabO1",
            5 => "stabT1",
            6 => "shoot1",
            _ => "swingO1",
        };
    }

    // One cast's checks: learned, MP, cooldown (the character level does not matter: every
    // skill stands at its max level). The client deducts MP and sets the cooldown after this
    // passes (single-threaded: no race).
    public static bool CanCast(CharaSlot chara, SkillData skill, int lv, float cdLeftMs) =>
        lv > 0 && chara.Mp >= skill.Mp && cdLeftMs <= 0;

    // One mob touch: DEF% blocks its share (min 1 when damage lands), HP
    // floors at 0. Returns the damage dealt (the client shows it; Hp 0 means dead).
    public static long ApplyHurt(CharaSlot chara, long damage, int defPct)
    {
        if (damage <= 0) return 0; // a Touch-0 mob is harmless, not chip damage
        long dealt = Math.Max(1, damage * (100 - defPct) / 100);
        chara.Hp = Math.Max(0, chara.Hp - dealt);
        return dealt;
    }
}

// The attacker's combat stats. Str/Dex are the final main/sub values;
// WeaponAtk is the final total attack. FinalPct is the Final Attack proc
// chance (not final damage; the chain has its own FinalDamagePct).
public readonly record struct Build(
    long Str,
    long Dex,
    long WeaponAtk,
    int DmgPct,
    int CritPct,
    int FinalPct,
    int BossPct = 0,
    int NormalPct = 0,
    int FinalDamagePct = 0,
    int CritDamage = 0,
    int IedPct = 0,
    int Level = 1,
    int WeaponType = 0);
