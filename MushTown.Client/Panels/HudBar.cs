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
    // Must match the exporter: the HUD names come from ExportHud. Every
    // name here must be exported, or LoadTexture throws. The quick slot caps
    // and the action icons load when a cell draws (QuickCell).
    private static readonly string[] HudFiles = new[]
    {
        "hud_plate.png", "hud_lvtag.png",
        "hud_hpbg.png", "hud_hpcover.png", "hud_hpfill.png", "hud_mpfill.png",
        "hud_expback.png", "hud_expcover.png", "hud_expgauge.png",
        "hud_qslot.png",
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
    private Scene _uiScene = new(); // one tree, ordered by Z (a reload makes a fresh one); not null from the start: shots raise windows before the HUD is built
    private readonly GameObject _invHost = new(), _statHost = new(), _skillHost = new();
    private readonly GameObject _questHost = new(), _settingsHost = new(), _shopHost = new();
    private readonly GameObject[] _panelHosts; // panel-id order: 0 inventory .. 5 shop (filled by the constructor)
    private readonly CarryHand _hand = new(); // game-level: a panel rebuilds its content, the held piece survives
    private readonly GameObject _talkHost = new() { Modal = true, Z = TalkZ }; // the NPC dialog: one root carries the modal flag
    private bool _invBuilt, _statBuilt, _skillBuilt, _questBuilt, _settingsBuilt, _shopBuilt; // built at least once
    private int _invBuiltTab = -1, _skillBuiltTab = -1; // baked Rects follow tab flips (bag counts are static)
    private int _shopBuiltTab = -1; // the shop tab in the drawn scene
    private List<int> _questBuiltRows = new();
    private List<int> _shopBuiltStock = new();
    private List<(int id, int count)> _shopBuiltSells = new();
    private int _talkBuiltNpc, _talkBuiltQuest, _talkBuiltPage;
    private string _talkBuiltButtons = "", _talkBuiltEntries = "";
    private UiGfx _gfx = null!;
    private bool _contentLoaded; // the scene build needs textures (popup height reads them)
    private readonly Dictionary<int, ShopData> _shops = new(); // keeper stocks (LoadShops)
    private List<int> _shopStock = null!; // the open shop's stock (OpenShop snapshots it: talk state clears)
    private int _invTab; // 0 Equip 1 Use 2 Etc 3 Set-up 4 Cash 5 Dec

    private static Rectangle PlateRect() => new(Layout.Plate.X, Layout.Plate.Y, 250, 35);
    private static Rectangle HpRect() => new(Layout.HpMp.X, Layout.HpMp.Y, 205, 46); // the bars art: 46 tall, its last 3px overlap the EXP band (the bars draw over it)
    private static Rectangle QslotRect() => new(Layout.Qslot.X, Layout.Qslot.Y, 557, 67);
    private static Rectangle ExpRect() => new(0, 758, 1366, 10);
    private Point _invPos = new(0, 0), _statPos = new(0, 0);
    private Point _skillPos = new(0, 0); // the skill window takes its spot from layouts/panel-skill.json
    private Point? _dragInv, _dragStat, _dragSkill, _dragQuest, _dragSettings, _dragShop; // title grab offsets while held
    private readonly List<GameObject> _raiseRow = new(); // every layer that can be raised (panels and windows): back to front
    // The Z bands: every layer keeps its own range, so one int decides draw and hit.
    private const int BalloonZ = -10;
    private const int HudTopZ = 100; // above the HUD roots, under the raise row
    private const int RaiseZ = 200; // panels and windows share one row: the last raised draws last
    private const int TalkZ = 500; // the NPC dialog: above every window, under the hand and the tips
    private const int HeldZ = 600;
    private const int TipZ = 700; // the last thing drawn: no layer can cover a tip

    // The UI actions the HUD consults, in one fixed order.
    private static readonly HotkeyAction[] UiHotkeyOrder =
    {
        HotkeyAction.Inventory, HotkeyAction.Stat, HotkeyAction.Equip, HotkeyAction.Skill,
        HotkeyAction.Quest, HotkeyAction.Settings, HotkeyAction.WorldMap,
        HotkeyAction.Party, HotkeyAction.KeyConfig, HotkeyAction.Guide, HotkeyAction.Menu,
        HotkeyAction.Chat, HotkeyAction.Minimap,
    };

    private void HandleHotkey(HotkeyAction action, KeyboardState kb)
    {
        switch (action)
        {
            case HotkeyAction.Inventory: TogglePanel(Feature.Inventory); break;
            case HotkeyAction.Stat: QuickFeature(Feature.Stat); break; // the other page of the stat window switches; the same page closes
            case HotkeyAction.Equip: QuickFeature(Feature.Equip); break;
            case HotkeyAction.Skill: TogglePanel(Feature.Skill); break;
            case HotkeyAction.Quest: TogglePanel(Feature.Quest); break;
            case HotkeyAction.Settings: TogglePanel(Feature.Settings); break;
            case HotkeyAction.Chat: _chatWindow.FocusInput(kb); break; // the input line takes the keys from here
            case HotkeyAction.Minimap: ShowWindow(HotkeyAction.Minimap); break; // always on the screen: a key only brings it back
            case HotkeyAction.KeyConfig:
                if (KeyWindowOpen) ToggleWindow(HotkeyAction.KeyConfig);
                else OpenKeyWindow();
                break;
            default: ToggleWindow(action); break;
        }
    }

    private Texture2D Tex(string file) => _textures["ui/" + file];

    private void UpdateHud(KeyboardState kb, bool click, bool held, int gx, int gy)
    {
        var hoverSlot = UpdateHudHover(gx, gy); // every frame (clicks return early below)
        _hoverItem = hoverSlot != null;
        MenuKeys(kb); // the open MENU window takes the arrows, Tab and Enter
        // The key window does not block anything: the character keeps moving
        // and fighting on the saved preset, the item keys keep drinking, and
        // every panel key stays alive, so a skill can be dragged onto a key.
        // The draft only reaches the live keys on Save.
        var keyWin = WindowFor(HotkeyAction.KeyConfig) as KeyConfigWindow;
        bool keyWindowOpen = KeyWindowOpen;
        if (keyWindowOpen) keyWin!.UpdateDrag(held, gx, gy); // the drop reads the same mouse edge the HUD uses
        ItemEdges(_talkNpc == 0 && !_menuWindow.Show); // the item keys drink their potion
        if (_talkNpc == 0)
        {
            bool any = false;
            foreach (var action in UiHotkeyOrder)
            {
                if (!_input.Fresh(action)) continue;
                HandleHotkey(action, kb);
                any = true;
                if (action != HotkeyAction.Menu) _menuWindow.Close();
            }
            if (any) PlaySfx("click");
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
            else _shopPos = ClampPanel(new Point(gx - _dragShop.Value.X, gy - _dragShop.Value.Y), ShopW, ShopH);
        }
        SyncPanels(); // tab flips and new rows rebuild (a drag only moves the host; hover ran first, so it lags one frame, like legacy)
        DragWindows(held, gx, gy); // the new windows move by their title
        _worldMapWindow.UpdateWheel(); // the wheel scrolls the map list: before the sync, so the rows land this frame
        SyncWindows(); // a window rebuilds when its state changed (a drag only moves the root)
        if (!click) return;
        UpdateHudClick(gx, gy); // click sounds play by Button Kind inside ClickTree (the flag funnel is gone)
    }

    // The panel a feature opens, or -1 when the feature opens a window or runs an action.
    private static int PanelId(Feature feature) => feature switch
    {
        Feature.Inventory => 0,
        Feature.Stat or Feature.Equip => 1, // the two icons share the stat window
        Feature.Skill => 2,
        Feature.Quest => 3,
        Feature.Settings => 4,
        _ => -1,
    };

    // A quick icon or a panel key: open on top, raise when behind, close when in front.
    private void TogglePanel(Feature feature)
    {
        int id = PanelId(feature);
        if (!PanelOn(id)) { OpenFeature(feature); return; }
        if (FrontHost() == _panelHosts[id]) { HidePanel(id); return; }
        RaisePanel(id);
    }

    // Is this panel open? The order is the panel-id order.
    private bool PanelOn(int id) => id switch
    {
        0 => _showInv,
        1 => _showStat,
        2 => _showSkill,
        3 => _showQuest,
        4 => _showSettings,
        5 => _showShop,
        _ => false,
    };

    // The front layer of the raise row: the last one that is on screen.
    private GameObject? FrontHost()
    {
        for (int i = _raiseRow.Count - 1; i >= 0; i--)
        {
            var host = _raiseRow[i];
            int id = PanelOf(host);
            if (id >= 0) { if (PanelOn(id)) return host; continue; }
            if (WindowOf(host) is UiWindow w && w.Show) return host;
        }
        return null;
    }

    // Hide one panel; hiding mid-drag drops the grab.
    private void HidePanel(int i)
    {
        switch (i)
        {
            case 0: _showInv = false; _dragInv = null; break;
            case 1: _showStat = false; _dragStat = null; break;
            case 2: CloseSkillWindow(); break;
            case 3: _showQuest = false; _dragQuest = null; break;
            case 4: CloseSettings(); break; // the one close path for the options window
            case 5: _showShop = false; _dragShop = null; break;
        }
    }

    // One "over" per frame (clicks live in UpdateHudClick below): the tree
    // answers with its topmost object, so a covered panel stays dark.
    // Returns the hovered inventory slot (it decides the grab cursor).
    private Slot? UpdateHudHover(int gx, int gy)
    {
        SfxOnHover(_uiScene.Hover(gx, gy)); // one tree: the topmost object by Z takes the hover (the modal blanks the rest)
        return _uiScene.HoverButton as Slot; // the bag is the only panel with slots: that is the grab cursor
    }

    private void UpdateHudClick(int gx, int gy)
    {
        // What the click is about to hit, read before any button runs: a
        // button may rebuild its own layer and detach the hit object.
        var top = _uiScene.TopAt(gx, gy);
        if (_talkNpc == 0)
        {
            int panel = PanelOf(top);
            if (WindowOf(top) is UiWindow w && !Pinned(w)) Raise(w); // a window comes to the front, then its button runs
            else if (panel >= 0) { RaisePanel(panel); } // a panel too: windows and panels share the row
        }
        var hit = ClickTree(_uiScene, gx, gy); // one tree: the topmost object by Z takes the click
        if (hit == null && _talkNpc == 0) NpcClick(gx, gy); // no UI took it: the world gets the click (the open dialog swallows it)
    }

    // A click or Esc changed what is open: the draw runs right after Update,
    // so the new state has to be in the tree this frame (a ghost frame shows).
    // The only two sync points: UpdateHud (after the hover, before the click,
    // so the hit test sees this frame's rects) and this one, which catches
    // what changed after it (net, map, death) and the shots, which never run
    // Update. A third, deliberate early write lives where the walk-away closes
    // the dialog: the hover and the click of that frame read the modal first.
    private void SyncLayers()
    {
        SyncPanels();
        SyncWindows();
    }

    // The panel id a hit object belongs to, or -1 (HUD, window, world).
    private int PanelOf(GameObject? o)
    {
        var root = RootOf(o);
        return root == null ? -1 : Array.IndexOf(_panelHosts, root);
    }

    // The scene root a hit object belongs to (null = no hit).
    private static GameObject? RootOf(GameObject? o)
    {
        while (o?.Parent != null) o = o.Parent;
        return o;
    }

    // Panels stay reachable: 80px on screen sideways, title never above the top.
    // A panel taller than the screen (the shop) keeps its whole body on screen.
    private static Point ClampPanel(Point p, int w, int h = 40) =>
        new(Math.Clamp(p.X, -w + 80, 1366 - 80), Math.Clamp(p.Y, 0, 768 - h));

    private Texture2D TexByKey(string key) => _textures[key]; // full keys ("ui/..."): all screens share one lookup

    // The one UI tree: HUD furniture, exp, plate, bars, quick slots,
    // tracker, buffs, boss bar, quick menu, then the panel and window
    // hosts, the dialog, the hand and the tips. Built after textures
    // load, rebuilt on layout reload.
    private void BuildGameScene()
    {
        InstallInput();
        _uiScene = new Scene(); // a reload starts over: no stale hover, click or root list
        var balloon = new GameObject { NoHit = true, Z = BalloonZ }; // the speech bubble over the character: under every HUD piece
        balloon.Add(MakeBalloon());
        _uiScene.Add(balloon);
        var furniture = new GameObject(); // HUD furniture: hit-rects with no Buttons (clicks stop here, silent)
        furniture.AddChild(HitBox(PlateRect()));
        var hp = HitBox(HpBox()); // the bars split at the line of their own numbers
        hp.Add(new HoverTip(() => "HP (Health Point)"));
        furniture.AddChild(hp);
        var mp = HitBox(MpBox()); // it stops where the EXP line starts, or the EXP object would win there
        mp.Add(new HoverTip(() => "MP (Mana Point)"));
        furniture.AddChild(mp);
        furniture.AddChild(HitBox(QslotRect()));
        _uiScene.Add(furniture); // Z 0, first: the art roots added after draw over it, its rects swallow the clicks
        var exp = new GameObject(); // the EXP line: the hit is this object (its rect), the tip rides it
        exp.Add(new Rect(ExpRect()));
        exp.Add(new Bar("ui/hud_expback.png", "ui/hud_expgauge.png", "ui/hud_expcover.png", ExpFill));
        exp.Add(new HoverTip(ExpTipText, above: true));
        _uiScene.Add(exp); // the EXP line: same pixels as the legacy draw, owned by the tree now
        var plate = new GameObject { NoHit = true }; // art-only roots below: the furniture keeps swallowing plate/HP/quickslot and the exp object its own line; tracker/boss/minimap pass clicks through like before
        plate.Add(new Rect(PlateRect()));
        plate.Add(new Picture("ui/hud_plate.png"));
        plate.Add(new PlateLevel(() => _save?.Slots[_slot]?.Level ?? 1));
        plate.Add(new Picture("ui/hud_lvtag.png", 8, 13));
        plate.Add(new LiveLabel(() => _save?.Slots[_slot]?.Name ?? "", 67, 13, Color.White)); // WZ namePos; CLI sessions have no name (empty draws nothing)
        _uiScene.Add(plate);
        var hpmp = new GameObject { NoHit = true };
        hpmp.Add(new Rect(HpRect()));
        hpmp.Add(new Picture("ui/hud_hpbg.png"));
        hpmp.Add(new Picture("ui/hud_hpfill.png", 4, 4)); // fills at bg+(4,4)/(4,25), cover at bg+(3,3), numbers centered on x+101. Bars always full: nothing drains HP/MP until W13 mob hits (W13 adds the clip then).
        hpmp.Add(new Picture("ui/hud_mpfill.png", 4, 25));
        hpmp.Add(new BarNumber(HpText, 101, 7));
        hpmp.Add(new BarNumber(MpText, 101, 28));
        hpmp.Add(new Picture("ui/hud_hpcover.png", 3, 3));
        _uiScene.Add(hpmp);
        var qslot = new GameObject { NoHit = true };
        qslot.Add(new Rect(QslotRect()));
        qslot.Add(new Picture("ui/hud_qslot.png"));
        _uiScene.Add(qslot); // the strip: the plain official cells, no gloss layer (the official cover is a second layer that has to keep its own 562x73 size and -2,-3 spot; the plain cells are the look we want)
        _uiScene.Add(BuildQuickSlots()); // the sixteen key cells of the same strip
        _uiScene.Add(BuildQuestHelper()); // the quest helper bar: the count, the buttons, the quest list
        _uiScene.Add(BuildBuffBar()); // the buff bar: the live buffs, top right
        var bossBar = new GameObject { NoHit = true };
        bossBar.Add(new BossBar(BossBarW, FindBoss));
        _uiScene.Add(bossBar);
        _uiScene.Add(BuildQuickMenu()); // the quick menu icon row: the top of the HUD band, so the clicks land there
        foreach (var host in _panelHosts) _uiScene.Add(host); // the panels ride the same tree: their Z is the whole order
        foreach (var w in UiWindow.List) { w.Scene = _uiScene; _uiScene.Add(w.Host); } // the windows too: one tree, one Z order
        _uiScene.Add(_talkHost); // the modal: clicks outside it blank in Scene.Hit
        var held = new GameObject { NoHit = true, Z = HeldZ }; // the piece on the cursor: above the modal, under the tips
        held.Add(new HeldItem(HeldArt));
        _uiScene.Add(held);
        var tips = new GameObject { NoHit = true, Z = TipZ }; // the last thing drawn: no layer can cover a tip
        tips.Add(new Tips(() => _hand.Empty, _items, _invLay, _statLay));
        _uiScene.Add(tips);
        SyncLayers(); // shots skip Update: without this the panels and windows draw empty there (first Update no-ops)
    }

    private static GameObject HitBox(Rectangle r)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        return o;
    }

    // The HP/MP hit boxes: the numbers sit at +7 and +28 of the plate, and the
    // bottom stops at the EXP line (y 758).
    private static Rectangle HpBox() => new(Layout.HpMp.X, Layout.HpMp.Y, 205, 24);
    private static Rectangle MpBox() => new(Layout.HpMp.X, Layout.HpMp.Y + 24, 205, 19);

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

    // The raise row: panels and windows, back to front. The row index is the
    // whole Z of that band, so one list decides draw and hit.
    private void RenumberRow()
    {
        for (int i = 0; i < _raiseRow.Count; i++) SetRootZ(_raiseRow[i], RaiseZ + i);
    }

    // Put a layer on top of the row (a click, a hotkey or a window opening).
    private void RaiseHost(GameObject host)
    {
        _raiseRow.Remove(host);
        _raiseRow.Add(host);
        RenumberRow();
    }

    // Repoint a root only when its Z moved: every Move is a remove + insert.
    private void SetRootZ(GameObject host, int z)
    {
        if (host.Z != z) _uiScene.Move(host, z);
    }

    // A click or a hotkey opened a panel: it goes to the front of the row, and
    // the MENU closes (it would stay open behind it and keep the keys).
    private void RaisePanel(int id)
    {
        if (_menuWindow.Show) _menuWindow.Close();
        RaiseHost(_panelHosts[id]);
    }

    // Panel trees are pure functions of (tab, contents): a drag only moves the
    // host, so only a tab flip or new rows rebuilds. Bag counts are static
    // catalogs, so slots never reshape. Also the dialog root: its visibility
    // follows the talk state.
    private void SyncPanels()
    {
        _invHost.Enabled = _showInv; _statHost.Enabled = _showStat; _skillHost.Enabled = _showSkill; // a hidden panel stays in the tree, it just stops drawing and hitting
        _questHost.Enabled = _showQuest; _settingsHost.Enabled = _showSettings; _shopHost.Enabled = _showShop;
        _talkHost.Enabled = _talkNpc != 0; // the dialog root is in the tree: showing it is all it takes
        if (!_invBuilt || _invTab != _invBuiltTab) { BuildInvScene(); _invBuilt = true; _invBuiltTab = _invTab; }
        if (!_statBuilt) { BuildStatScene(); _statBuilt = true; }
        if (!_skillBuilt || _skillTab != _skillBuiltTab) { BuildSkillScene(); _skillBuilt = true; _skillBuiltTab = _skillTab; }
        if (_showQuest) // the rows cost a list each call, and a hidden panel is not drawn: skip it
        {
            var quests = QuestRows();
            if (!_questBuilt || !quests.SequenceEqual(_questBuiltRows)) { BuildQuestScene(quests); _questBuilt = true; _questBuiltRows = quests; }
        }
        if (!_settingsBuilt) { BuildSettingsScene(); _settingsBuilt = true; }
        if (_showShop && _shopStock != null)
        {
            var sells = ShopSellRows();
            if (!_shopBuilt || !ReferenceEquals(_shopStock, _shopBuiltStock) || !sells.SequenceEqual(_shopBuiltSells) || _shopTab != _shopBuiltTab)
            { BuildShopScene(sells); _shopBuilt = true; _shopBuiltStock = _shopStock; _shopBuiltSells = sells; _shopBuiltTab = _shopTab; }
        }
        if (_talkNpc != 0) // gated: TalkButtons needs an open talk (same exposure as the legacy chain)
        {
            string buttons = string.Join(",", TalkButtons());
            string entriesKey = _talkQuest == 0 ? string.Join(",", TalkEntries(_talkNpc).Select(e => e.quest)) : ""; // offers are level-gated: ids, not count (LvMax can swap at the same count)
            if (_talkNpc != _talkBuiltNpc || _talkQuest != _talkBuiltQuest || _talkPage != _talkBuiltPage || buttons != _talkBuiltButtons || entriesKey != _talkBuiltEntries)
            { BuildTalkScene(); _talkBuiltNpc = _talkNpc; _talkBuiltQuest = _talkQuest; _talkBuiltPage = _talkPage; _talkBuiltButtons = buttons; _talkBuiltEntries = entriesKey; }
        }
        // The spots go on the hosts last: a build may clamp or place its panel.
        PlaceHost(_invHost, _invPos, InvW, InvH);
        PlaceHost(_statHost, _statPos, StatW, StatH);
        PlaceHost(_skillHost, _skillPos, SkillW, SkillBodyH);
        PlaceHost(_questHost, _questPos, QuestW, QuestBodyH);
        PlaceHost(_settingsHost, _settingsPos, SettingsW, _settingsLay.Get("titleH", 16) + SettingsBodyH);
        PlaceHost(_shopHost, _shopPos, ShopW, ShopH);
    }

    private static GameObject PanelButton(Rectangle r, string id, Action onClick, ButtonKind kind = ButtonKind.Silent)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        o.Add(new Button { LegacyId = id, Kind = kind, OnClick = onClick });
        return o;
    }

    // The host rect carries the panel's spot on the screen: a drag repoints it
    // and the built body rides along, so nothing is rebuilt for a move.
    private static void PlaceHost(GameObject host, Point pos, int w, int h) => UiWindow.Place(host, pos, new Point(w, h));

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

    private Point _questPos = new(0, 0); // quest window opens centered-ish, drags after
    private Point _settingsPos = SettingsLayoutPos(); // settings window opens centered-ish, drags after
    private Point _shopPos = new(0, 0); // the shop window opens at the spot in panel-shop.json, drags after

    private void DrawHud(int gx, int gy, bool pressed)
    {
        _gfx.Pressed = pressed;
        _gfx.Mouse = new Point(gx, gy); // the held piece and the tips ride this
        _uiScene.Draw(_gfx); // one tree, one Z order: balloon, HUD, panels, windows, dialog, hand, tips
    }

    // The art of the piece in the hand, "" when the hand is empty.
    private string HeldArt() => _hand.Empty ? "" : _items[_hand.Id].Image;

    // The EXP tip has the numbers: the official writes them as
    // "EXP : 116 / 19,112 [0.61%]".
    private string ExpTipText()
    {
        var (value, max) = ExpFill();
        double pct = value * 100.0 / max;
        return $"EXP : {value:#,##0} / {max:#,##0} [{pct:0.00}%]";
    }

    private static Rectangle QslotCell(int i) => new(QslotRect().X + i * 35, QslotRect().Y, 32, 32); // the official cells: 32x32, pitch 35

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
