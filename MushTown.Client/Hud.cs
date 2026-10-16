using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// W9 bottom bar + item/stat panels. Art is official (StatusBar3, UIInventory,
// UICharacterInfo); all text baked. Plate/slot positions are WZ vectors;
// the rest is tuned rects. HP/MP numbers and EXP are live (W10 leveling);
// nothing drains HP until W13 mob hits, so the bars always draw full.
public partial class MushTownGame
{
    // Must match Export.cs ExportHud: every name here must be exported, or LoadTexture throws.
    private static readonly string[] HudFiles = new[]
    {
        "hud_plate.png", "hud_lvtag.png",
        "hud_hpbg.png", "hud_hpcover.png", "hud_hpfill.png", "hud_mpfill.png",
        "hud_expback.png", "hud_expcover.png", "hud_expgauge.png",
        "hud_btn_char_n.png", "hud_btn_char_h.png", "hud_btn_char_p.png",
        "hud_btn_menu_n.png", "hud_btn_menu_h.png", "hud_btn_menu_p.png",
        "hud_sub_char_n.png", "hud_sub_char_h.png", "hud_sub_char_p.png",
        "hud_sub_item_n.png", "hud_sub_item_h.png", "hud_sub_item_p.png",
        "hud_sub_bg0.png", "hud_sub_bg1.png", "hud_sub_bg2.png",
        "hud_qslot.png", "hud_qslotcover.png",
        "hud_key_ctrl.png", "hud_key_alt.png", "hud_key_i.png", "hud_key_c.png",
        "hud_inv.png",
        "hud_itab_s0.png", "hud_itab_s1.png", "hud_itab_s2.png",
        "hud_itab_s3.png", "hud_itab_s4.png", "hud_itab_s5.png",
        "hud_itab_n0.png", "hud_itab_n1.png", "hud_itab_n2.png",
        "hud_itab_n3.png", "hud_itab_n4.png", "hud_itab_n5.png",
        "hud_coin.png", "hud_stat.png", "hud_stathilong.png", "hud_statx.png",
    };
    private const int TabCount = 6;
    private const int SkillW = 320; // skill window: tabs + SP line + 8 rows
    private const int SkillRowH = 52;
    private const int SkillRowsY = 48; // rows start below the tab strip + SP line
    private const int SkillBodyH = SkillRowsY + 8 * SkillRowH + 8; // strip + SP + rows + bottom pad
    private const int QuestW = 320; // quest window: accepted list + detail
    private const int QuestRowH = 22;
    private const int QuestListH = 5 * QuestRowH; // 5 rows, no scroll in v1
    private const int QuestBodyH = QuestListH + 190; // list + detail block
    private const int SettingsW = 260; // settings window: 2 volume rows
    private const int SettingsRowH = 38;
    private const int SettingsBodyH = 10 + 2 * SettingsRowH + 10; // top pad + rows + bottom pad
    private const int ShopW = 340; // shop window: buy rows + sell rows (height follows the bag)
    private const int ShopRowH = 40;
    private const int TalkW = 440; // talk panel: body + quest entries + buttons
    private const int TalkBtnW = 90, TalkBtnH = 24;

    private readonly Dictionary<int, ItemData> _items = new();
    private ManifestData _manifest = null!;

    // Scanned tab rosters (LoadItemDefs): consumables slot USE, equips slot
    // EQUIP, both in id order. Clicks and draws share them, so cell i is
    // always the same item on both paths.
    private List<int> _potions = new();
    private List<int> _equips = new();

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
    private readonly Dictionary<int, SkillData> _skills = new();
    private SkillCost _skillCost = null!; // live casts over _skills (same dict ref)
    private SkillDamage _skillDmg = null!;
    private int _skillTab; // 0 Warrior, 1 Fighter, 2 Crusader
    private bool _showInv, _showStat, _showSkill, _showQuest, _showSettings, _showShop;
    private readonly SceneManager _scenes = new();
    private Scene _gameScene = null!; // W1: the submenu only; later weeks add the rest
    private Scene _invScene = new(), _statScene = new(), _skillScene = new();
    private readonly CarryHand _hand = new(); // game-level: panel rebuilds swap scenes, the held piece survives
    private Scene _questScene = new(), _settingsScene = new(), _shopScene = new(), _talkScene = new();
    private Point _invBuiltPos = new(-1, -1), _statBuiltPos = new(-1, -1), _skillBuiltPos = new(-1, -1);
    private Point _questBuiltPos = new(-1, -1), _settingsBuiltPos = new(-1, -1), _shopBuiltPos = new(-1, -1);
    private int _invBuiltTab = -1, _skillBuiltTab = -1; // baked Rects follow drags + tab flips (bag counts are static)
    private List<int> _questBuiltRows = new();
    private List<int> _shopBuiltStock = new();
    private List<(int id, int count)> _shopBuiltSells = new();
    private int _talkBuiltNpc, _talkBuiltQuest, _talkBuiltPage;
    private string _talkBuiltButtons = "", _talkBuiltEntries = "";
    private GameObject _submenu = null!; // root; Enabled is the open flag
    private UiGfx _gfx = null!;
    private bool _contentLoaded; // the scene build needs textures (popup height reads them)
    private readonly Dictionary<int, ShopData> _shops = new(); // keeper stocks (LoadShops)
    private List<int> _shopStock = null!; // the open shop's stock (OpenShop snapshots it: talk state clears)
    private int _invTab; // 0 Equip 1 Use 2 Etc 3 Set-up 4 Cash 5 Dec
    private bool _iWasDown, _cWasDown, _kWasDown, _vWasDown;

