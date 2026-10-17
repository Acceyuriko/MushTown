using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Bag panels: inventory, stat, skill.
public partial class MushTownGame
{
    private const int TabCount = 6;
    private const int SkillW = 320; // skill window: tabs + SP line + 8 rows
    private const int SkillRowH = 52;
    private const int SkillRowsY = 48; // rows start below the tab strip + SP line
    private const int SkillBodyH = SkillRowsY + 8 * SkillRowH + 8; // strip + SP + rows + bottom pad

    // Skill window tabs (one per job) + rows: skill id + fixed cast key
    // (null = passive, no key; rebinds wait for the settings week).
    private static readonly string[] SkillTabNames = { "Warrior", "Fighter", "Crusader" };
    private static readonly (int id, string? key)[][] SkillTabs = new[]
    {
        new (int id, string? key)[]
        {
            (SkillData.SlashBlast, "Q"), (SkillData.IronBody, "R"), (SkillData.WarLeap, "W"),
            (SkillData.WarriorMastery, null), (SkillData.LeapAttack, "E"), (SkillData.GuardianArmor, null),
        },
        new (int id, string? key)[]
        {
            (SkillData.Brandish, "T"), (SkillData.WeaponMastery, null), (SkillData.SpiritBlade, "U"),
            (SkillData.ComboAttack, "F"), (SkillData.FlashBlade, "Y"), (SkillData.AgileArms, null),
            (SkillData.FinalAttack, null), (SkillData.PhysicalTraining, null),
        },
        new (int id, string? key)[]
        {
            (SkillData.IntrepidSlash, "G"), (SkillData.BeamBlade, "H"), (SkillData.Rush, "J"),
            (SkillData.ScarringSword, "L"), (SkillData.ComboSynergy, null), (SkillData.SelfRecovery, null),
            (SkillData.ChanceAttack, null), (SkillData.Endure, null),
        },
    };
    // One-line passive blurbs (per skill level): the window's only help text.
    private static readonly Dictionary<int, string> PassiveBlurb = new()
    {
        [SkillData.WarriorMastery] = "+HP/speed",
        [SkillData.GuardianArmor] = "+attack/DEF",
        [SkillData.WeaponMastery] = "+damage/crit",
        [SkillData.AgileArms] = "+STR",
        [SkillData.FinalAttack] = "bonus hit",
        [SkillData.PhysicalTraining] = "+STR/DEX",
        [SkillData.ComboSynergy] = "+orb gain/dmg",
        [SkillData.SelfRecovery] = "HP/MP regen",
        [SkillData.ChanceAttack] = "+crit",
        [SkillData.Endure] = "+Max HP/DEF",
    };

    private Rectangle InvRect() => new(_invPos.X, _invPos.Y, 242, 593);
    private Rectangle StatRect() => new(_statPos.X, _statPos.Y, 448, 422); // title 16 + body 406
    private Rectangle SkillRect() => new(_skillPos.X, _skillPos.Y, SkillW, 16 + SkillBodyH); // title 16 + tabs + SP + rows
    private Rectangle InvTitleRect() => new(_invPos.X, _invPos.Y, 242, 28);
    private Rectangle StatTitleRect() => new(_statPos.X, _statPos.Y, 448, 16);
    private Rectangle SkillTitleRect() => new(_skillPos.X, _skillPos.Y, SkillW, 16);

