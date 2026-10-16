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
    private int _skillTab; // 0 Warrior, 1 Fighter, 2 Crusader
    private bool _showInv, _showStat, _showSkill, _showQuest, _showSettings, _showShop;
    private readonly SceneManager _scenes = new();
    private Scene _gameScene = null!; // W1: the submenu only; later weeks add the rest
    private Scene _invScene = new(), _statScene = new(), _skillScene = new();
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
        charBtn.Add(new Button { LegacyId = "char", Kind = ButtonKind.Click, OnClick = () => { _showStat = !_showStat; _submenu.Enabled = false; if (_showStat) _topPanel = 1; } });
        _gameScene.Add(charBtn);
        var menuBtn = new GameObject();
        menuBtn.Add(new Rect(MenuBtnRect()));
        menuBtn.Add(new Button { LegacyId = "menu", Kind = ButtonKind.Click, OnClick = () => _submenu.Enabled = !_submenu.Enabled });
        _gameScene.Add(menuBtn); // last (topmost): legacy checks menu/char before the submenu and furniture
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

    private static GameObject PanelSlot(Rectangle r, string itemKind, int index, Func<Scene> scene, Action legacy)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        var s = new Slot { ItemKind = itemKind, Index = index };
        s.OnClick = () => scene().Carry.Resolve(s, legacy); // the scene is read live: rebuilds swap it
        o.Add(s);
        return o;
    }

    private void BuildInvScene()
    {
        _invScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(InvRect())); // no Button: the body swallows, like the legacy fallthrough
        if (_invTab == 1)
            for (int s = 0; s < _potions.Count; s++)
            {
                int i = s; // for-loop variables capture shared: copy per slot
                body.AddChild(PanelSlot(UseSlotRect(i), "use", i, () => _invScene, () => DrinkPotion(_potions[i])));
            }
        if (_invTab == 0)
            for (int s = 0; s < _equips.Count; s++)
            {
                int i = s; // for-loop variables capture shared: copy per slot
                body.AddChild(PanelSlot(WornRect(i), "worn", i, () => _invScene, () => { if (TakeOff(_equips[i])) { PushBuild(); PushName(); } }));
                body.AddChild(PanelSlot(EquipBagRect(i), "bag", i, () => _invScene, () => { if (Wear(_equips[i])) { PushBuild(); PushName(); } }));
            }
        for (int t = 0; t < TabCount; t++)
        {
            int i = t; // for-loop variables capture shared: copy per tab
            body.AddChild(PanelButton(TabRect(i), "invtab" + i, () => _invTab = i, ButtonKind.Click));
        }
        body.AddChild(PanelButton(InvTitleRect(), "", () => _dragInv = _invScene.LastClick - _invPos)); // silent, like legacy
        body.AddChild(PanelButton(InvXRect(), "x0", () => _showInv = false, ButtonKind.Click));
        _invScene.Add(body);
    }

    private void BuildStatScene()
    {
        _statScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(StatRect())); // stat shows numbers only: title + X, no content buttons
        body.AddChild(PanelButton(StatTitleRect(), "", () => _dragStat = _statScene.LastClick - _statPos));
        body.AddChild(PanelButton(StatXRect(), "x1", () => _showStat = false, ButtonKind.Click));
        _statScene.Add(body);
    }

    private void BuildSkillScene()
    {
        _skillScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(SkillRect())); // no Button: the body swallows, like the legacy fallthrough
        var tab = SkillTabs[_skillTab];
        for (int row = 0; row < tab.Length; row++)
        {
            int i = row; // for-loop variables capture shared: copy per row
            body.AddChild(PanelButton(SkillPlusRect(i), "skillplus" + i, () => { if (LearnSkill(tab[i].id)) PushBuild(); }, ButtonKind.Click));
        }
        for (int t = 0; t < SkillTabs.Length; t++)
        {
            int i = t; // for-loop variables capture shared: copy per tab
            body.AddChild(PanelButton(SkillTabRect(i), "skilltab" + i, () => _skillTab = i, ButtonKind.Click));
        }
        body.AddChild(PanelButton(SkillTitleRect(), "", () => _dragSkill = _skillScene.LastClick - _skillPos)); // silent, like legacy
        body.AddChild(PanelButton(SkillXRect(), "x2", () => _showSkill = false, ButtonKind.Click));
        _skillScene.Add(body);
    }

    private void BuildQuestScene(List<int> accepted)
    {
        _questScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(QuestRect())); // no Button: the body swallows, like the legacy fallthrough
        for (int row = 0; row < accepted.Count && row < 5; row++) // draw shows 5: clicks match
        {
            int i = row; // for-loop variables capture shared: copy per row
            body.AddChild(PanelButton(QuestRowRect(i), "questrow" + i, () => _questSel = accepted[i], ButtonKind.Click));
        }
        body.AddChild(PanelButton(QuestTitleRect(), "", () => _dragQuest = _questScene.LastClick - _questPos)); // silent, like legacy
        body.AddChild(PanelButton(QuestXRect(), "x3", () => _showQuest = false, ButtonKind.Click));
        _questScene.Add(body);
    }

    private void BuildSettingsScene()
    {
        _settingsScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(SettingsRect())); // no Button: the body swallows, like the legacy fallthrough
        for (int r = 0; r < 2; r++)
        {
            int i = r; // for-loop variables capture shared: copy per row
            body.AddChild(PanelButton(SettingsMinusRect(i), "setminus" + i, () => BumpVolume(i == 0, -10), ButtonKind.Click));
            body.AddChild(PanelButton(SettingsPlusRect(i), "setplus" + i, () => BumpVolume(i == 0, 10), ButtonKind.Click));
        }
        body.AddChild(PanelButton(SettingsTitleRect(), "", () => _dragSettings = _settingsScene.LastClick - _settingsPos)); // silent, like legacy
        body.AddChild(PanelButton(SettingsXRect(), "x4", () => _showSettings = false, ButtonKind.Click));
        _settingsScene.Add(body);
    }

    private void BuildShopScene(List<(int id, int count)> sells)
    {
        _shopScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(ShopRect())); // no Button: the body swallows, like the legacy fallthrough
        for (int i = 0; i < sells.Count; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            body.AddChild(PanelButton(ShopSellRect(r), "sell" + r, () => SellToShop(sells[r].id)));
        }
        for (int i = 0; i < _shopStock.Count; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            body.AddChild(PanelButton(ShopBuyRect(r), "buy" + r, () => BuyFromShop(_shopStock[r])));
        }
        body.AddChild(PanelButton(ShopTitleRect(), "", () => _dragShop = _shopScene.LastClick - _shopPos)); // silent, like legacy
        body.AddChild(PanelButton(ShopXRect(), "x5", () => _showShop = false, ButtonKind.Click));
        _shopScene.Add(body);
    }

    private void BuildTalkScene()
    {
        _talkScene = new Scene();
        var body = new GameObject();
        body.Add(new Rect(TalkRect())); // no Button: talk swallows every click, like the legacy guard
        if (_talkQuest == 0)
        {
            var entries = TalkEntries(_talkNpc);
            for (int i = 0; i < entries.Count; i++)
            {
                int r = i; // for-loop variables capture shared: copy per row
                body.AddChild(PanelButton(TalkEntryRect(r), "talkentry" + r, () => { _talkQuest = entries[r].quest; _talkPage = 0; }, ButtonKind.Click));
            }
            if (NpcHasShop(_talkNpc))
                body.AddChild(PanelButton(TalkEntryRect(entries.Count), "talkshop", () => OpenShop(_talkNpc), ButtonKind.Click));
        }
        var buttons = TalkButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            body.AddChild(PanelButton(TalkBtnRect(r, buttons.Count), "talkbtn" + r, () => TalkAction(buttons[r]), ButtonKind.Click));
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

    // Click a USE slot: drink one. Empty slots and full pools do nothing.
    private void DrinkPotion(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions carry no bag
        var item = _items[id];
        Progression.UsePotion(chara, id, item.Hp, item.Mp, OwnHpPct());
    }

    // Click an EQUIP bag slot: wear one through the shared rule. True when worn.
    private bool Wear(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false; // CLI sessions carry no bag
        return Progression.WearEquip(chara, id, _items);
    }

    // Click a worn slot: back to the bag. Unworn slots do nothing.
    private bool TakeOff(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false; // CLI sessions carry no bag
        return Progression.TakeOffEquip(chara, id);
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
        DrawExpBar();
        DrawPlate();
        DrawHpMp();
        DrawHudButton(MenuBtnRect(), "hud_btn_menu", "menu", pressed);
        DrawHudButton(CharBtnRect(), "hud_btn_char", "char", pressed);
        _gfx.Pressed = pressed;
        _scenes.Current?.Draw(_gfx); // the submenu only, for now (disabled subtrees skip themselves)
        DrawQslot();
        DrawTracker();
        DrawBossBar();
        DrawMinimap(); // always on, top-right; panels and talk draw above it
        // The front panel draws last (on top); the back five keep inv < stat < skill < quest < settings < shop.
        if (_topPanel == 0) { if (_showStat) DrawStatPanel(); if (_showSkill) DrawSkillPanel(); if (_showQuest) DrawQuestPanel(); if (_showSettings) DrawSettingsPanel(); if (_showShop) DrawShopPanel(gx, gy); if (_showInv) DrawInvPanel(); }
        else if (_topPanel == 1) { if (_showInv) DrawInvPanel(); if (_showSkill) DrawSkillPanel(); if (_showQuest) DrawQuestPanel(); if (_showSettings) DrawSettingsPanel(); if (_showShop) DrawShopPanel(gx, gy); if (_showStat) DrawStatPanel(); }
        else if (_topPanel == 2) { if (_showInv) DrawInvPanel(); if (_showStat) DrawStatPanel(); if (_showQuest) DrawQuestPanel(); if (_showSettings) DrawSettingsPanel(); if (_showShop) DrawShopPanel(gx, gy); if (_showSkill) DrawSkillPanel(); }
        else if (_topPanel == 3) { if (_showInv) DrawInvPanel(); if (_showStat) DrawStatPanel(); if (_showSkill) DrawSkillPanel(); if (_showSettings) DrawSettingsPanel(); if (_showShop) DrawShopPanel(gx, gy); if (_showQuest) DrawQuestPanel(); }
        else if (_topPanel == 4) { if (_showInv) DrawInvPanel(); if (_showStat) DrawStatPanel(); if (_showSkill) DrawSkillPanel(); if (_showQuest) DrawQuestPanel(); if (_showShop) DrawShopPanel(gx, gy); if (_showSettings) DrawSettingsPanel(); }
        else { if (_showInv) DrawInvPanel(); if (_showStat) DrawStatPanel(); if (_showSkill) DrawSkillPanel(); if (_showQuest) DrawQuestPanel(); if (_showSettings) DrawSettingsPanel(); if (_showShop) DrawShopPanel(gx, gy); }
        if (_talkNpc != 0) DrawTalkPanel(); // modal: above everything
    }

    private void DrawHudButton(Rectangle r, string stem, string id, bool pressed)
    {
        bool hover = _hoverId == id; // the z-winner, not live HitRect: covered buttons stay dark
        string file = hover ? pressed ? $"{stem}_p.png" : $"{stem}_h.png" : $"{stem}_n.png";
        _spriteBatch.Draw(Tex(file), r, Color.White);
    }

    private void DrawExpBar()
    {
        var r = ExpRect();
        _spriteBatch.Draw(Tex("hud_expback.png"), r, Color.White);
        var chara = _save?.Slots[_slot];
        if (chara != null && chara.Exp > 0) // CLI sessions have no EXP: the bar stays empty
        {
            var gauge = Tex("hud_expgauge.png");
            int w = gauge.Width * chara.Exp / Progression.ExpNext(chara.Level);
            if (w > 0)
                _spriteBatch.Draw(gauge, new Vector2(r.X, r.Y), new Rectangle(0, 0, w, gauge.Height), Color.White);
        }
        _spriteBatch.Draw(Tex("hud_expcover.png"), new Vector2(r.X, r.Y), Color.White);
    }

    private void DrawPlate()
    {
        var r = PlateRect();
        _spriteBatch.Draw(Tex("hud_plate.png"), r, Color.White);
        var chara = _save?.Slots[_slot];
        int x = r.X + 28; // WZ levelPos; digits step 10 (numInterval)
        foreach (char ch in (chara?.Level ?? 1).ToString())
        {
            _spriteBatch.Draw(Tex($"hud_lv{ch}.png"), new Vector2(x, r.Y + 11), Color.White);
            x += 10;
        }
        _spriteBatch.Draw(Tex("hud_lvtag.png"), new Vector2(r.X + 8, r.Y + 13), Color.White);
        if (chara != null) // WZ namePos; CLI sessions have no name
            _spriteBatch.Draw(_ui.GetSmall(chara.Name), new Vector2(r.X + 67, r.Y + 13), Color.White);
    }

    private void DrawHpMp()
    {
        var r = HpRect();
        _spriteBatch.Draw(Tex("hud_hpbg.png"), r, Color.White);
        // main/status/normal anchors (canvas draws at anchor minus origin):
        // fills at bg+(4,4)/(4,25), cover at bg+(3,3), numbers centered on
        // x+101 (vector:hp/mp), tops y+7/y+28. Bars always full: nothing
        // drains HP/MP until W13 mob hits (W13 adds the clip then).
        var chara = _save?.Slots[_slot];
        int maxHp = Progression.BuffedMaxHp(chara?.Level ?? 1, OwnHpPct()), maxMp = Progression.MaxMp(chara?.Level ?? 1);
        _spriteBatch.Draw(Tex("hud_hpfill.png"), new Vector2(r.X + 4, r.Y + 4), Color.White);
        _spriteBatch.Draw(Tex("hud_mpfill.png"), new Vector2(r.X + 4, r.Y + 25), Color.White);
        DrawBarNumber($"{chara?.Hp ?? maxHp}/{maxHp}", r.X + 101, r.Y + 7);
        DrawBarNumber($"{chara?.Mp ?? maxMp}/{maxMp}", r.X + 101, r.Y + 28);
        _spriteBatch.Draw(Tex("hud_hpcover.png"), new Vector2(r.X + 3, r.Y + 3), Color.White);
    }

    // Art digits centered on the official x; the "/" is baked 12px
    // (font:number is Arial 12; the WZ separator name breaks path lookup).
    private void DrawBarNumber(string text, int centerX, int topY)
    {
        int dh = Tex("hud_num0.png").Height;
        int w = 0;
        foreach (char ch in text) w += ch == '/' ? _ui.GetSmall("/").Width : Tex($"hud_num{ch}.png").Width;
        int x = centerX - w / 2;
        foreach (char ch in text)
        {
            if (ch == '/')
            {
                var s = _ui.GetSmall("/");
                _spriteBatch.Draw(s, new Vector2(x, topY + (dh - s.Height) / 2), Color.White);
                x += s.Width;
            }
            else
            {
                var d = Tex($"hud_num{ch}.png");
                _spriteBatch.Draw(d, new Vector2(x, topY), Color.White);
                x += d.Width;
            }
        }
    }

    private void DrawQslot()
    {
        var r = QslotRect();
        _spriteBatch.Draw(Tex("hud_qslot.png"), r, Color.White);
        // Fixed W9 hotkeys: official key art top-left (icons arrive with skills/potions).
        (string file, string action)[] slots = new[] { ("hud_key_ctrl.png", "Attack"), ("hud_key_alt.png", "Jump"), ("hud_key_i.png", "Items"), ("hud_key_c.png", "Stats") };
        for (int i = 0; i < slots.Length; i++)
        {
            var s = QslotCell(i);
            var k = Tex(slots[i].file);
            var a = _ui.GetSmall(slots[i].action);
            _spriteBatch.Draw(k, new Vector2(s.X + 2, s.Y + 2), Color.White);
            _spriteBatch.Draw(a, new Vector2(s.X + (s.Width - a.Width) / 2, s.Y + 2 + k.Height), Color.Gray);
        }
        _spriteBatch.Draw(Tex("hud_qslotcover.png"), r, Color.White); // gloss stretched onto the cells: bottom flush with EXP top
    }

    private static Rectangle QslotCell(int i)
    {
        var r = QslotRect();
        return new Rectangle(r.X + 6 + i * 34, r.Y + 2, 34, 31);
    }

    private void DrawInvPanel()
    {
        var r = InvRect();
        _spriteBatch.Draw(Tex("hud_inv.png"), r, Color.White);
        for (int t = 0; t < TabCount; t++) DrawTab(t);
        _spriteBatch.Draw(Tex("hud_statx.png"), InvXRect(), Color.White);
        var chara = _save?.Slots[_slot];
        if (_invTab == 0 && chara != null) // EQUIP tab: gold worn row (click takes off) over bag rows (click wears)
        {
            var cap = _ui.GetSmall("worn");
            _spriteBatch.Draw(cap, new Vector2(r.X + 14, r.Y + 78), new Color(255, 220, 0));
            for (int i = 0; i < _equips.Count; i++)
            {
                int id = _equips[i];
                int n = chara.Equips?.Count(w => w == id) ?? 0;
                if (n > 0) DrawWornSlot(i, id, n);
                int have = chara.EquipBag?.GetValueOrDefault(id) ?? 0;
                if (have > 0) DrawInvSlot(EquipBagRect(i), id, have);
            }
        }
        if (_invTab == 1 && chara?.Use != null) // USE tab: owned potions only (Etc/Setup/Cash/Dec stay empty in v1)
            for (int i = 0; i < _potions.Count; i++)
                if (chara.Use.TryGetValue(_potions[i], out int count) && count > 0)
                    DrawInvSlot(UseSlotRect(i), _potions[i], count);
        // Official numbers: Arial Bold 12 #606871, right end x+213 top y+484 (font:Meso, vector:Meso).
        _spriteBatch.Draw(Tex("hud_coin.png"), new Vector2(r.X + Layout.Coin.X, r.Y + Layout.Coin.Y), Color.White);
        var meso = _ui.GetMeso((chara?.Mesos ?? 0).ToString("#,##0"));
        _spriteBatch.Draw(meso, new Vector2(r.X + Layout.Meso.X - meso.Width, r.Y + Layout.Meso.Y), new Color(96, 104, 113));
    }

    private void DrawTab(int t)
    {
        var tr = TabRect(t);
        string file = t == _invTab ? $"hud_itab_s{t}.png" : $"hud_itab_n{t}.png";
        _spriteBatch.Draw(Tex(file), new Vector2(tr.X, tr.Y), Color.White);
    }

    // One worn slot: gold-tinted cell (it reads as equipped, not bagged).
    private void DrawWornSlot(int i, int id, int count)
    {
        var s = WornRect(i);
        _spriteBatch.Draw(_pixel, s, new Color(255, 220, 0, 60));
        DrawInvSlot(s, id, count);
    }

    // One inventory slot: icon centered in the cell, count bottom-right.
    private void DrawInvSlot(Rectangle s, int id, int count)
    {
        var icon = _textures[_items[id].Image];
        _spriteBatch.Draw(icon, new Vector2(s.X + (s.Width - icon.Width) / 2, s.Y + (s.Height - icon.Height) / 2), Color.White);
        var n = _ui.GetSmall(count.ToString());
        _spriteBatch.Draw(n, new Vector2(s.X + s.Width - n.Width - 2, s.Y + s.Height - n.Height - 2), Color.Black);
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

    private void DrawStatPanel()
    {
        var r = StatRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall("STAT"), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(Tex("hud_statx.png"), StatXRect(), Color.White);
        int by = r.Y + 16;
        _spriteBatch.Draw(_statBack, new Vector2(r.X, by), Color.White);
        _spriteBatch.Draw(Tex("hud_stat.png"), new Vector2(r.X, by), Color.White);
        var chara = _save?.Slots[_slot];
        var build = OwnBuild(); // STR/DEX/range show skilled values (passives and buffs feed combat)
        int maxHp = Progression.BuffedMaxHp(chara?.Level ?? 1, OwnHpPct()), maxMp = Progression.MaxMp(chara?.Level ?? 1);
        var cp = _ui.GetSmall("0"); // value only; "Combat Power" is baked in the art (no formula yet)
        _spriteBatch.Draw(cp, new Vector2(r.X + 432 - cp.Width, by + 8), Color.White);
        int y = by + 132; // panel 1: vitals + 4 stats (name/level live on the plate)
        DrawStatRow("HP", $"{chara?.Hp ?? maxHp} / {maxHp}", r.X, y); y += 22;
        DrawStatRow("MP", $"{chara?.Mp ?? maxMp} / {maxMp}", r.X, y); y += 22;
        DrawStatRow("STR", $"{build.Str}", r.X, y); y += 22;
        DrawStatRow("DEX", $"{build.Dex}", r.X, y); y += 22;
        DrawStatRow("INT", $"{chara?.Int ?? 4}", r.X, y); y += 22;
        DrawStatRow("LUK", $"{chara?.Luk ?? 4}", r.X, y);
        y = by + 320; // panel 2 is only ~80px: 5 tight rows (official rows are tight too)
        (int lo, int hi) = Progression.PanelRange(build.Str, build.Dex, build.WeaponAtk);
        DrawStatRow("Attack Power", $"{lo}~{hi}", r.X, y); y += 16;
        DrawStatRow("Magic ATT", "0", r.X, y); y += 16;
        DrawStatRow("Defense", $"{OwnDef()}", r.X, y); y += 16;
        DrawStatRow("Speed", "100", r.X, y); y += 16;
        DrawStatRow("Jump", "100", r.X, y);
    }

    private void DrawStatRow(string label, string value, int x, int y)
    {
        _spriteBatch.Draw(_ui.GetSmall(label), new Vector2(x + 16, y), new Color(50, 50, 60));
        var v = _ui.GetSmall(value);
        _spriteBatch.Draw(v, new Vector2(x + 432 - v.Width, y), Color.Black);
    }

    // Skill window: one tab per job (icon, name, level, costs, key).
    // Locked skills show the required level in red; cooling skills and live
    // buffs count seconds; learnable rows grow a green [+] (one SP a click).
    private void DrawSkillPanel()
    {
        var r = SkillRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall("SKILL"), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(Tex("hud_statx.png"), SkillXRect(), Color.White);
        int by = r.Y + 16;
        _spriteBatch.Draw(_skillBack, new Vector2(r.X, by), Color.White);
        for (int t = 0; t < SkillTabs.Length; t++)
        {
            var tab = SkillTabRect(t);
            var ink = t == _skillTab ? new Color(255, 220, 0) : new Color(130, 140, 150);
            _spriteBatch.Draw(_ui.GetSmall(SkillTabNames[t]), new Vector2(tab.X + 8, tab.Y + 2), ink);
        }
        int sp = _save?.Slots[_slot]?.Sp ?? 0;
        _spriteBatch.Draw(_ui.GetSmall(sp > 0 ? $"SP: {sp} (click + to learn)" : $"SP: {sp}"),
            new Vector2(r.X + 16, by + 26), Color.White);
        var rows = SkillTabs[_skillTab];
        for (int i = 0; i < rows.Length; i++)
            DrawSkillRow(rows[i].id, rows[i].key, r.X, by + SkillRowsY + i * SkillRowH);
    }

    private void DrawSkillRow(int id, string? key, int x, int y)
    {
        var skill = _skills[id];
        var chara = _save?.Slots[_slot];
        int level = chara?.Level ?? 1;
        int lv = 0;
        chara?.SkillLv.TryGetValue(id, out lv);
        bool locked = level < skill.ReqLevel;
        bool maxed = lv >= skill.MaxLevel;
        bool learnable = !locked && !maxed && (chara?.Sp ?? 0) > 0;
        float left = 0;
        bool cooling = !locked && _skillCdMs.TryGetValue(id, out left) && left > 0;
        float buffLeft = 0;
        bool live = _buffMs.TryGetValue(id, out buffLeft) && buffLeft > 0;
        _spriteBatch.Draw(_textures[locked || cooling ? skill.Disabled : skill.Icon], new Vector2(x + 10, y + 8), Color.White);
        var ink = locked ? new Color(130, 140, 150) : Color.White;
        _spriteBatch.Draw(_ui.GetSmall(skill.Name), new Vector2(x + 50, y + 4), ink);
        string sub = skill.Kind == SkillData.KindPassive
            ? $"Lv.{lv}/{skill.MaxLevel}  {PassiveBlurb[id]}"
            : skill.Kind == SkillData.KindBuff
            ? $"Lv.{lv}/{skill.MaxLevel}  MP {skill.Mp}  {skill.BuffMs / 1000}s{(id == SkillData.ComboAttack && live ? $"  {_orbs} orbs" : "")}"
            : $"Lv.{lv}/{skill.MaxLevel}  MP {skill.Mp}  CD {skill.CooldownMs / 1000}s";
        _spriteBatch.Draw(_ui.GetSmall(sub), new Vector2(x + 50, y + 24), ink);
        string right = locked ? $"Lv.{skill.ReqLevel}"
            : live ? $"{(int)Math.Ceiling(buffLeft / 1000)}s"
            : cooling ? $"{(int)Math.Ceiling(left / 1000)}s"
            : key ?? $"{lv}/{skill.MaxLevel}";
        var rc = locked ? new Color(255, 120, 120)
            : live || cooling ? new Color(255, 220, 0) : Color.White;
        if (learnable) { right += " [+]"; rc = new Color(120, 255, 120); }
        var rt = _ui.GetSmall(right);
        _spriteBatch.Draw(rt, new Vector2(x + SkillW - 8 - rt.Width, y + 4), rc);
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

    private void DrawQuestPanel()
    {
        var chara = _save?.Slots[_slot];
        var r = QuestRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall("QUEST"), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(Tex("hud_statx.png"), QuestXRect(), Color.White);
        _spriteBatch.Draw(_questBack, new Vector2(r.X, r.Y + 16), Color.White);
        var rows = QuestRows();
        for (int i = 0; i < rows.Count && i < 5; i++)
        {
            var q = _quests[rows[i]];
            bool done = QuestLog.IsComplete(chara!, q); // rows exist only with a chara
            var ink = rows[i] == _questSel ? new Color(255, 220, 0)
                : done ? new Color(120, 255, 120) : Color.White;
            _spriteBatch.Draw(_ui.GetSmall((done ? "? " : "") + q.Name),
                new Vector2(r.X + 12, r.Y + 16 + 6 + i * QuestRowH + 3), ink);
        }
        if (chara != null && _quests.TryGetValue(_questSel, out var sel) && chara.Quests.Contains(_questSel))
        {
            string key = _questSel + ":" + string.Join(",", sel.Kills.Select((_, i) => QuestLog.KillCount(chara, _questSel, i)));
            if (key != _questKey)
            {
                _questKey = key;
                _questLines = _ui.Wrap(QText(sel.Progress), QuestW - 24);
            }
            int y = r.Y + 16 + QuestListH + 10;
            // The block holds 180px: text yields to kills (kills cap at 9, so this stays >= 1).
            foreach (string line in _questLines.Take(10 - sel.Kills.Count))
            {
                _spriteBatch.Draw(_ui.GetSmall(line), new Vector2(r.X + 12, y), Color.White);
                y += 16;
            }
            for (int i = 0; i < sel.Kills.Count; i++)
            {
                var k = sel.Kills[i];
                int n = QuestLog.KillCount(chara, _questSel, i);
                var ink = n >= k.Count ? new Color(120, 255, 120) : Color.White;
                _spriteBatch.Draw(_ui.GetSmall($"{MobName(k.Mob)} {n}/{k.Count}"), new Vector2(r.X + 12, y), ink);
                y += 16;
            }
            string turnIn = sel.NeedItemsToComplete ? "Need items" : $"Turn in: {NpcName(sel.Npc1)}";
            _spriteBatch.Draw(_ui.GetSmall(turnIn), new Vector2(r.X + 12, y), new Color(255, 220, 0));
        }
    }

    // Settings window: two volume rows (music, effects). [-]/[+] step 10
    // and save at once; the value sits right-aligned between the buttons.
    private void DrawSettingsPanel()
    {
        var r = SettingsRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall("SETTINGS"), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(Tex("hud_statx.png"), SettingsXRect(), Color.White);
        _spriteBatch.Draw(_settingsBack, new Vector2(r.X, r.Y + 16), Color.White);
        string[] labels = { "Music", "Sound" };
        int[] vols = { _bgmVol, _sfxVol };
        for (int i = 0; i < 2; i++)
        {
            int y = r.Y + 16 + 10 + i * SettingsRowH;
            _spriteBatch.Draw(_ui.GetSmall(labels[i]), new Vector2(r.X + 12, y + 4), Color.White);
            var minus = SettingsMinusRect(i);
            var plus = SettingsPlusRect(i);
            _spriteBatch.Draw(_pixel, minus, new Color(0, 0, 0, 120));
            _spriteBatch.Draw(_pixel, plus, new Color(0, 0, 0, 120));
            var dash = _ui.GetSmall("-");
            _spriteBatch.Draw(dash, new Vector2(minus.X + (minus.Width - dash.Width) / 2, y + 4), Color.White);
            var cross = _ui.GetSmall("+");
            _spriteBatch.Draw(cross, new Vector2(plus.X + (plus.Width - cross.Width) / 2, y + 4), Color.White);
            var val = _ui.GetSmall(vols[i].ToString());
            _spriteBatch.Draw(val, new Vector2(plus.X - val.Width, y + 4), Color.White); // right-aligned to the [+] box
        }
    }

    // Pio's shop: wallet line, stock rows (icon + name + price + [Buy]),
    // bag rows (icon + name xN + sell price + [Sell]). Height follows the
    // bag, so the body is a plain fill, not a fixed rounded backing.
    private void DrawShopPanel(int gx, int gy)
    {
        var chara = _save?.Slots[_slot];
        var r = ShopRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall("SHOP"), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(Tex("hud_statx.png"), ShopXRect(), Color.White);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y + 16, r.Width, r.Height - 16), new Color(62, 96, 118));
        _spriteBatch.Draw(_ui.GetSmall($"Mesos: {(chara?.Mesos ?? 0):#,##0}"), new Vector2(r.X + 12, r.Y + 16 + 4), new Color(255, 220, 0));
        _spriteBatch.Draw(_ui.GetSmall("Buy"), new Vector2(r.X + 12, r.Y + 16 + 22), Color.White);
        for (int i = 0; i < _shopStock.Count; i++)
        {
            int id = _shopStock[i];
            var def = _items[id];
            int y = ShopBuyTop() + i * ShopRowH;
            DrawShopIcon(id, r.X + 12, y);
            _spriteBatch.Draw(_ui.GetSmall(def.Name), new Vector2(r.X + 52, y + 4), Color.White);
            _spriteBatch.Draw(_ui.GetSmall($"{def.Price:#,##0}"), new Vector2(r.X + 52, y + 20), new Color(130, 140, 150));
            DrawShopButton(ShopBuyRect(i), "Buy", "buy" + i);
        }
        _spriteBatch.Draw(_ui.GetSmall("Sell"), new Vector2(r.X + 12, ShopSellTop() - 16), Color.White);
        var sells = ShopSellRows();
        if (sells.Count == 0)
            _spriteBatch.Draw(_ui.GetSmall("Your bag is empty."), new Vector2(r.X + 12, ShopSellTop() + 12), new Color(130, 140, 150));
        for (int i = 0; i < sells.Count; i++)
        {
            int y = ShopSellTop() + i * ShopRowH;
            DrawShopIcon(sells[i].id, r.X + 12, y);
            _spriteBatch.Draw(_ui.GetSmall($"{_items[sells[i].id].Name} x{sells[i].count}"), new Vector2(r.X + 52, y + 4), Color.White);
            _spriteBatch.Draw(_ui.GetSmall($"{Shop.SellPrice(_items[sells[i].id].Price):#,##0}"), new Vector2(r.X + 52, y + 20), new Color(130, 140, 150));
            DrawShopButton(ShopSellRect(i), "Sell", "sell" + i);
        }
    }

    private void DrawShopIcon(int id, int x, int y)
    {
        var icon = _textures[_items[id].Image];
        _spriteBatch.Draw(icon, new Vector2(x + (32 - icon.Width) / 2, y + (ShopRowH - icon.Height) / 2), Color.White);
    }

    private void DrawShopButton(Rectangle b, string label, string id)
    {
        _spriteBatch.Draw(_pixel, b, _hoverId == id ? new Color(52, 88, 110) : new Color(30, 50, 66));
        var t = _ui.GetSmall(label);
        _spriteBatch.Draw(t, new Vector2(b.X + (b.Width - t.Width) / 2, b.Y + 5), Color.White);
    }

    // Minimap: the WZ canvas scaled into a 220x160 corner box, portals as
    // blue dots, the player as a yellow dot. Always on, top-right.
    private void DrawMinimap()
    {
        var tex = _textures[_map.MiniMap.Image];
        float s = Math.Min(220f / tex.Width, 160f / tex.Height);
        int w = (int)(tex.Width * s), h = (int)(tex.Height * s);
        int x = 1366 - 8 - w, y = 8;
        _spriteBatch.Draw(_pixel, new Rectangle(x - 2, y - 2, w + 4, h + 4), new Color(0, 0, 0, 160));
        _spriteBatch.Draw(tex, new Rectangle(x, y, w, h), Color.White);
        foreach (var p in _map.Portals)
        {
            var (dx, dy) = _map.MiniMap.ToDot(p.X, p.Y, w, h);
            _spriteBatch.Draw(_pixel, new Rectangle(x + dx - 1, y + dy - 1, 3, 3), new Color(80, 160, 255));
        }
        var (px, py) = _map.MiniMap.ToDot(_px, _py, w, h);
        _spriteBatch.Draw(_pixel, new Rectangle(x + px - 1, y + py - 1, 3, 3), new Color(255, 220, 0));
    }

    // Quest tracker: always-on lines under the top-left corner (nothing
    // accepted, nothing shown, so old shots stay pixel-identical).
    private void DrawTracker()
    {
        int y = 10;
        var lines = TrackerLines();
        for (int i = 0; i < lines.Count; i++) // name/detail pairs: even lines are gold names
        {
            var t = _ui.GetSmall(lines[i]);
            _spriteBatch.Draw(_pixel, new Rectangle(6, y - 2, t.Width + 8, 16), new Color(0, 0, 0, 120));
            _spriteBatch.Draw(t, new Vector2(10, y), i % 2 == 0 ? new Color(255, 220, 0) : Color.White);
            y += 16;
        }
    }

    // Boss HP bar (top-center, fixed): shows while a boss lives on this map
    // (v1 has one: Mushmom). Backing + name/numbers + red bar; death hides it.
    private void DrawBossBar()
    {
        MobView? boss = null;
        foreach (var m in _mobs.Values)
            if (m.Hp > 0 && _mobData[m.Mob].Boss) { boss = m; break; }
        if (boss == null) return;
        int x = (1366 - BossBarW) / 2;
        var head = _ui.GetSmall($"{_mobData[boss.Mob].Name}  {boss.Hp} / {boss.MaxHp}");
        _spriteBatch.Draw(_pixel, new Rectangle(x - 4, 8, BossBarW + 8, 32), new Color(0, 0, 0, 120));
        _spriteBatch.Draw(head, new Vector2(x + (BossBarW - head.Width) / 2, 10), new Color(255, 220, 0));
        _spriteBatch.Draw(_pixel, new Rectangle(x, 28, BossBarW, 8), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(x + 1, 29, Math.Max(1, (BossBarW - 2) * boss.Hp / boss.MaxHp), 6), new Color(220, 60, 60));
    }

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
            : string.Join(",", quest.Kills.Select((_, i) => QuestLog.KillCount(chara, _talkQuest, i)));
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
        if (QuestLog.IsComplete(chara, q)) return last ? new List<string> { "Complete", "Bye" } : new List<string> { "Next", "Bye" };
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

    private void DrawTalkPanel()
    {
        var chara = _save?.Slots[_slot];
        var r = TalkRect();
        _spriteBatch.Draw(Tex("hud_stathilong.png"), new Rectangle(r.X, r.Y, r.Width, 16), Color.White);
        _spriteBatch.Draw(_ui.GetSmall(_npcs[_talkNpc].Name), new Vector2(r.X + 8, r.Y + 2), new Color(200, 220, 0));
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y + 16, r.Width, r.Height - 16), new Color(62, 96, 118));
        int y = r.Y + 16 + 8;
        foreach (string line in TalkLines())
        {
            _spriteBatch.Draw(_ui.GetSmall(line), new Vector2(r.X + 16, y), Color.White);
            y += 16;
        }
        if (_talkQuest == 0)
        {
            var entries = TalkEntries(_talkNpc);
            for (int i = 0; i < entries.Count; i++)
            {
                var q = _quests[entries[i].quest];
                bool ready = !entries[i].offer && chara != null && QuestLog.IsComplete(chara, q);
                var ink = entries[i].offer || ready ? new Color(255, 220, 0) : new Color(130, 140, 150);
                string mark = entries[i].offer ? "! " : ready ? "? " : "";
                _spriteBatch.Draw(_ui.GetSmall(mark + q.Name), new Vector2(r.X + 16, y), ink);
                y += 16;
            }
            if (NpcHasShop(_talkNpc))
                _spriteBatch.Draw(_ui.GetSmall("Shop"), new Vector2(r.X + 16, y), new Color(255, 220, 0));
        }
        var buttons = TalkButtons();
        for (int i = 0; i < buttons.Count; i++)
        {
            var b = TalkBtnRect(i, buttons.Count);
            _spriteBatch.Draw(_pixel, b, _hoverId == "talkbtn" + i ? new Color(52, 88, 110) : new Color(30, 50, 66));
            var t = _ui.GetSmall(buttons[i]);
            _spriteBatch.Draw(t, new Vector2(b.X + (b.Width - t.Width) / 2, b.Y + 5), Color.White);
        }
    }

}
