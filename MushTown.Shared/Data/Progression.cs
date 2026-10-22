namespace MushTown.Shared;

// Leveling, vitals, and the attack panel: pure math shared by the client
// (EXP bar, stat panel, potions) and the server (damage judging).
// W10 plays a fixed warrior line: auto stat points on level-up (M4).
public static class Progression
{
    // Starting weapon attack. v1 placeholder: 32 keeps the damage range the
    // tests and the early mobs are tuned for (5..29). The one-handed Sword
    // 01302000 has WZ incPAD 15; the real attack number lands with W17.
    public const int WeaponAtk = 32;

    // EXP to climb from this level to the next. Linear v1 curve: levels 1-10
    // cost 1035 total, and one training center pays about that much per
    // clear, so grinding the training mobs always outruns the cap.
    public static int ExpNext(int level) => 20 * level + 15;

    // Vitals grow per level; a level-up refills both pools (official does too).
    public static int MaxHp(int level) => 50 + 30 * (level - 1);
    public static int MaxMp(int level) => 5 + 5 * (level - 1);

    // Melee weapon multiplier. bwiki's weapon table page is gone (template
    // 404), so 4.6 is a v1 guess, kept visible here until verified.
    private const double WeaponMult = 4.6;

    // Melee base mastery: bwiki 熟练度 floor 20% (no beginner skill raises it).
    private const double MeleeMastery = 0.20;

    // The bwiki panel range (实际面板): warrior STR main, DEX sub. Damage.Calc
    // rolls inside this range; the stat panel shows it.
    public static (int min, int max) PanelRange(int str, int dex, int weaponAtk)
    {
        int max = (int)(WeaponMult * (str * 4 + dex) * weaponAtk / 100);
        return ((int)(max * MeleeMastery), max);
    }

    // Combat stats the client sends the server (Build message) so damage is
    // judged on the real attacker. Beginner is the default for clients that
    // never sent one (CLI sessions). DmgPct/CritPct/FinalPct fold every
    // skill source client-side (passives, buffs, orbs stay in SkillCast).
    public static readonly Build Beginner = new(4, 4, WeaponAtk, 0, 0, 0);

    // Skill points banked per level-up (creation banks one level too).
    public const int SpPerLevel = 3;

    // Max-HP pool with the +MaxHP% of learned skills (Iron Body, Warrior
    // Mastery, Endure) folded in. MP has no buff source in v1.
    public static int BuffedMaxHp(int level, int hpPct) => MaxHp(level) * (100 + hpPct) / 100;

    // Active damage pct at a skill level: lv 1 deals DmgLo, max deals DmgHi,
    // linear between. Single-level skills (max 1) always deal DmgLo.
    public static int SkillDamage(SkillData data, int lv) =>
        data.MaxLevel <= 1 ? data.DmgLo : data.DmgLo + (data.DmgHi - data.DmgLo) * (lv - 1) / (data.MaxLevel - 1);

    // The SP cost of one skill level.
    public const int SkillSpCost = 1;

    // Can the character raise this skill right now? The rule LearnSkill uses:
    // the character level, an unspent point and headroom under max.
    public static bool CanLearn(CharaSlot chara, SkillData data)
    {
        chara.SkillLv.TryGetValue(data.SkillId, out int lv);
        return chara.Level >= data.ReqLevel && chara.Sp >= SkillSpCost && lv < data.MaxLevel;
    }

    // Spends one SP to raise a skill: needs the character level, an unspent
    // point, and headroom under max. Returns false when nothing happens.
    public static bool LearnSkill(CharaSlot chara, SkillData data)
    {
        if (!CanLearn(chara, data)) return false;
        chara.SkillLv.TryGetValue(data.SkillId, out int lv);
        chara.SkillLv[data.SkillId] = lv + 1;
        chara.Sp -= SkillSpCost;
        return true;
    }

    // Raises the skill as far as the SP go (the MAX button). Returns the
    // levels bought: 0 when nothing happens (no SP, too young, already maxed).
    public static int LearnSkillMax(CharaSlot chara, SkillData data)
    {
        int bought = 0;
        while (LearnSkill(chara, data)) bought++;
        return bought;
    }