    private GameObject PanelSlot(Rectangle r, string itemKind, int index)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        var s = new Slot { ItemKind = itemKind, Index = index };
        s.OnClick = () => ClickSlot(itemKind, index);
        o.Add(s);
        return o;
    }

    private void BuildInvScene()
    {
        _invScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(InvRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new Picture("ui/hud_inv.png"));
        if (_invTab == 0)
            body.Add(new LiveLabel(() => _save?.Slots[_slot] != null ? "worn" : "", 14, 78, new Color(255, 220, 0)));
        if (_invTab == 1)
            for (int s = 0; s < _potions.Count; s++)
            {
                int i = s; // for-loop variables capture shared: copy per slot
                int id = _potions[i];
                var slot = PanelSlot(UseSlotRect(i), "use", i);
                slot.Add(new InvSlot(_items[id].Image, () => _save?.Slots[_slot]?.Use?.GetValueOrDefault(id) ?? 0, false));
                body.AddChild(slot);
            }
        if (_invTab == 0)
            for (int s = 0; s < _equips.Count; s++)
            {
                int i = s; // for-loop variables capture shared: copy per slot
                int id = _equips[i];
                var worn = PanelSlot(WornRect(i), "worn", i);
                worn.Add(new InvSlot(_items[id].Image, () => _save?.Slots[_slot]?.Equips?.Count(w => w == id) ?? 0, true));
                body.AddChild(worn);
                var bag = PanelSlot(EquipBagRect(i), "bag", i);
                bag.Add(new InvSlot(_items[id].Image, () => _save?.Slots[_slot]?.EquipBag?.GetValueOrDefault(id) ?? 0, false));
                body.AddChild(bag);
            }
        for (int t = 0; t < TabCount; t++)
        {
            int i = t; // for-loop variables capture shared: copy per tab
            var tab = PanelButton(TabRect(i), "invtab" + i, () => _invTab = i, ButtonKind.Click);
            tab.Add(new Picture($"ui/hud_itab_{(i == _invTab ? "s" : "n")}{i}.png", 0, 0));
            body.AddChild(tab);
        }
        body.AddChild(PanelButton(InvTitleRect(), "", () => _dragInv = _invScene.LastClick - _invPos)); // silent, like legacy
        var x = PanelButton(InvXRect(), "x0", () => _showInv = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        var coin = new GameObject { NoHit = true }; // purse draws last (bag rows can reach it when the roster grows)
        coin.Add(new Rect(InvRect()));
        coin.Add(new Picture("ui/hud_coin.png", Layout.Coin.X, Layout.Coin.Y));
        coin.Add(new RightLabel((ui, s) => ui.GetMeso(s),
            () => (_save?.Slots[_slot]?.Mesos ?? 0).ToString("#,##0"), Layout.Meso.X, Layout.Meso.Y, new Color(96, 104, 113)));
        body.AddChild(coin);
        _invScene.Add(body);
    }

    private void BuildStatScene()
    {
        _statScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(StatRect())); // stat shows numbers only: title + X, no content buttons
        body.Add(new TitleBar("STAT"));
        body.Add(new Picture(_statBack, 0, 16));
        body.Add(new Picture("ui/hud_stat.png", 0, 16));
        body.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => "0", 432, 24, Color.White)); // value only; "Combat Power" is baked in the art
        int by = 16; // body-relative (legacy by = r.Y + 16)
        int y = by + 132; // panel 1: vitals + 4 stats (name/level live on the plate)
        body.Add(new StatRow("HP", () => { var c = _save?.Slots[_slot]; int max = Progression.BuffedMaxHp(c?.Level ?? 1, OwnHpPct()); return $"{c?.Hp ?? max} / {max}"; }, y)); y += 22;
        body.Add(new StatRow("MP", () => { var c = _save?.Slots[_slot]; int max = Progression.MaxMp(c?.Level ?? 1); return $"{c?.Mp ?? max} / {max}"; }, y)); y += 22;
        body.Add(new StatRow("STR", () => $"{OwnBuild().Str}", y)); y += 22;
        body.Add(new StatRow("DEX", () => $"{OwnBuild().Dex}", y)); y += 22;
        body.Add(new StatRow("INT", () => $"{_save?.Slots[_slot]?.Int ?? 4}", y)); y += 22;
        body.Add(new StatRow("LUK", () => $"{_save?.Slots[_slot]?.Luk ?? 4}", y));
        y = by + 320; // panel 2 is only ~80px: 5 tight rows (official rows are tight too)
        body.Add(new StatRow("Attack Power", () => { var b = OwnBuild(); var (lo, hi) = Progression.PanelRange(b.Str, b.Dex, b.WeaponAtk); return $"{lo}~{hi}"; }, y)); y += 16;
        body.Add(new StatRow("Magic ATT", () => "0", y)); y += 16;
        body.Add(new StatRow("Defense", () => $"{OwnDef()}", y)); y += 16;
        body.Add(new StatRow("Speed", () => "100", y)); y += 16;
        body.Add(new StatRow("Jump", () => "100", y));
        body.AddChild(PanelButton(StatTitleRect(), "", () => _dragStat = _statScene.LastClick - _statPos));
        var x = PanelButton(StatXRect(), "x1", () => _showStat = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        _statScene.Add(body);
    }

    private void BuildSkillScene()
    {
        _skillScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(SkillRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("SKILL"));
        body.Add(new Picture(_skillBack, 0, 16));
        body.Add(new LiveLabel(() => { int sp = _save?.Slots[_slot]?.Sp ?? 0; return sp > 0 ? $"SP: {sp} (click + to learn)" : $"SP: {sp}"; }, 16, 42, Color.White));
        var tab = SkillTabs[_skillTab];
        for (int row = 0; row < tab.Length; row++)
        {
            int i = row; // for-loop variables capture shared: copy per row
            body.AddChild(PanelButton(SkillPlusRect(i), "skillplus" + i, () => { if (LearnSkill(tab[i].id)) PushBuild(); }, ButtonKind.Click));
        }
        for (int t = 0; t < SkillTabs.Length; t++)
        {
            int i = t; // for-loop variables capture shared: copy per tab
            var tb = PanelButton(SkillTabRect(i), "skilltab" + i, () => _skillTab = i, ButtonKind.Click);
            tb.Add(new Label(SkillTabNames[i], 8, 2) { Tint = i == _skillTab ? new Color(255, 220, 0) : new Color(130, 140, 150) });
            body.AddChild(tb);
        }
        body.AddChild(PanelButton(SkillTitleRect(), "", () => _dragSkill = _skillScene.LastClick - _skillPos)); // silent, like legacy
        var x = PanelButton(SkillXRect(), "x2", () => _showSkill = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        var r = SkillRect();
        for (int row = 0; row < tab.Length; row++)
        {
            int i = row; // for-loop variables capture shared: copy per row
            int id = tab[i].id;
            var holder = new GameObject { NoHit = true };
            holder.Add(new Rect(new Rectangle(r.X, r.Y + 16 + SkillRowsY + i * SkillRowH, SkillW, SkillRowH)));
            holder.Add(new SkillRow(_skills[id], tab[i].key, PassiveBlurb.TryGetValue(id, out string? b) ? b : "", () =>
            {
                var chara = _save?.Slots[_slot];
                int lv = 0;
                chara?.SkillLv.TryGetValue(id, out lv);
                _fighter.Cooldowns.TryGetValue(id, out float cd);
                _fighter.Buffs.TryGetValue(id, out float buff);
                return new SkillLive(lv, chara?.Sp ?? 0, chara?.Level ?? 1, cd, buff, _fighter.Orbs);
            }));
            body.AddChild(holder);
        }
        _skillScene.Add(body);
    }

    // Official tabs are fixed 35x22 art at pitch 37 (tab:category lt/pitch).
    private Rectangle TabRect(int t) =>
        new(InvRect().X + 11 + 37 * t, InvRect().Y + 31, 35, 22);

    // USE tab potion cells: aligned to the first inset row of the art grid
    // (local y 95..143, measured off the USE shot; x pitch 56).
    private Rectangle UseSlotRect(int i)
    {
        var r = InvRect();
        return new Rectangle(r.X + 14 + i * 56, r.Y + 95, 48, 48);
    }

    // EQUIP tab worn cells: the gold row, wraps 4-across like the bag.
    private Rectangle WornRect(int i)
    {
        var s = UseSlotRect(i % 4);
        return new Rectangle(s.X, s.Y + 56 * (i / 4), 48, 48);
    }

    // EQUIP tab bag cells: rows below the worn rows, 4 across (same 48px grid).
    private Rectangle EquipBagRect(int i)
    {
        var s = UseSlotRect(i % 4);
        return new Rectangle(s.X, s.Y + 56 * ((_equips.Count + 3) / 4 + i / 4), 48, 48);
    }

    // One inventory slot click, through the hand (pick, drop, wear,
    // swap, double-click drink). Conditional sounds stay explicit here.
    private void ClickSlot(string kind, int index)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no bag
        int id = kind == "use" ? _potions[index] : _equips[index];
        var act = _hand.Click(chara, kind, id, _items, OwnHpPct(), _timeMs);
        if (act == HandAct.Wore) { PushBuild(); PushName(); PlaySfx("click"); }
        else if (act == HandAct.Moved) PlaySfx("click");
    }

    // --wear poses the demo doll: wear one through the shared rule. True when worn.
    private bool Wear(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false; // CLI sessions carry no bag
        return Progression.WearEquip(chara, id, _items);
    }

    // Skill window job tabs (3 x 100px under the title).
    private Rectangle SkillTabRect(int t)
    {
        var r = SkillRect();
        return new Rectangle(r.X + 8 + t * 104, r.Y + 16 + 4, 100, 18);
    }

    // [+] zone: right end of a skill row (spends one SP on click).
    private Rectangle SkillPlusRect(int i)
    {
        var r = SkillRect();
        return new Rectangle(r.X + SkillW - 44, r.Y + 16 + SkillRowsY + i * SkillRowH + 4, 36, 26);
    }

    // Click a [+]: learn one level. CLI sessions carry no character.
    private bool LearnSkill(int id)
    {
        var chara = _save?.Slots[_slot];
        return chara != null && Progression.LearnSkill(chara, _skills[id]);
    }

    private Rectangle InvXRect()
    {
        var r = InvRect();
        return new Rectangle(r.X + r.Width - 11 - 6, r.Y + 6, 11, 11);
    }

    private Rectangle StatXRect()
    {
        var r = StatRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private Rectangle SkillXRect()
    {
        var r = SkillRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }
}