    private static Rectangle PlateRect() => new(Layout.Plate.X, Layout.Plate.Y, 250, 35);
    private static Rectangle HpRect() => new(Layout.HpMp.X, Layout.HpMp.Y, 205, 46); // cover bottom flush with EXP top
    private static Rectangle MenuBtnRect() => new(Layout.MenuBtn.X, Layout.MenuBtn.Y, 34, 37);
    private static Rectangle CharBtnRect() => new(Layout.CharBtn.X, Layout.CharBtn.Y, 34, 37);
    private static Rectangle QslotRect() => new(Layout.Qslot.X, Layout.Qslot.Y, 557, 67);
    private static Rectangle ExpRect() => new(0, 758, 1366, 10);
    private Point _invPos = new(Layout.Inv.X, Layout.Inv.Y), _statPos = new(Layout.Stat.X, Layout.Stat.Y);
    private Point _skillPos = new(Layout.Skill.X, Layout.Skill.Y);
    private Point? _dragInv, _dragStat, _dragSkill, _dragQuest, _dragSettings, _dragShop; // title grab offsets while held
    private int _topPanel = 1; // the front panel: 0 = inventory, 1 = stat, 2 = skill, 3 = quest, 4 = settings, 5 = shop
    private Texture2D _statBack = null!; // navy body backing, corners rounded to match the art
    private Texture2D _skillBack = null!; // same navy, skill window size
    private Texture2D _questBack = null!; // same navy, quest window size
    private Texture2D _settingsBack = null!; // same navy, settings window size

    private Rectangle InvRect() => new(_invPos.X, _invPos.Y, 242, 593);
    private Rectangle StatRect() => new(_statPos.X, _statPos.Y, 448, 422); // title 16 + body 406
    private Rectangle SkillRect() => new(_skillPos.X, _skillPos.Y, SkillW, 16 + SkillBodyH); // title 16 + tabs + SP + rows
    private Rectangle InvTitleRect() => new(_invPos.X, _invPos.Y, 242, 28);
    private Rectangle StatTitleRect() => new(_statPos.X, _statPos.Y, 448, 16);
    private Rectangle SkillTitleRect() => new(_skillPos.X, _skillPos.Y, SkillW, 16);

    private Texture2D Tex(string file) => _textures["ui/" + file];

    private void UpdateHud(KeyboardState kb, bool click, bool held, int gx, int gy)
    {
        UpdateHudHover(gx, gy); // every frame (clicks return early below)
        bool i = kb.IsKeyDown(Keys.I);
        if (i && !_iWasDown && _talkNpc == 0) { _showInv = !_showInv; _dragInv = null; if (_showInv) _topPanel = 0; PlaySfx("click"); } // hiding mid-drag drops the grab
        _iWasDown = i;
        bool c = kb.IsKeyDown(Keys.C);
        if (c && !_cWasDown && _talkNpc == 0) { _showStat = !_showStat; _dragStat = null; if (_showStat) _topPanel = 1; PlaySfx("click"); } // hiding mid-drag drops the grab
        _cWasDown = c;
        bool k = kb.IsKeyDown(Keys.K);
        if (k && !_kWasDown && _talkNpc == 0) { _showSkill = !_showSkill; _dragSkill = null; if (_showSkill) _topPanel = 2; PlaySfx("click"); } // hiding mid-drag drops the grab
        _kWasDown = k;
        bool v = kb.IsKeyDown(Keys.V);
        if (v && !_vWasDown && _talkNpc == 0) { _showQuest = !_showQuest; _dragQuest = null; if (_showQuest) _topPanel = 3; PlaySfx("click"); } // hiding mid-drag drops the grab; the modal freezes panels
        _vWasDown = v;
        // Title drag runs on held (down starts it in the chain below).
        if (_dragInv.HasValue)
        {
            if (!held) _dragInv = null;
            else _invPos = ClampPanel(new Point(gx - _dragInv.Value.X, gy - _dragInv.Value.Y), 242);
        }
        if (_dragStat.HasValue)
        {
            if (!held) _dragStat = null;
            else _statPos = ClampPanel(new Point(gx - _dragStat.Value.X, gy - _dragStat.Value.Y), 448);
        }
        if (_dragSkill.HasValue)
        {
            if (!held) _dragSkill = null;
            else _skillPos = ClampPanel(new Point(gx - _dragSkill.Value.X, gy - _dragSkill.Value.Y), SkillW);
        }
        if (_dragQuest.HasValue)
        {
            if (!held) _dragQuest = null;
            else _questPos = ClampPanel(new Point(gx - _dragQuest.Value.X, gy - _dragQuest.Value.Y), QuestW);
        }
        if (_dragSettings.HasValue)
        {
            if (!held) _dragSettings = null;
            else _settingsPos = ClampPanel(new Point(gx - _dragSettings.Value.X, gy - _dragSettings.Value.Y), SettingsW);
        }
        if (_dragShop.HasValue)
        {
            if (!held) _dragShop = null;
            else _shopPos = ClampPanel(new Point(gx - _dragShop.Value.X, gy - _dragShop.Value.Y), ShopW);
        }
        SyncPanelScenes(); // drags + tab flips move the baked Rects (hover ran first, so it lags one frame, like legacy)
        if (!click) return;
        UpdateHudClick(gx, gy); // click sounds play by Button Kind inside ClickTree (the flag funnel is gone)
    }

    // Panels front-to-back (the click z: front first, the rest in place).
    private IEnumerable<(bool show, Scene scene)> PanelsFrontToBack()
    {
        var all = new (bool, Scene)[] { (_showInv, _invScene), (_showStat, _statScene), (_showSkill, _skillScene), (_showQuest, _questScene), (_showSettings, _settingsScene), (_showShop, _shopScene) };
        yield return all[_topPanel];
        for (int i = 0; i < 6; i++) if (i != _topPanel) yield return all[i];
    }