    // Adds EXP, levels up while the bar is full. Each level: auto stat points
    // (+4 STR +1 DEX warrior ratio), +3 SP, pools refilled to the new (buffed)
    // max. Returns levels gained (0 when the bar is not full yet).
    public static int ApplyExp(CharaSlot chara, int exp, int hpPct = 0)
    {
        chara.Exp += exp;
        int ups = 0;
        while (chara.Exp >= ExpNext(chara.Level))
        {
            chara.Exp -= ExpNext(chara.Level);
            chara.Level++;
            chara.Str += 4;
            chara.Dex += 1;
            chara.Sp += SpPerLevel;
            chara.Hp = BuffedMaxHp(chara.Level, hpPct);
            chara.Mp = MaxMp(chara.Level);
            ups++;
        }
        return ups;
    }

    // Drinks one potion: needs a bottle in the bag and a pool that is not
    // full. Returns false when nothing happens (empty or already full).
    public static bool UsePotion(CharaSlot chara, int itemId, int healHp, int healMp, int hpPct = 0)
    {
        var use = chara.Use;
        if (use == null || !use.TryGetValue(itemId, out int count) || count <= 0)
            return false;
        bool need = (healHp > 0 && chara.Hp < BuffedMaxHp(chara.Level, hpPct))
            || (healMp > 0 && chara.Mp < MaxMp(chara.Level));
        if (!need) return false;
        if (count == 1) use.Remove(itemId);
        else use[itemId] = count - 1;
        chara.Hp = Math.Min(chara.Hp + healHp, BuffedMaxHp(chara.Level, hpPct));
        chara.Mp = Math.Min(chara.Mp + healMp, MaxMp(chara.Level));
        return true;
    }

    // Wears one from the EQUIP bag (the same-slot piece swaps out, so a
    // slot never holds two). Empty slots do nothing. True when worn.
    public static bool WearEquip(CharaSlot chara, int id, IReadOnlyDictionary<int, ItemData> defs)
    {
        var def = defs[id]; // callers only pass loaded equips (client boot proves their slots)
        if (chara.EquipBag == null || !chara.EquipBag.TryGetValue(id, out int have) || have <= 0) return false;
        chara.EquipBag[id] = have - 1;
        if (chara.EquipBag[id] == 0) chara.EquipBag.Remove(id);
        chara.Equips ??= new List<int>();
        int swap = chara.Equips.FindIndex(w => defs.TryGetValue(w, out var wd) && wd.Slot == def.Slot);
        if (swap >= 0)
        {
            int old = chara.Equips[swap];
            chara.Equips.RemoveAt(swap);
            chara.EquipBag[old] = chara.EquipBag.GetValueOrDefault(old) + 1;
        }
        chara.Equips.Add(id);
        return true;
    }

    // Takes one off, back to the bag. Unworn slots do nothing. A hand-made
    // save may wear unknown ids (they draw and grant nothing); the tab shows
    // no cell for them, so they sit stuck but harmless.
    public static bool TakeOffEquip(CharaSlot chara, int id)
    {
        if (chara.Equips == null || !chara.Equips.Remove(id)) return false;
        chara.EquipBag ??= new Dictionary<int, int>();
        chara.EquipBag[id] = chara.EquipBag.GetValueOrDefault(id) + 1;
        return true;
    }

    // One cast's checks: learned, level, MP, cooldown. The client deducts MP
    // and sets the cooldown after this passes (single-threaded: no race).
    public static bool CanCast(CharaSlot chara, SkillData skill, int lv, float cdLeftMs) =>
        lv > 0 && chara.Level >= skill.ReqLevel && chara.Mp >= skill.Mp && cdLeftMs <= 0;

    // One mob touch: DEF% blocks its share (min 1 when damage lands), HP
    // floors at 0. Returns the damage dealt (the client shows it; Hp 0 means dead).
    public static int ApplyHurt(CharaSlot chara, int damage, int defPct)
    {
        if (damage <= 0) return 0; // a Touch-0 mob is harmless, not chip damage
        int dealt = Math.Max(1, damage * (100 - defPct) / 100);
        chara.Hp = Math.Max(0, chara.Hp - dealt);
        return dealt;
    }
}

// The attacker's combat stats (warrior line: STR/DEX + weapon). Int/Luk stay
// client-side: nothing consumes them until later milestones. The last three
// fold skill sources: +damage% (Weapon Mastery), crit% (Weapon/Chance),
// Final Attack proc% — the client recomputes them on every learn/buff tick.
public readonly record struct Build(int Str, int Dex, int WeaponAtk, int DmgPct, int CritPct, int FinalPct);
