using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// Bottom bar + panel dispatch (panels live in HudBags/HudQuest). Art is
// official (StatusBar3, UIInventory, UICharacterInfo); all text baked.
// Plate/slot positions are WZ vectors; the rest is tuned rects. HP/MP
// numbers and EXP are live; nothing drains HP until W13 mob hits, so the
// bars always draw full.
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
        "hud_coin.png", "hud_stathilong.png", "hud_statx.png",
    };
    private readonly Dictionary<int, ItemData> _items = new();
    private ManifestData _manifest = null!;

    // Scanned tab rosters (LoadItemDefs): consumables slot USE, equips slot
    // EQUIP, both in id order. Clicks and draws share them, so cell i is
    // always the same item on both paths.
    private List<int> _potions = new();
    private List<int> _equips = new();

    private readonly Dictionary<int, SkillData> _skills = new();
    private SkillCost _skillCost = null!; // live casts over _skills (same dict ref)
    private SkillDamage _skillDmg = null!;
    private int _skillTab; // 0 Warrior, 1 Fighter, 2 Crusader
    private bool _showInv, _showStat, _showSkill, _showQuest, _showSettings, _showShop;
    private readonly SceneManager _scenes = new();
    private Scene _gameScene = null!; // HUD furniture + exp + submenu + buttons + plate + bars + tracker + boss bar + minimap
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

    private static Rectangle PlateRect() => new(Layout.Plate.X, Layout.Plate.Y, 250, 35);
    private static Rectangle HpRect() => new(Layout.HpMp.X, Layout.HpMp.Y, 205, 46); // cover bottom flush with EXP top
    private static Rectangle MenuBtnRect() => new(Layout.MenuBtn.X, Layout.MenuBtn.Y, 34, 37);
    private static Rectangle CharBtnRect() => new(Layout.CharBtn.X, Layout.CharBtn.Y, 34, 37);
    private static Rectangle QslotRect() => new(Layout.Qslot.X, Layout.Qslot.Y, 557, 67);
    private static Rectangle ExpRect() => new(0, 758, 1366, 10);
    private Point _invPos = new(0, 0), _statPos = new(0, 0);
    private Point _skillPos = new(0, 0); // the skill window takes its spot from layouts/panel-skill.json
    private Point? _dragInv, _dragStat, _dragSkill, _dragQuest, _dragSettings, _dragShop; // title grab offsets while held
    private int _topPanel = 1; // the front panel: 0 = inventory, 1 = stat, 2 = skill, 3 = quest, 4 = settings, 5 = shop

    private Texture2D Tex(string file) => _textures["ui/" + file];

    private void UpdateHud(KeyboardState kb, bool click, bool held, int gx, int gy)
    {
        UpdateHudHover(gx, gy); // every frame (clicks return early below)
        var hotkey = FreshHotkey(kb);
        if (hotkey != null && _talkNpc == 0)
        {
            bool old = true; // the old panels click here; the new windows click themselves
            switch (hotkey.Value)
            {
                case HotkeyAction.Inventory: _showInv = !_showInv; _dragInv = null; if (_showInv) _topPanel = 0; break; // hiding mid-drag drops the grab
                case HotkeyAction.Stat: _showStat = !_showStat; _dragStat = null; if (_showStat) _topPanel = 1; break;
                case HotkeyAction.Skill: _showSkill = !_showSkill; _dragSkill = null; if (_showSkill) _topPanel = 2; break;
                case HotkeyAction.Quest: _showQuest = !_showQuest; _dragQuest = null; if (_showQuest) _topPanel = 3; break; // the modal freezes panels
                case HotkeyAction.Settings: if (_showSettings) CloseSettings(); else OpenSettings(); break;
                default: old = false; ToggleWindow(hotkey.Value); break; // the new windows draw above the older panels
            }
            if (old) PlaySfx("click");
        }
        // Title drag runs on held (down starts it in the chain below).
        if (_dragInv.HasValue)
        {
            if (!held) _dragInv = null;
            else _invPos = ClampPanel(new Point(gx - _dragInv.Value.X, gy - _dragInv.Value.Y), InvRect().Width);
        }
        if (_dragStat.HasValue)
        {
            if (!held) _dragStat = null;
            else _statPos = ClampPanel(new Point(gx - _dragStat.Value.X, gy - _dragStat.Value.Y), StatW);
        }
        if (_dragSkill.HasValue)
        {
            if (!held) _dragSkill = null;
            else _skillPos = ClampPanel(new Point(gx - _dragSkill.Value.X, gy - _dragSkill.Value.Y), SkillW);
        }
        if (_dragQuest.HasValue)
        {
            if (!held) _dragQuest = null;
            else { _questDragged = true; _questPos = ClampPanel(new Point(gx - _dragQuest.Value.X, gy - _dragQuest.Value.Y), QuestW); }
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
        DragWindows(held, gx, gy); // the new windows move by their title
        SyncWindows(); // the new windows rebuild when they move
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
        if (HoverWindows(gx, gy)) { _gameScene.ClearHover(); return; } // the new windows sit on top
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
        if (ClickWindows(gx, gy)) { _submenu.Enabled = false; return; } // the new windows sit on top
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

    // The game scene holds HUD furniture, exp, submenu, buttons, plate,
    // bars, tracker, boss bar, and minimap.
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
        AddSubTextRow(3, "Settings", () => { OpenSettings(); _submenu.Enabled = false; });
        AddSubTextRow(4, "Exit", () => { _submenu.Enabled = false; if (_save != null) ToChars(); else Exit(); });
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
        (string file, string action, HotkeyAction? hotkey)[] keys = new (string, string, HotkeyAction?)[] { ("hud_key_ctrl.png", "Attack", null), ("hud_key_alt.png", "Jump", null), ("hud_key_i.png", "Items", HotkeyAction.Inventory), ("hud_key_c.png", "Stats", HotkeyAction.Stat) }; // official key art top-left; the rebindable ones show the cap of the current binding
        KeyConfigWindow.LoadCaps(this);
        for (int i = 0; i < keys.Length; i++)
        {
            var s = QslotCell(i);
            var file = keys[i].file; // the loop variable is shared: copy it for the lambda
            var cell = new GameObject(); // NoHit rides the qslot parent (Hit skips the whole subtree)
            cell.Add(new Rect(s));
            if (keys[i].hotkey is HotkeyAction hk) cell.Add(new Invoke(gfx => DrawQuickslotCap(gfx, s, hk, file)));
            else cell.Add(new Picture("ui/" + file, 2, 2));
            var a = _ui.GetSmall(keys[i].action);
            cell.Add(new Label(keys[i].action, (s.Width - a.Width) / 2, 2 + Tex(file).Height) { Tint = Color.Gray });
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
        minimap.Add(new Minimap(() => _map, () => _mover.X, () => _mover.Y));
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

    // One art button (the art has normal, hover and pressed states).
    internal static GameObject ArtButton(Rectangle r, string stem, Action onClick)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Image(stem));
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

    // Submenu popup: 3-slice bg sized by its own caps, five 109x26 rows inside.
    private const int SubPopupW = 121;
    private const int SubRows = 5; // stat, inventory, quest, settings, exit
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

    private Point _questPos = new(0, 0); // quest window opens centered-ish, drags after
    private Point _settingsPos = SettingsLayoutPos(); // settings window opens centered-ish, drags after
    private Point _shopPos = new(Layout.Shop.X, Layout.Shop.Y); // shop window opens centered-ish, drags after

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
        DrawWindows(); // the new windows (minimap, world map, key settings) draw above the older panels
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

    // The quick slot shows the official cap of the key the action has now
    // (the player can rebind it in the key settings window).
    private void DrawQuickslotCap(UiGfx gfx, Rectangle s, HotkeyAction action, string fallback)
    {
        string cap = KeyConfigWindow.CapArt(HotkeyKey(action));
        var tex = gfx.Tex(cap != "" ? cap : fallback);
        gfx.Sb.Draw(tex, new Vector2(s.X + 2, s.Y + 2), Color.White);
    }

    // Solid backing with rounded corners (the art's own radius is ~3px, measured).
    internal Texture2D RoundedBacking(int w, int h, Color color, int r)
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

}
