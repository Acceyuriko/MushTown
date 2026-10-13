namespace MushTown.Shared;

// Leveling, vitals, and the attack panel: pure math shared by the client
// (EXP bar, stat panel, potions) and the server (damage judging).
// W10 plays a fixed warrior line: auto stat points on level-up (M4).
public static class Progression
{
    // Beginner polearm 01442000, its WZ incPAD (real attack arrives with W17 equips).
    public const int WeaponAtk = 32;

    // EXP to climb from this level to the next. Linear v1 curve: levels 1-10
    // cost 1035 total, and the 23 killable mobs pay 1225, so grinding both
    // maps always outruns the cap (until W13 respawn).
    public static int ExpNext(int level) => 20 * level + 15;

    // Vitals grow per level; a level-up refills both pools (official does too).
    public static int MaxHp(int level) => 50 + 30 * (level - 1);
    public static int MaxMp(int level) => 5 + 5 * (level - 1);

    // Polearm weapon multiplier. bwiki's weapon table page is gone (template
    // 404), so 4.6 is a v1 guess, kept visible here until verified.
    private const double PolearmMult = 4.6;

    // Melee base mastery: bwiki 熟练度 floor 20% (no beginner skill raises it).
    private const double MeleeMastery = 0.20;

    // The bwiki panel range (实际面板): warrior STR main, DEX sub. Damage.Calc
    // rolls inside this range; the stat panel shows it.
    public static (int min, int max) PanelRange(int str, int dex, int weaponAtk)
    {
        int max = (int)(PolearmMult * (str * 4 + dex) * weaponAtk / 100);
        return ((int)(max * MeleeMastery), max);
    }

    // Combat stats the client sends the server (Build message) so damage is
    // judged on the real attacker. Beginner is the default for clients that
    // never sent one (CLI sessions).
    public static readonly Build Beginner = new(4, 4, WeaponAtk);

    // Adds EXP, levels up while the bar is full. Each level: auto stat points
    // (+4 STR +1 DEX warrior ratio), pools refilled to the new max. Returns
    // levels gained (0 when the bar is not full yet).
    public static int ApplyExp(CharaSlot chara, int exp)
    {
        chara.Exp += exp;
        int ups = 0;
        while (chara.Exp >= ExpNext(chara.Level))
        {
            chara.Exp -= ExpNext(chara.Level);
            chara.Level++;
            chara.Str += 4;
            chara.Dex += 1;
            chara.Hp = MaxHp(chara.Level);
            chara.Mp = MaxMp(chara.Level);
            ups++;
        }
        return ups;
    }

    // Drinks one potion: needs a bottle in the bag and a pool that is not
    // full. Returns false when nothing happens (empty or already full).
    public static bool UsePotion(CharaSlot chara, int itemId, int healHp, int healMp)
    {
        var use = chara.Use;
        if (use == null || !use.TryGetValue(itemId, out int count) || count <= 0)
            return false;
        bool need = (healHp > 0 && chara.Hp < MaxHp(chara.Level))
            || (healMp > 0 && chara.Mp < MaxMp(chara.Level));
        if (!need) return false;
        if (count == 1) use.Remove(itemId);
        else use[itemId] = count - 1;
        chara.Hp = Math.Min(chara.Hp + healHp, MaxHp(chara.Level));
        chara.Mp = Math.Min(chara.Mp + healMp, MaxMp(chara.Level));
        return true;
    }

    // One cast's checks: level, MP, cooldown. The client deducts MP and
    // sets the cooldown after this passes (single-threaded: no race).
    public static bool CanCast(CharaSlot chara, SkillData skill, float cdLeftMs) =>
        chara.Level >= skill.ReqLevel && chara.Mp >= skill.Mp && cdLeftMs <= 0;
}

// The attacker's combat stats (warrior line: STR/DEX + weapon). Int/Luk stay
// client-side: nothing consumes them until later milestones.
public readonly record struct Build(int Str, int Dex, int WeaponAtk);