    // One "over" per HUD button entry (clicks live in UpdateHudClick below).
    // Hover respects overlap now (the W5 z-fix): the frontmost panel at the
    // point swallows below, so covered buttons stay silent and dark. Where
    // nothing overlaps, the ids match the legacy chain exactly.
    private void UpdateHudHover(int gx, int gy)
    {
        _hoverItem = false;
        if (_talkNpc != 0) { _gameScene.ClearHover(); SfxOnHover(_talkScene.Hover(gx, gy)); return; }
        foreach (var (show, scene) in PanelsFrontToBack())
        {
            if (!show) continue;
            string id = scene.Hover(gx, gy);
            if (scene.Hovered != null)
            {
                _gameScene.ClearHover();
                _hoverItem = scene == _invScene && scene.HoverButton is Slot;
                SfxOnHover(id);
                return;
            }
        }
        SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? ""); // menu/char/submenu/furniture all live in the tree now
    }

    private void UpdateHudClick(int gx, int gy)
    {
        if (_talkNpc != 0) { ClickTree(_talkScene, gx, gy); return; } // modal: talk swallows every click
        bool onInv = _showInv && HitRect(InvRect(), gx, gy);
        bool onStat = _showStat && HitRect(StatRect(), gx, gy);
        bool onSkill = _showSkill && HitRect(SkillRect(), gx, gy);
        bool onQuest = _showQuest && HitRect(QuestRect(), gx, gy);
        bool onSettings = _showSettings && HitRect(SettingsRect(), gx, gy);
        bool onShop = _showShop && HitRect(ShopRect(), gx, gy);
        // Overlap: the frontmost panel owns the click. The front panel wins
        // every tie; the back five keep inv < stat < skill < quest < settings < shop order.
        // Pairwise elimination leaves the highest z standing, and the survivor
        // comes to front (a click brings its panel to front, even on overlap).
        int z(int p) => p == _topPanel ? 6 : p;
        if (onInv && onStat) { if (z(0) < z(1)) onInv = false; else onStat = false; }
        if (onInv && onSkill) { if (z(0) < z(2)) onInv = false; else onSkill = false; }
        if (onInv && onQuest) { if (z(0) < z(3)) onInv = false; else onQuest = false; }
        if (onStat && onSkill) { if (z(1) < z(2)) onStat = false; else onSkill = false; }
        if (onStat && onQuest) { if (z(1) < z(3)) onStat = false; else onQuest = false; }
        if (onSkill && onQuest) { if (z(2) < z(3)) onSkill = false; else onQuest = false; }
        if (onInv && onSettings) { if (z(0) < z(4)) onInv = false; else onSettings = false; }
        if (onStat && onSettings) { if (z(1) < z(4)) onStat = false; else onSettings = false; }
        if (onSkill && onSettings) { if (z(2) < z(4)) onSkill = false; else onSettings = false; }
        if (onQuest && onSettings) { if (z(3) < z(4)) onQuest = false; else onSettings = false; }
        if (onInv && onShop) { if (z(0) < z(5)) onInv = false; else onShop = false; }
        if (onStat && onShop) { if (z(1) < z(5)) onStat = false; else onShop = false; }
        if (onSkill && onShop) { if (z(2) < z(5)) onSkill = false; else onShop = false; }
        if (onQuest && onShop) { if (z(3) < z(5)) onQuest = false; else onShop = false; }
        if (onSettings && onShop) { if (z(4) < z(5)) onSettings = false; else onShop = false; }
        if (onInv) _topPanel = 0;
        else if (onStat) _topPanel = 1;
        else if (onSkill) _topPanel = 2;
        else if (onQuest) _topPanel = 3;
        else if (onSettings) _topPanel = 4;
        else if (onShop) _topPanel = 5;
        // Panels draw on top, so a panel click never reaches the bottom bar behind it.
        if (onInv || onStat || onSkill || onQuest || onSettings || onShop)
        {
            _submenu.Enabled = false;
            if (onInv) ClickTree(_invScene, gx, gy); // X > title > content, same order as the legacy chain
            else if (onStat) ClickTree(_statScene, gx, gy);
            else if (onSkill) ClickTree(_skillScene, gx, gy);
            else if (onQuest) ClickTree(_questScene, gx, gy);
            else if (onSettings) ClickTree(_settingsScene, gx, gy);
            else if (onShop) ClickTree(_shopScene, gx, gy);
            return;
        }
        if (ClickTree(_scenes.Current, gx, gy) != null) { } // menu/char/submenu/furniture all live in the tree now
        else if (_submenu.Enabled) _submenu.Enabled = false; // outside click closes (no panel behind to fall through to)
        else NpcClick(gx, gy); // no UI took it: the world gets the click
    }

    // Panels stay reachable: 80px on screen sideways, title never above the top.
    private static Point ClampPanel(Point p, int w) =>
        new(Math.Clamp(p.X, -w + 80, 1366 - 80), Math.Clamp(p.Y, 0, 768 - 40));

    private Texture2D TexByKey(string key) => _textures[key]; // full keys ("ui/..."): all screens share one lookup

    // W1: the game scene holds the submenu only; later weeks add the rest.
    // Built after textures load, rebuilt on layout reload (a rebuild
    // resets the submenu to closed, same as the panel positions above).
    private void BuildGameScene()
    {
        _gameScene = new Scene();
        var furniture = new GameObject(); // HUD furniture: hit-rects with no Buttons (clicks stop here, silent)
        foreach (var r in new[] { PlateRect(), HpRect(), QslotRect(), ExpRect() })
        {
            var f = new GameObject();
            f.Add(new Rect(r));
            furniture.AddChild(f);
        }
        _gameScene.Add(furniture); // first (bottom): menu/char cover its Exp strip
        var exp = new GameObject();
        exp.Add(new Rect(ExpRect()));
        exp.Add(new Bar("ui/hud_expback.png", "ui/hud_expgauge.png", "ui/hud_expcover.png", ExpFill));
        _gameScene.Add(exp); // legacy draws exp first: same pixels, owned by the tree now
        _submenu = new GameObject();
        _submenu.Add(new Rect(SubPopupRect()));
        _submenu.Add(new Panel("ui/hud_sub_bg0.png", "ui/hud_sub_bg1.png", "ui/hud_sub_bg2.png"));
        AddSubArtRow(0, "ui/hud_sub_char", () => { _showStat = true; _submenu.Enabled = false; _topPanel = 1; });
        AddSubArtRow(1, "ui/hud_sub_item", () => { _showInv = true; _submenu.Enabled = false; _topPanel = 0; });
        AddSubTextRow(2, "Quest", () => { _showQuest = true; _submenu.Enabled = false; _topPanel = 3; });
        AddSubTextRow(3, "Settings", () => { _showSettings = true; _submenu.Enabled = false; _topPanel = 4; });
        AddSubTextRow(4, "Log", () => { _panelOpen = !_panelOpen; _submenu.Enabled = false; });
        AddSubTextRow(5, "Exit", () => { _submenu.Enabled = false; if (_save != null) ToChars(); else Exit(); });
        _gameScene.Add(_submenu);
        var charBtn = new GameObject();
        charBtn.Add(new Rect(CharBtnRect()));
        charBtn.Add(new Image("ui/hud_btn_char")); // art rides the button now (was DrawHudButton); keep art a Component, never a child (Image matches Hovered == Owner)
        charBtn.Add(new Button { LegacyId = "char", Kind = ButtonKind.Click, OnClick = () => { _showStat = !_showStat; _submenu.Enabled = false; if (_showStat) _topPanel = 1; } });
        _gameScene.Add(charBtn);
        var menuBtn = new GameObject();
        menuBtn.Add(new Rect(MenuBtnRect()));
        menuBtn.Add(new Image("ui/hud_btn_menu")); // art rides the button now (was DrawHudButton)
        menuBtn.Add(new Button { LegacyId = "menu", Kind = ButtonKind.Click, OnClick = () => _submenu.Enabled = !_submenu.Enabled });
        _gameScene.Add(menuBtn); // topmost hit: legacy checks menu/char before the submenu and furniture (art roots added after are NoHit)
        var plate = new GameObject { NoHit = true }; // art-only roots below: the furniture keeps swallowing plate/HP/quickslot/exp; tracker/boss/minimap pass clicks through like before
        plate.Add(new Rect(PlateRect()));
        plate.Add(new Picture("ui/hud_plate.png"));
        plate.Add(new PlateLevel(() => _save?.Slots[_slot]?.Level ?? 1));
        plate.Add(new Picture("ui/hud_lvtag.png", 8, 13));
        plate.Add(new LiveLabel(() => _save?.Slots[_slot]?.Name ?? "", 67, 13, Color.White)); // WZ namePos; CLI sessions have no name (empty draws nothing)
        _gameScene.Add(plate);
        var hpmp = new GameObject { NoHit = true };
        hpmp.Add(new Rect(HpRect()));
        hpmp.Add(new Picture("ui/hud_hpbg.png"));
        hpmp.Add(new Picture("ui/hud_hpfill.png", 4, 4)); // fills at bg+(4,4)/(4,25), cover at bg+(3,3), numbers centered on x+101. Bars always full: nothing drains HP/MP until W13 mob hits (W13 adds the clip then).
        hpmp.Add(new Picture("ui/hud_mpfill.png", 4, 25));
        hpmp.Add(new BarNumber(HpText, 101, 7));
        hpmp.Add(new BarNumber(MpText, 101, 28));
        hpmp.Add(new Picture("ui/hud_hpcover.png", 3, 3));
        _gameScene.Add(hpmp);
        var qslot = new GameObject { NoHit = true };
        qslot.Add(new Rect(QslotRect()));
        qslot.Add(new Picture("ui/hud_qslot.png"));
        (string file, string action)[] keys = new[] { ("hud_key_ctrl.png", "Attack"), ("hud_key_alt.png", "Jump"), ("hud_key_i.png", "Items"), ("hud_key_c.png", "Stats") }; // fixed W9 hotkeys: official key art top-left (icons arrive with skills/potions)
        for (int i = 0; i < keys.Length; i++)
        {
            var s = QslotCell(i);
            var cell = new GameObject(); // NoHit rides the qslot parent (Hit skips the whole subtree)
            cell.Add(new Rect(s));
            cell.Add(new Picture("ui/" + keys[i].file, 2, 2));
            var a = _ui.GetSmall(keys[i].action);
            cell.Add(new Label(keys[i].action, (s.Width - a.Width) / 2, 2 + Tex(keys[i].file).Height) { Tint = Color.Gray });
            qslot.AddChild(cell);
        }
        var gloss = new GameObject(); // NoHit rides the qslot parent (Hit skips the whole subtree)
        gloss.Add(new Rect(QslotRect()));
        gloss.Add(new Picture("ui/hud_qslotcover.png")); // gloss stretched onto the cells: bottom flush with EXP top
        qslot.AddChild(gloss);
        _gameScene.Add(qslot);
        var tracker = new GameObject { NoHit = true };
        tracker.Add(new Tracker(TrackerLines));
        _gameScene.Add(tracker);
        var bossBar = new GameObject { NoHit = true };
        bossBar.Add(new BossBar(BossBarW, FindBoss));
        _gameScene.Add(bossBar);
        var minimap = new GameObject { NoHit = true };
        minimap.Add(new Minimap(() => _map, () => _px, () => _py));
        _gameScene.Add(minimap);
        SyncPanelScenes(); // shots skip Update: without this the panels draw empty there (first Update no-ops, positions match)
    }

    // The living boss on this map, if any (v1 has one: Mushmom).
    private (string Name, int Hp, int MaxHp)? FindBoss()
    {
        foreach (var m in _mobs.Values)
            if (m.Hp > 0 && _mobData[m.Mob].Boss)
                return (_mobData[m.Mob].Name, m.Hp, m.MaxHp);
        return null;
    }

    private string HpText()
    {
        var chara = _save?.Slots[_slot];
        int maxHp = Progression.BuffedMaxHp(chara?.Level ?? 1, OwnHpPct());
        return $"{chara?.Hp ?? maxHp}/{maxHp}";
    }

    private string MpText()
    {
        var chara = _save?.Slots[_slot];
        int maxMp = Progression.MaxMp(chara?.Level ?? 1);
        return $"{chara?.Mp ?? maxMp}/{maxMp}";
    }

    // Live EXP for the bar (CLI sessions have none: the fill stays empty).
    private (int Value, int Max) ExpFill()
    {
        var chara = _save?.Slots[_slot];
        return chara == null ? (0, 1) : (chara.Exp, Progression.ExpNext(chara.Level));
    }

    // Panel trees are pure functions of (pos, tab): rebuild when either
    // moves. Bag counts are static catalogs, so slots never reshape.
    private void SyncPanelScenes()
    {
        if (_invPos != _invBuiltPos || _invTab != _invBuiltTab) { BuildInvScene(); _invBuiltPos = _invPos; _invBuiltTab = _invTab; }
        if (_statPos != _statBuiltPos) { BuildStatScene(); _statBuiltPos = _statPos; }
        if (_skillPos != _skillBuiltPos || _skillTab != _skillBuiltTab) { BuildSkillScene(); _skillBuiltPos = _skillPos; _skillBuiltTab = _skillTab; }
        var quests = QuestRows();
        if (_questPos != _questBuiltPos || !quests.SequenceEqual(_questBuiltRows)) { BuildQuestScene(quests); _questBuiltPos = _questPos; _questBuiltRows = quests; }
        if (_settingsPos != _settingsBuiltPos) { BuildSettingsScene(); _settingsBuiltPos = _settingsPos; }
        var sells = ShopSellRows();
        if (_shopStock != null && (_shopPos != _shopBuiltPos || !ReferenceEquals(_shopStock, _shopBuiltStock) || !sells.SequenceEqual(_shopBuiltSells)))
        { BuildShopScene(sells); _shopBuiltPos = _shopPos; _shopBuiltStock = _shopStock; _shopBuiltSells = sells; }
        if (_talkNpc != 0) // gated: TalkButtons needs an open talk (same exposure as the legacy chain)
        {
            string buttons = string.Join(",", TalkButtons());
            string entriesKey = _talkQuest == 0 ? string.Join(",", TalkEntries(_talkNpc).Select(e => e.quest)) : ""; // offers are level-gated: ids, not count (LvMax can swap at the same count)
            if (_talkNpc != _talkBuiltNpc || _talkQuest != _talkBuiltQuest || _talkPage != _talkBuiltPage || buttons != _talkBuiltButtons || entriesKey != _talkBuiltEntries)
            { BuildTalkScene(); _talkBuiltNpc = _talkNpc; _talkBuiltQuest = _talkQuest; _talkBuiltPage = _talkPage; _talkBuiltButtons = buttons; _talkBuiltEntries = entriesKey; }
        }
    }

    private static GameObject PanelButton(Rectangle r, string id, Action onClick, ButtonKind kind = ButtonKind.Silent)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        o.Add(new Button { LegacyId = id, Kind = kind, OnClick = onClick });
        return o;
    }

    // Dispatches a click and plays the hit Button's Kind. Conditional
    // sounds (shopbuy, gated clicks) stay explicit in their callbacks;
    // the old flag-snapshot funnel is gone.
    private GameObject? ClickTree(Scene? scene, int gx, int gy)
    {
        var hit = scene?.Click(gx, gy);
        if (scene?.ClickButton?.Kind == ButtonKind.Click) PlaySfx("click");
        return hit;
    }

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
                _skillCdMs.TryGetValue(id, out float cd);
                _buffMs.TryGetValue(id, out float buff);
                return new SkillLive(lv, chara?.Sp ?? 0, chara?.Level ?? 1, cd, buff, _orbs);
            }));
            body.AddChild(holder);
        }
        _skillScene.Add(body);
    }

    private void BuildQuestScene(List<int> accepted)
    {
        _questScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(QuestRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("QUEST"));
        body.Add(new Picture(_questBack, 0, 16));
        for (int row = 0; row < accepted.Count && row < 5; row++) // draw shows 5: clicks match
        {
            int i = row; // for-loop variables capture shared: copy per row
            int qid = accepted[i];
            string name = _quests[qid].Name;
            var rb = PanelButton(QuestRowRect(i), "questrow" + i, () => _questSel = accepted[i], ButtonKind.Click);
            rb.Add(new LiveTint(() =>
            {
                var c = _save?.Slots[_slot];
                bool done = _questLog.State(qid).IsComplete(c!); // rows exist only with a chara
                var ink = qid == _questSel ? new Color(255, 220, 0)
                    : done ? new Color(120, 255, 120) : Color.White;
                return ((done ? "? " : "") + name, ink);
            }, 4, 3));
            body.AddChild(rb);
        }
        body.AddChild(PanelButton(QuestTitleRect(), "", () => _dragQuest = _questScene.LastClick - _questPos)); // silent, like legacy
        var x = PanelButton(QuestXRect(), "x3", () => _showQuest = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        var detail = new GameObject { NoHit = true };
        detail.Add(new Rect(QuestRect()));
        detail.Add(new QuestDetail(() =>
        {
            var c = _save?.Slots[_slot];
            if (c == null || !_quests.TryGetValue(_questSel, out var sel) || !c.Quests.Contains(_questSel)) return null;
            return new QuestLive(QuestDetailLines(c, sel), sel.Kills.Select((k, i) =>
            {
                int n = _questLog.KillCount(c, _questSel, i);
                return ($"{MobName(k.Mob)} {n}/{k.Count}", n >= k.Count);
            }).ToList(), sel.NeedItemsToComplete ? "Need items" : $"Turn in: {NpcName(sel.Npc1)}");
        }, 16 + QuestListH + 10));
        body.AddChild(detail);
        _questScene.Add(body);
    }

    // Selected-quest wrapped lines, re-wrapped when the sel or its kill
    // counts change (counters ride the key: a kill under the open panel
    // re-renders #a, same as the talk cache).
    private List<string> QuestDetailLines(CharaSlot chara, QuestData sel)
    {
        string key = _questSel + ":" + string.Join(",", sel.Kills.Select((_, i) => _questLog.KillCount(chara, _questSel, i)));
        if (key != _questKey)
        {
            _questKey = key;
            _questLines = _ui.Wrap(QText(sel.Progress), QuestW - 24);
        }
        return _questLines;
    }

    private void BuildSettingsScene()
    {
        _settingsScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(SettingsRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("SETTINGS"));
        body.Add(new Picture(_settingsBack, 0, 16));
        string[] labels = { "Music", "Sound" };
        for (int r = 0; r < 2; r++)
        {
            int i = r; // for-loop variables capture shared: copy per row
            body.Add(new Label(labels[i], 12, 16 + 10 + i * SettingsRowH + 4));
            var minus = PanelButton(SettingsMinusRect(i), "setminus" + i, () => BumpVolume(i == 0, -10), ButtonKind.Click);
            minus.Add(new StepBox("-"));
            body.AddChild(minus);
            var plus = PanelButton(SettingsPlusRect(i), "setplus" + i, () => BumpVolume(i == 0, 10), ButtonKind.Click);
            plus.Add(new StepBox("+"));
            plus.Add(new RightLabel((ui, s) => ui.GetSmall(s), () => (i == 0 ? _bgmVol : _sfxVol).ToString(), 0, 4, Color.White));
            body.AddChild(plus);
        }
        body.AddChild(PanelButton(SettingsTitleRect(), "", () => _dragSettings = _settingsScene.LastClick - _settingsPos)); // silent, like legacy
        var x = PanelButton(SettingsXRect(), "x4", () => _showSettings = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        _settingsScene.Add(body);
    }

    private void BuildShopScene(List<(int id, int count)> sells)
    {
        _shopScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(ShopRect())); // no Button: the body swallows, like the legacy fallthrough
        body.Add(new TitleBar("SHOP"));
        body.Add(new Fill(new Color(62, 96, 118), 16));
        body.Add(new LiveLabel(() => $"Mesos: {(_save?.Slots[_slot]?.Mesos ?? 0):#,##0}", 12, 16 + 4, new Color(255, 220, 0)));
        body.Add(new Label("Buy", 12, 16 + 22));
        var r = ShopRect();
        for (int i = 0; i < _shopStock.Count; i++)
        {
            int s = i; // for-loop variables capture shared: copy per row
            int id = _shopStock[s];
            var def = _items[id];
            var holder = new GameObject { NoHit = true };
            holder.Add(new Rect(new Rectangle(r.X, ShopBuyTop() + s * ShopRowH, ShopW, ShopRowH)));
            holder.Add(new ShopRow(def.Image, def.Name, $"{def.Price:#,##0}"));
            body.AddChild(holder);
            var buy = PanelButton(ShopBuyRect(s), "buy" + s, () => BuyFromShop(_shopStock[s]));
            buy.Add(new FlatButton("Buy", "buy" + s, () => _hoverId));
            body.AddChild(buy);
        }
        body.Add(new Label("Sell", 12, ShopSellTop() - r.Y - 16));
        if (sells.Count == 0)
            body.Add(new Label("Your bag is empty.", 12, ShopSellTop() - r.Y + 12) { Tint = new Color(130, 140, 150) });
        for (int i = 0; i < sells.Count; i++)
        {
            int s = i; // for-loop variables capture shared: copy per row
            int id = sells[s].id;
            var holder = new GameObject { NoHit = true };
            holder.Add(new Rect(new Rectangle(r.X, ShopSellTop() + s * ShopRowH, ShopW, ShopRowH)));
            holder.Add(new ShopRow(_items[id].Image, $"{_items[id].Name} x{sells[s].count}", $"{Shop.SellPrice(_items[id].Price):#,##0}"));
            body.AddChild(holder);
            var sell = PanelButton(ShopSellRect(s), "sell" + s, () => SellToShop(sells[s].id));
            sell.Add(new FlatButton("Sell", "sell" + s, () => _hoverId));
            body.AddChild(sell);
        }
        body.AddChild(PanelButton(ShopTitleRect(), "", () => _dragShop = _shopScene.LastClick - _shopPos)); // silent, like legacy
        var x = PanelButton(ShopXRect(), "x5", () => _showShop = false, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);
        _shopScene.Add(body);
    }

    private void BuildTalkScene()
    {
        _talkScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(TalkRect())); // no Button: talk swallows every click, like the legacy guard
        body.Add(new TitleBar(_npcs[_talkNpc].Name));
        body.Add(new Fill(new Color(62, 96, 118), 16));
        body.Add(new TalkBody(() => TalkLines(), 16 + 8));
        if (_talkQuest == 0)
        {
            var entries = TalkEntries(_talkNpc);
            for (int i = 0; i < entries.Count; i++)
            {
                int r = i; // for-loop variables capture shared: copy per row
                int q = entries[r].quest;
                bool offer = entries[r].offer;
                var eb = PanelButton(TalkEntryRect(r), "talkentry" + r, () => { _talkQuest = entries[r].quest; _talkPage = 0; }, ButtonKind.Click);
                eb.Add(new LiveTint(() =>
                {
                    var c = _save?.Slots[_slot];
                    bool ready = !offer && c != null && _questLog.State(q).IsComplete(c);
                    var ink = offer || ready ? new Color(255, 220, 0) : new Color(130, 140, 150);
                    string mark = offer ? "! " : ready ? "? " : "";
                    return (mark + _quests[q].Name, ink);
                }, 0, 0));
                body.AddChild(eb);
            }
            if (NpcHasShop(_talkNpc))
            {
                var sb = PanelButton(TalkEntryRect(entries.Count), "talkshop", () => OpenShop(_talkNpc), ButtonKind.Click);
                sb.Add(new Label("Shop", 0, 0) { Tint = new Color(255, 220, 0) });
                body.AddChild(sb);
            }
        }
        var buttons = TalkButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            var bb = PanelButton(TalkBtnRect(r, buttons.Count), "talkbtn" + r, () => TalkAction(buttons[r]), ButtonKind.Click);
            bb.Add(new FlatButton(buttons[r], "talkbtn" + r, () => _hoverId));
            body.AddChild(bb);
        }
        _talkScene.Add(body);
    }

    private void AddSubArtRow(int i, string stem, Action onClick)
    {
        var row = new GameObject();
        row.Add(new Rect(SubRowRect(i)));
        row.Add(new Button { LegacyId = "sub" + i, Kind = ButtonKind.Click, OnClick = onClick });
        var art = new GameObject();
        art.Add(new Rect(SubRowRect(i)));
        art.Add(new Image(stem));
        row.AddChild(art);
        _submenu.AddChild(row);
    }

    private void AddSubTextRow(int i, string text, Action onClick)
    {
        var row = new GameObject();
        row.Add(new Rect(SubRowRect(i)));
        row.Add(new Button { LegacyId = "sub" + i, Kind = ButtonKind.Click, HoverFill = true, OnClick = onClick });
        var label = new GameObject();
        label.Add(new Rect(SubRowRect(i)));
        label.Add(new Label(text, 8, 6));
        row.AddChild(label);
        _submenu.AddChild(row);
    }

    // Submenu popup: 3-slice bg sized by its own caps, six 109x26 rows inside.
    private const int SubPopupW = 121;
    private const int SubRows = 6; // stat, inventory, quest, settings, log, exit
    private Rectangle SubPopupRect()
    {
        int top = Tex("hud_sub_bg0.png").Height;
        int bottom = Tex("hud_sub_bg2.png").Height;
        int h = top + SubRows * 26 + (SubRows + 1) * 4 + bottom;
        int x = MenuBtnRect().X + (MenuBtnRect().Width - SubPopupW) / 2;
        return new Rectangle(x, MenuBtnRect().Y - 4 - h, SubPopupW, h);
    }

    private Rectangle SubRowRect(int i)
    {
        var p = SubPopupRect();
        int y = p.Y + Tex("hud_sub_bg0.png").Height + 4 + i * (26 + 4);
        return new Rectangle(p.X + 6, y, 109, 26);
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

    // Click a stock row: buy one at WZ price (a broke wallet logs and skips).
    private void BuyFromShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no wallet
        var def = _items[id];
        int price = def.Price;
        if (Shop.Buy(chara, id, price))
        {
            GameLog($"bought {def.Name} for {price:#,##0} mesos");
            PlaySfx("shopbuy");
        }
        else GameLog($"not enough mesos for {def.Name} ({price:#,##0})");
    }

    // Click a bag row: sell one at half price (rows show owned only, so it lands).
    private void SellToShop(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no bag
        var def = _items[id];
        if (Shop.Sell(chara, id, def))
        {
            GameLog($"sold {def.Name} for {Shop.SellPrice(def.Price):#,##0} mesos");
            PlaySfx("click");
        }
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

    private Point _questPos = new(Layout.Quest.X, Layout.Quest.Y); // quest window opens centered-ish, drags after
    private Rectangle QuestRect() => new(_questPos.X, _questPos.Y, QuestW, 16 + QuestBodyH);
    private Rectangle QuestTitleRect() => new(_questPos.X, _questPos.Y, QuestW, 16);
    private Rectangle QuestXRect()
    {
        var r = QuestRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private Point _settingsPos = new(Layout.Settings.X, Layout.Settings.Y); // settings window opens centered-ish, drags after
    private Rectangle SettingsRect() => new(_settingsPos.X, _settingsPos.Y, SettingsW, 16 + SettingsBodyH);
    private Rectangle SettingsTitleRect() => new(_settingsPos.X, _settingsPos.Y, SettingsW, 16);
    private Rectangle SettingsXRect()
    {
        var r = SettingsRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    // Volume row buttons: [-] and [+] boxes around the value.
    private Rectangle SettingsMinusRect(int i)
    {
        var r = SettingsRect();
        return new Rectangle(r.X + 120, r.Y + 16 + 10 + i * SettingsRowH, 30, 24);
    }

    private Rectangle SettingsPlusRect(int i)
    {
        var r = SettingsRect();
        return new Rectangle(r.X + 200, r.Y + 16 + 10 + i * SettingsRowH, 30, 24);
    }

    // Sell rows: owned potions first, then owned equips (both in tab order).
    // Draw and clicks share this list, so row i is always row i.
    private List<(int id, int count)> ShopSellRows()
    {
        var rows = new List<(int, int)>();
        var chara = _save?.Slots[_slot];
        if (chara == null) return rows;
        foreach (int id in _potions)
            if (chara.Use?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        foreach (int id in _equips)
            if (chara.EquipBag?.TryGetValue(id, out int n) == true && n > 0) rows.Add((id, n));
        return rows;
    }

    // Shop rects: mesos line 20, Buy header 18 + stock rows, Sell header 18
    // + bag rows (an empty bag keeps one row for the "empty" line).
    private Point _shopPos = new(Layout.Shop.X, Layout.Shop.Y); // shop window opens centered-ish, drags after

    private Rectangle ShopRect()
    {
        int sells = Math.Max(1, ShopSellRows().Count);
        int body = 20 + 18 + _shopStock.Count * ShopRowH + 18 + sells * ShopRowH + 10;
        return new Rectangle(_shopPos.X, _shopPos.Y, ShopW, 16 + body);
    }

    private Rectangle ShopTitleRect() => new(_shopPos.X, _shopPos.Y, ShopW, 16);

    private Rectangle ShopXRect()
    {
        var r = ShopRect();
        return new Rectangle(r.X + r.Width - 11 - 4, r.Y + 2, 11, 11);
    }

    private int ShopBuyTop() => _shopPos.Y + 16 + 20 + 18;

    private int ShopSellTop() => ShopBuyTop() + _shopStock.Count * ShopRowH + 18;

    private Rectangle ShopBuyRect(int i) =>
        new(_shopPos.X + ShopW - 80, ShopBuyTop() + i * ShopRowH + 7, 68, 26);

    private Rectangle ShopSellRect(int i) =>
        new(_shopPos.X + ShopW - 80, ShopSellTop() + i * ShopRowH + 7, 68, 26);

    private void DrawHud(int gx, int gy, bool pressed)
    {
        _gfx.Pressed = pressed;
        _scenes.Current?.Draw(_gfx); // HUD art lives in the tree now (exp, buttons, submenu, plate, HP/MP, quickslot, tracker, boss bar, minimap); panels and talk draw above it
        // The front panel draws last (on top); the back five keep inv < stat < skill < quest < settings < shop.
        if (_topPanel == 0) { if (_showStat) _statScene.Draw(_gfx); if (_showSkill) _skillScene.Draw(_gfx); if (_showQuest) _questScene.Draw(_gfx); if (_showSettings) _settingsScene.Draw(_gfx); if (_showShop) _shopScene.Draw(_gfx); if (_showInv) _invScene.Draw(_gfx); }
        else if (_topPanel == 1) { if (_showInv) _invScene.Draw(_gfx); if (_showSkill) _skillScene.Draw(_gfx); if (_showQuest) _questScene.Draw(_gfx); if (_showSettings) _settingsScene.Draw(_gfx); if (_showShop) _shopScene.Draw(_gfx); if (_showStat) _statScene.Draw(_gfx); }
        else if (_topPanel == 2) { if (_showInv) _invScene.Draw(_gfx); if (_showStat) _statScene.Draw(_gfx); if (_showQuest) _questScene.Draw(_gfx); if (_showSettings) _settingsScene.Draw(_gfx); if (_showShop) _shopScene.Draw(_gfx); if (_showSkill) _skillScene.Draw(_gfx); }
        else if (_topPanel == 3) { if (_showInv) _invScene.Draw(_gfx); if (_showStat) _statScene.Draw(_gfx); if (_showSkill) _skillScene.Draw(_gfx); if (_showSettings) _settingsScene.Draw(_gfx); if (_showShop) _shopScene.Draw(_gfx); if (_showQuest) _questScene.Draw(_gfx); }
        else if (_topPanel == 4) { if (_showInv) _invScene.Draw(_gfx); if (_showStat) _statScene.Draw(_gfx); if (_showSkill) _skillScene.Draw(_gfx); if (_showQuest) _questScene.Draw(_gfx); if (_showShop) _shopScene.Draw(_gfx); if (_showSettings) _settingsScene.Draw(_gfx); }
        else { if (_showInv) _invScene.Draw(_gfx); if (_showStat) _statScene.Draw(_gfx); if (_showSkill) _skillScene.Draw(_gfx); if (_showQuest) _questScene.Draw(_gfx); if (_showSettings) _settingsScene.Draw(_gfx); if (_showShop) _shopScene.Draw(_gfx); }
        if (_talkNpc != 0) _talkScene.Draw(_gfx); // modal: above everything
        if (!_hand.Empty) // the held piece rides the cursor (take-1: no count to show)
        {
            var icon = _textures[_items[_hand.Id].Image];
            _spriteBatch.Draw(icon, new Vector2(gx - icon.Width / 2, gy - icon.Height / 2), Color.White);
        }
    }

    private static Rectangle QslotCell(int i)
    {
        var r = QslotRect();
        return new Rectangle(r.X + 6 + i * 34, r.Y + 2, 34, 31);
    }

    // Solid backing with rounded corners (the art's own radius is ~3px, measured).
    private Texture2D RoundedBacking(int w, int h, Color color, int r)
    {
        var tex = new Texture2D(GraphicsDevice, w, h);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y);
                bool cut = dx < r && dy < r && (dx - r) * (dx - r) + (dy - r) * (dy - r) > r * r;
                px[y * w + x] = cut ? Color.Transparent : color;
            }
        tex.SetData(px);
        return tex;
    }

    // Rounds the title strip's top corners in place (drawn stretched ~2x wide,
    // so the native ellipse is rx=2 by ry=3).
    private static void RoundTopCorners(Texture2D tex, int rx, int ry)
    {
        Color[] px = new Color[tex.Width * tex.Height];
        tex.GetData(px);
        for (int y = 0; y < ry; y++)
            for (int x = 0; x < tex.Width; x++)
            {
                int dx = Math.Min(x, tex.Width - 1 - x);
                if (dx < rx && (dx - rx) * (dx - rx) * ry * ry + (y - ry) * (y - ry) * rx * rx > rx * rx * ry * ry)
                    px[y * tex.Width + x] = Color.Transparent;
            }
        tex.SetData(px);
    }

    // Quest window: accepted quests on top (click to select), the selected
    // quest's progress below (objectives with live counts + turn-in).
    private List<int> QuestRows()
    {
        var chara = _save?.Slots[_slot];
        // Unknown ids (removed custom file, stale save) hide everywhere: the
        // tracker skips them too, so draw, click, and tracker agree.
        return chara == null ? new List<int>()
            : chara.Quests.Where(id => _quests.ContainsKey(id)).OrderBy(id => id).ToList();
    }

    private Rectangle QuestRowRect(int i)
    {
        var r = QuestRect();
        return new Rectangle(r.X + 8, r.Y + 16 + 6 + i * QuestRowH, QuestW - 16, QuestRowH);
    }

    // Minimap: the WZ canvas scaled into a 220x160 corner box, portals as
    // blue dots, the player as a yellow dot. Always on, top-right.
    // Talk panel (modal): NPC name on top, one page of body, quest entries in
    // menu mode, buttons at the bottom. Height follows the content.
    private Rectangle TalkRect()
    {
        int lines = TalkLines().Count + (_talkQuest == 0 ? TalkEntries(_talkNpc).Count : 0)
            + (_talkQuest == 0 && NpcHasShop(_talkNpc) ? 1 : 0); // the Shop row in menu mode
        int h = 16 + 8 + lines * 16 + 8 + TalkBtnH + 10;
        return new Rectangle((1366 - TalkW) / 2, 340 - h / 2, TalkW, h);
    }

    private List<string> TalkLines()
    {
        // Counters ride the key: a kill under the open modal re-renders #a.
        var chara = _save?.Slots[_slot];
        var quest = _quests.GetValueOrDefault(_talkQuest);
        string kills = quest == null || chara == null ? ""
            : string.Join(",", quest.Kills.Select((_, i) => _questLog.KillCount(chara, _talkQuest, i)));
        string key = $"{_talkNpc}:{_talkQuest}:{_talkPage}:{kills}";
        if (key != _talkKey)
        {
            _talkKey = key;
            _talkLines = _ui.Wrap(QText(TalkPages()[_talkPage]), TalkW - 32);
        }
        return _talkLines;
    }

    private List<string> TalkButtons()
    {
        var q = _quests.GetValueOrDefault(_talkQuest);
        if (q == null) return new List<string> { "Bye" };
        var chara = _save?.Slots[_slot]!; // quest mode needs an entry click, which needs a chara
        bool last = _talkPage >= TalkPages().Count - 1;
        bool offer = !chara.Quests.Contains(q.QuestId);
        if (offer) return last ? new List<string> { "Accept", "Bye" } : new List<string> { "Next", "Bye" };
        if (_questLog.State(q.QuestId).IsComplete(chara)) return last ? new List<string> { "Complete", "Bye" } : new List<string> { "Next", "Bye" };
        return new List<string> { "Bye" };
    }

    private Rectangle TalkBtnRect(int i, int n)
    {
        var r = TalkRect();
        int total = n * TalkBtnW + (n - 1) * 8;
        return new Rectangle(r.X + (TalkW - total) / 2 + i * (TalkBtnW + 8), r.Bottom - TalkBtnH - 8, TalkBtnW, TalkBtnH);
    }

    private Rectangle TalkEntryRect(int i)
    {
        var r = TalkRect();
        return new Rectangle(r.X + 16, r.Y + 16 + 8 + TalkLines().Count * 16 + i * 16, TalkW - 32, 16);
    }

}
