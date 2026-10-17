using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// Lobby: saves -> server -> characters -> job -> setup -> game.
// Art is official modern UI from assets/ui (normal states only, hover is a
// white overlay); every string is baked white and tinted at draw time.
public partial class MushTownGame
{
    private enum Screen { Saves, Server, Chars, Job, Setup, Game }

    private Screen _screen = Screen.Game;
    private SaveStore _store = new();
    private string _savesPath = "";
    private SaveData? _save;
    private int _saveSlot;
    private int _slot;
    private string? _hostPick; // null = this PC (host here), else the LAN IP to join
    private Task<List<FoundHost>>? _scanTask; // a running LAN scan (UpdateServer collects it; shots never start one)
    private List<FoundHost> _hosts = new();
    private bool _scanWasDown;
    private bool _jobPicked;
    private Scene _menuScene = null!; // rebuilt on every menu entry (rows/slots follow live data)
    private GameObject _csDeleteModal = null!; // delete root; Enabled is the open flag
    private GameObject _pager = null!; // pager container (enter-select yields to it)
    private string _focus = "";
    private string _name = "";
    private string _error = "";
    private bool _mouseWasDown, _enterWasDown, _escWasDown, _backWasDown;
    private float _backTimer;
    private UiText _ui = null!;
    private Texture2D _pixel = null!;
    private RollLog? _rollLog;
    private readonly List<string> _logLines = new();
    private readonly object _logLock = new();
    private bool _panelOpen;
    private Texture2D? _panelTex;
    private GameObject _logRoot = null!; // bare root (built in InitializeMenu): DrawLogOverlay draws it in menu and game
    private bool _panelDirty = true;
    private int _page; // char-select page 0-4 (12 slots each)
    private int _walkFrame;
    private float _walkT;
    private int _walkSlot = -1; // walk restarts when the selection moves
    private UiClip _ghost = null!;
    private UiClip _portal = null!;
    private UiClip _motes = null!;
    private MouseCursor _cursorNormal = null!; // scaled state 0 (never overwritten: release restores it)
    private MouseCursor _cursorAnim = null!; // latest anim frame (kept alive while shown)
    private CursorData _cursorHot = null!; // unscaled hotspot (SyncCursor scales it with the pixels)
    private readonly CursorSet _hoverSet = new(); // WZ state 1 (button hover blink)
    private readonly CursorSet _clickSet = new(); // WZ state 12 (pressed hand)
    private readonly CursorSet _itemSet = new(); // WZ state 5 (inventory grab)
    private Texture2D? _cursorTex; // scaled cursor pixels (the _textures source stays 24x28)
    private int _cursorH; // backbuffer height the OS cursor was last built for (0 = not built)
    private readonly CursorAnim _cursor = new(); // the brain: state + frame (the OS hands stay below)
    private bool _hoverItem; // the mouse sits on an inventory slot (no sfx: the cursor alone answers)
    private string _hoverId = ""; // last hovered button id (hover sfx plays on change)

    // One looping overlay clip: frames plus their player state.
    private sealed class UiClip
    {
        public UiFramesData Data = new();
        public int Frame;
        public float Timer;
    }

    // One animated cursor set: WZ frames plus their scaled textures.
    private sealed class CursorSet
    {
        public CursorAnimData Data = new();
        public readonly List<Texture2D> Tex = new();
        public readonly List<Point> Hot = new();
    }

    private static readonly Color Ink = new(50, 50, 50); // dark text on white art
    private static readonly Color Hint = new(153, 153, 153); // official hint gray
    private static readonly Color ErrRed = new(220, 60, 50);
    private static readonly Color ListGray = new(140, 149, 153); // official font:charNumNormal #8C9599
    private static readonly Color MenuInk = new(43, 8, 5); // baked button text median, sampled off btn_astra

    // Must match Export.cs UiFiles: every name here must be exported, or LoadTexture throws.
    // Char-select buttons carry real 3-state art (_n/_h/_p); frame sets
    // (cs_ghost/cs_portal/cs_motes) load through LoadUiClip instead.
    private static readonly string[] UiFiles = new[]
    {
        "worldbg.png",
        "selectworld.png",
        "cs_select_n.png", "cs_select_h.png", "cs_select_p.png",
        "cs_new_n.png", "cs_new_h.png", "cs_new_p.png",
        "cs_delete_n.png", "cs_delete_h.png", "cs_delete_p.png",
        "cs_back_n.png", "cs_back_h.png", "cs_back_p.png",
        "cs_delyes_n.png", "cs_delyes_h.png", "cs_delyes_p.png",
        "cs_delno_n.png", "cs_delno_h.png", "cs_delno_p.png",
        "cs_board.png", "cs_pillar.png", "cs_page_bg.png", "cs_page_base.png", "cs_delglass.png",
        "cs_page_l_n.png", "cs_page_l_h.png", "cs_page_l_p.png",
        "cs_page_r_n.png", "cs_page_r_h.png", "cs_page_r_p.png",
        "cs_page_1_n.png", "cs_page_1_h.png", "cs_page_1_p.png",
        "cs_page_2_n.png", "cs_page_2_h.png", "cs_page_2_p.png",
        "cs_page_3_n.png", "cs_page_3_h.png", "cs_page_3_p.png",
        "cs_page_4_n.png", "cs_page_4_h.png", "cs_page_4_p.png",
        "cs_page_5_n.png", "cs_page_5_h.png", "cs_page_5_p.png",
        "listbg.png", "cell_hero.png", "btn_make.png",
        "setupbg.png", "btn_confirm.png", "btn_cancel.png",
    };

    // Wooden sign art (tracked art/menu/*.png, like custom quests/): the menu
    // board plus its pill buttons. Missing file fails loud, same as exports.
    private static readonly string[] MenuFiles = new[]
    {
        "sign_top.png", "sign_mid.png", "sign_bot.png",
        "btn_blank.png", "btn_exit.png", "btn_log.png",
        "btn_astra.png", "btn_blackveil.png", "btn_crimson.png",
    };

    private Texture2D MenuTex(string file) => _textures["menu/" + file];

    // Wooden sign board (saves + server): a header sign, a stretchable cream
    // body, a mushroom base. Rows are baked pill buttons with gaps between;
    // the Exit and Cancel plaques match each other. All numbers live in
    // layouts/menu-layout.json (DEBUG builds reload it live); code only centers.
    private static readonly string[] SaveNames = { "Astra", "Blackveil", "Crimson" };
    private static readonly string[] SaveBtnFiles = { "btn_astra.png", "btn_blackveil.png", "btn_crimson.png" };
    private int SaveRowH(int i) => MenuTex(i < SaveStore.MaxSaves ? SaveBtnFiles[i] : "btn_exit.png").Height;
    private int SaveRowsH()
    {
        int t = 0;
        for (int i = 0; i < SaveRows(); i++) t += SaveRowH(i);
        return t + MenuLayout.RowGap * (SaveRows() - 1);
    }
    private int ServerRowsH(int rows) => rows * MenuLayout.ServerRowH + MenuLayout.RowGap * (rows - 1);
    private int SaveBodyH() => MenuLayout.BodyPadTop + SaveRowsH() + MenuLayout.BodyPadBottom + MenuLayout.MiddleExtraH;
    private int ServerBodyH(int rows) => MenuLayout.BodyPadTop + ServerRowsH(rows) + MenuLayout.BodyPadBottom + MenuLayout.MiddleExtraH;
    private int PanelTop(int bodyH) => (GameHeight - (MenuTex("sign_top.png").Height + bodyH + MenuTex("sign_bot.png").Height - MenuLayout.OverlapTop - MenuLayout.BotUp)) / 2;
    private int BodyY(int bodyH) => PanelTop(bodyH) + MenuTex("sign_top.png").Height - MenuLayout.OverlapTop;
    private int PanelBottom(int bodyH) => BodyY(bodyH) + bodyH - MenuLayout.BotUp + MenuTex("sign_bot.png").Height;
    // Menu space shrinks toward the panel center: layout math above runs
    // full-size (the file's numbers), every rect below comes out scaled.
    private float MS => MenuLayout.Scale;
    private int MSS(int v) => (int)(v * MS);
    private int MSX(int x) => GameWidth / 2 + (int)((x - GameWidth / 2) * MS);
    private int PanelTopS(int bodyH) => (GameHeight - (int)((MenuTex("sign_top.png").Height + bodyH + MenuTex("sign_bot.png").Height - MenuLayout.OverlapTop - MenuLayout.BotUp) * MS)) / 2;
    private int MSY(int y, int bodyH) => PanelTopS(bodyH) + (int)((y - PanelTop(bodyH)) * MS);
    private Rectangle MSR(Rectangle r, int bodyH) => new(MSX(r.X), MSY(r.Y, bodyH), MSS(r.Width), MSS(r.Height));
    private int PanelBottomS(int bodyH) => MSY(PanelBottom(bodyH), bodyH);
    private Rectangle SaveRowRect(int i)
    {
        int bodyH = SaveBodyH();
        int y = BodyY(bodyH) + (bodyH - SaveRowsH()) / 2; // rows stay centered when the middle grows
        for (int k = 0; k < i; k++) y += SaveRowH(k) + MenuLayout.RowGap;
        var t = MenuTex(i < SaveStore.MaxSaves ? SaveBtnFiles[i] : "btn_exit.png");
        return MSR(new Rectangle((GameWidth - t.Width) / 2, y, t.Width, t.Height), bodyH);
    }
    private Rectangle ServerRowRect(int i, int rows)
    {
        int bodyH = ServerBodyH(rows);
        return MSR(new Rectangle((GameWidth - MenuLayout.ServerRowW) / 2,
            BodyY(bodyH) + (bodyH - ServerRowsH(rows)) / 2 + i * (MenuLayout.ServerRowH + MenuLayout.RowGap),
            MenuLayout.ServerRowW, MenuLayout.ServerRowH), bodyH);
    }

    // Char select in game pixels (1366x768), shared by click and draw code.
    // All numbers live in layouts/charselect-layout.json (DEBUG builds reload it
    // live); code only centers. Button sizes come from the art.
    private const int PageSlots = 12;
    private const int Pages = 5;
    private static (int fx, int fy) SlotFeet(int cell) =>
        cell < 6 ? (CharLayout.SlotTopX[cell], CharLayout.SlotTopY) : (CharLayout.SlotBotX[cell - 6], CharLayout.SlotBotY);
    private static Rectangle SlotRect(int cell)
    {
        var (fx, fy) = SlotFeet(cell);
        return new Rectangle(fx - CharLayout.SlotW / 2, fy - CharLayout.SlotH, CharLayout.SlotW, CharLayout.SlotH);
    }
    private Rectangle UiRect(string file, int x, int y)
    {
        var t = _textures["ui/" + file];
        return new Rectangle(x, y, t.Width, t.Height);
    }
    private static Rectangle WorldPillRect() => new(CharLayout.Pill.X, CharLayout.Pill.Y, 200, 57);
    private Rectangle BoardRect() => UiRect("cs_board.png", CharLayout.Board.X, CharLayout.Board.Y);
    private Rectangle SelectBtnRect() => UiRect("cs_select_n.png", CharLayout.Select.X, CharLayout.Select.Y);
    private Rectangle NewBtnRect() => UiRect("cs_new_n.png", CharLayout.New.X, CharLayout.New.Y);
    private Rectangle DeleteBtnRect() => UiRect("cs_delete_n.png", CharLayout.Delete.X, CharLayout.Delete.Y);
    private Rectangle BackBtnRect() => UiRect("cs_back_n.png", CharLayout.Back.X, CharLayout.Back.Y);
    private Rectangle PagerBgRect() => UiRect("cs_page_bg.png", CharLayout.Pager.X, CharLayout.Pager.Y);
    // Pager cells: L, five digits, R. The arrows sit apart in the black
    // bar ends (layout file); digits tile the white bar (~37.6px each).
    // Digit art (30x33) overflows the 26px bar height, like the official look.
    private Rectangle PageLRect() => UiRect("cs_page_l_n.png", CharLayout.PageL.X, CharLayout.PageL.Y);
    private Rectangle PageRRect() => UiRect("cs_page_r_n.png", CharLayout.PageR.X, CharLayout.PageR.Y);
    private Rectangle PageCellRect(int cell)
    {
        var bg = PagerBgRect();
        if (cell == 0) return PageLRect();
        if (cell == 6) return PageRRect();
        // Digits tile the whole bar now the arrows moved out (188/5 = 37.6px each).
        float pitch = bg.Width / 5f;
        int x0 = bg.X + (int)((cell - 1) * pitch);
        return new Rectangle(x0, bg.Y, bg.X + (int)(cell * pitch) - x0, bg.Height);
    }
    private static Rectangle DeletePanelRect() => new(CharLayout.DelPanel.X, CharLayout.DelPanel.Y, CharLayout.DelSize.X, CharLayout.DelSize.Y);
    private Rectangle DeleteYesRect() => UiRect("cs_delyes_n.png", CharLayout.DelYes.X, CharLayout.DelYes.Y);
    private Rectangle DeleteNoRect() => UiRect("cs_delno_n.png", CharLayout.DelNo.X, CharLayout.DelNo.Y);
    private static Rectangle ListBgRect() => new(526, 87, 313, 593);
    private static Rectangle CellRect() => new(554, 147, 152, 74);
    private static Rectangle MakeBtnRect() => new(489, 692, 200, 57);
    private static Rectangle JobCancelRect() => new(709, 699, 167, 43);
    private static Rectangle NameFieldRect() => new(660, 258, 304, 40);
    private static Rectangle ConfirmBtnRect() => new(660, 320, 166, 43);
    private static Rectangle SetupCancelRect() => new(846, 320, 167, 43);
    private static Rectangle LogButtonRect() => new(MenuLayout.Log.X, MenuLayout.Log.Y, 157, 59); // btn_log 373x140 at 0.42
    private static Rectangle LogPanelRect() => new(900, 228, 446, 470);

    // Mode for StartGame: null pick = host here, else join that IP.
    private string ServerMode() => _hostPick == null ? "host" : _hostPick;

    private string ServerLabel() => _hostPick == null ? $"Host {Net.Port}" : _hostPick;

    private void InitializeMenu(string? shotScreen)
    {
        if (SaveNames.Length != SaveStore.MaxSaves || SaveBtnFiles.Length != SaveStore.MaxSaves)
            throw new InvalidDataException("save names/buttons drifted from MaxSaves");
        if (PageSlots * Pages != SaveStore.MaxHeroes)
            throw new InvalidDataException("char pages drifted from MaxHeroes");
        _logRoot = new GameObject();
        _logRoot.Add(new Rect(LogPanelRect()));
        _logRoot.Add(new LogPanel(() => _panelTex));
        string dataDir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "data");
        _savesPath = Path.Combine(dataDir, "saves.json");
        Window.TextInput += (_, e) => MenuTextInput(e.Character);
        if (_shotPath == null && !_host && _joinHost == null) // real menu session only: CLI never touches saves
        {
            _store = SaveStore.Load(_savesPath);
            _rollLog ??= new RollLog(NetLogPath());
            _screen = Screen.Saves; // every launch starts at saves (no session skip)
        }
        if (shotScreen != null) // menu screenshots ride on a demo save
        {
            _store = DemoStore();
            _saveSlot = 0;
            _save = _store.Saves[0];
            _screen = shotScreen switch
            {
                "saves" => Screen.Saves,
                "server" => Screen.Server,
                "chars" or "chars2" or "delete" or "panel" => Screen.Chars,
                "job" => Screen.Job,
                "setup" => Screen.Setup,
                "inv" or "use" or "stat" or "hud" or "skill" or "quest" or "talk" or "settings" or "shop" => Screen.Game,
                _ => Screen.Saves,
            };
            if (shotScreen is "inv" or "use" or "hud") _showInv = true;
            if (shotScreen == "use") _invTab = 1;
            if (shotScreen is "stat" or "hud") _showStat = true;
            if (shotScreen == "skill") { _showSkill = true; _skillTab = _shotSkillTab; }
            if (shotScreen == "quest") { _showQuest = true; _topPanel = 3; }
            if (shotScreen == "settings") { _showSettings = true; _topPanel = 4; }
            if (shotScreen == "chars2") _page = 1; // empty page: the demo fills page 1 only
            if (_screen != Screen.Game) ZeroCamera(); // menu shots draw in screen pixels
            if (shotScreen == "server") _hosts = new List<FoundHost> { new("192.168.1.4", Net.Port, "ACCEYURIKO") };
            _slot = _screen == Screen.Job || _screen == Screen.Setup ? 1 : 0; // job/setup target the empty slot
            if (shotScreen == "chars2") _slot = PageSlots; // empty slot: no start/delete on the empty-page shot
            if (_screen == Screen.Setup) { _name = "newhero"; _focus = "name"; }
            _panelOpen = shotScreen == "panel"; // art shots stay clean; the panel shot pairs with chars (Start sits under it)
            GameLog("MushTown saves menu shot");
        }
    }

    // The shot dummy: Astra holds the Lv.7 hero plus a mage, Blackveil is empty.
    private static SaveStore DemoStore()
    {
        var store = new SaveStore();
        store.Saves[0]!.Slots[0] = DemoChara();
        store.Saves[0]!.Slots[2] = new CharaSlot { Name = "mage", Level = 2, Map = 20000, Hp = Progression.MaxHp(2), Mp = Progression.MaxMp(2) };
        return store;
    }

    // The Lv.7 shot dummy: leveled stats (6 level-ups at +4/+1), half-full
    // EXP bar, full pools, 3 SP spent on Slash Blast (Lv.7 casts Spirit
    // Blade, the only buff with loop art). Plain --skill buff shots (no
    // screen, no account) borrow it too.
    private static CharaSlot DemoChara() => new()
    {
        Name = "hero",
        Level = 7,
        Map = 10000,
        X = 100,
        Y = 200,
        Mesos = 12345,
        Exp = 77,
        Hp = Progression.MaxHp(7),
        Mp = Progression.MaxMp(7),
        Str = 28,
        Dex = 10,
        Sp = 18, // earned 21 (creation 3 + 6 level-ups), spent 3 on Slash Blast
        SkillLv = new Dictionary<int, int> { { SkillData.SlashBlast, 3 } },
        Use = new Dictionary<int, int> { { ItemData.RedPotion, 20 }, { ItemData.BluePotion, 10 } },
        EquipBag = new Dictionary<int, int> { { ItemData.Mop, 1 }, { ItemData.Sergeant, 1 }, { ItemData.BambooRing, 1 }, { ItemData.InkRing, 1 }, { ItemData.IslandRing, 1 } },
    };

    // Item defs (Initialize phase: the doll art loop needs the roster before
    // LoadContent runs). The scan is the roster: every item/*.json loads,
    // consumables (Slot null) slot the USE tab, the rest the EQUIP tab, both
    // in id order. Boot checks fail loud on stale jsons.
    private static void RequireExportDir(string dir)
    {
        if (!Directory.Exists(dir))
            throw new FileNotFoundException(
                $"missing exported dir: {Path.GetFullPath(dir)} (run: dotnet run --project MushTown.Exporter -- export)", dir);
    }

    // Manifest first: the ship list every scan-loaded def set proves against.
    private void LoadManifest()
    {
        _manifest = LoadJson<ManifestData>(Path.Combine(_assetsDir, "manifest.json"));
    }

    private void LoadItemDefs()
    {
        string dir = Path.Combine(_assetsDir, "item");
        RequireExportDir(dir);
        foreach (string f in Directory.GetFiles(dir, "*.json"))
        {
            var item = LoadJson<ItemData>(f);
            _items[item.ItemId] = item;
        }
        _potions = _items.Values.Where(d => d.Slot == null).Select(d => d.ItemId).OrderBy(id => id).ToList();
        _equips = _items.Values.Where(d => d.Slot != null).Select(d => d.ItemId).OrderBy(id => id).ToList();
        foreach (int id in _manifest.Potions.Concat(_manifest.Equips)) // the ship list proves complete: listed but unscanned fails here
            if (!_items.ContainsKey(id))
                throw new InvalidDataException($"manifest lists item {id} with no exported def");
        foreach (var (id, item) in _items)
        {
            if (item.Slot != null) // equips: the slot must hide a beginner img or be a ring; rings name a plate
            {
                if (!ItemData.BeginnerImgs.ContainsKey(item.Slot) && item.Slot != "Ri")
                    throw new InvalidDataException($"equip {id} has a bad slot");
                if (item.Slot == "Ri" && item.NameTag == 0)
                    throw new InvalidDataException($"equip {id} has no nametag");
                if (ItemData.BeginnerImgs.ContainsKey(item.Slot) && !item.HasArt)
                    throw new InvalidDataException($"equip {id} has no art");
            }
            if (item.Price <= 0 && item.NameTag == 0) // stale jsons price 0, except rings (cash gear carries no meso price)
                throw new InvalidDataException($"item {id} has a bad price");
            if (item.Name == "") // pre-name jsons would print blank labels
                throw new InvalidDataException($"item {id} has no name");
        }
    }

    // Drop tables name real items: the server rolls these ids straight into
    // Pickup, where an unknown id would crash, so boot proves them here.
    private void ValidateDropTables()
    {
        string dir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "drops");
        if (!Directory.Exists(dir))
            throw new FileNotFoundException($"missing drop tables: {Path.GetFullPath(dir)} (repo data: restore it from git)", dir);
        foreach (string f in Directory.GetFiles(dir, "*.json"))
        {
            var table = LoadJson<DropData>(f);
            foreach (var d in table.Items)
                if (!_items.ContainsKey(d.Item))
                    throw new InvalidDataException($"drop table {table.MobId} names item {d.Item} with no exported def");
        }
    }

    // Keeper stocks (shops/{npc}.json, repo data): Pio's potions today, more
    // keepers tomorrow. An NPC keeps a shop iff its file exists.
    private void LoadShops()
    {
        string dir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "shops");
        if (!Directory.Exists(dir))
            throw new FileNotFoundException($"missing shops: {Path.GetFullPath(dir)} (repo data: restore it from git)", dir);
        foreach (string f in Directory.GetFiles(dir, "*.json"))
        {
            var shop = LoadJson<ShopData>(f);
            if (shop.Npc == 0)
                throw new InvalidDataException($"shop {f} has no keeper");
            foreach (int id in shop.Stock) // the buy rows index defs by stock id: a missing def fails here
                if (!_items.ContainsKey(id))
                    throw new InvalidDataException($"shop {shop.Npc} stocks {id} with no exported def");
            _shops[shop.Npc] = shop;
        }
    }

    // Skill defs (Initialize phase, same reason). Tabs keep their own roster
    // (window placement + hotkeys); a tabbed skill without a def fails loud
    // instead of drawing blank.
    private void LoadSkillDefs()
    {
        string dir = Path.Combine(_assetsDir, "skill");
        RequireExportDir(dir);
        foreach (string f in Directory.GetFiles(dir, "*.json"))
        {
            var skill = LoadJson<SkillData>(f);
            _skills[skill.SkillId] = skill;
        }
        foreach (var tab in SkillTabs)
            foreach ((int id, string? _) in tab)
                if (!_skills.ContainsKey(id))
                    throw new InvalidDataException($"skill tab lists {id} with no exported def");
        foreach (int id in _manifest.Skills)
            if (!_skills.ContainsKey(id))
                throw new InvalidDataException($"manifest lists skill {id} with no exported def");
        _skillCost = new SkillCost(_skills);
        _skillDmg = new SkillDamage(_skills);
    }

    private void LoadMenuContent()
    {
        _ui = new UiText(GraphicsDevice, Path.Combine(_assetsDir, "fonts"));
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        foreach (string f in UiFiles) LoadTexture("ui/" + f); // missing file fails loud here
        string selectDir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "art", "back");
        string selectBg = Path.Combine(selectDir, "charselect.png");
        if (!File.Exists(selectBg)) // select backdrop: the user's fig2 crop, tracked in art/ (not exporter output)
            throw new FileNotFoundException($"missing select art: {Path.GetFullPath(selectBg)}", selectBg);
        using (var stream = File.OpenRead(selectBg))
            _textures["back/charselect.png"] = Texture2D.FromStream(GraphicsDevice, stream);
        _ghost = LoadUiClip("cs_ghost");
        _portal = LoadUiClip("cs_portal");
        _motes = LoadUiClip("cs_motes");
        string menuDir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "art", "menu");
        foreach (string f in MenuFiles)
        {
            string path = Path.Combine(menuDir, f);
            if (!File.Exists(path))
                throw new FileNotFoundException($"missing menu art: {Path.GetFullPath(path)}", path);
            using var stream = File.OpenRead(path);
            _textures["menu/" + f] = Texture2D.FromStream(GraphicsDevice, stream);
        }
        foreach (string f in HudFiles) LoadTexture("ui/" + f);
        for (int d = 0; d <= 9; d++) { LoadTexture($"ui/hud_lv{d}.png"); LoadTexture($"ui/hud_num{d}.png"); }
        foreach (var item in _items.Values) LoadTexture(item.Image); // icons for the scanned defs
        LoadNameTags(); // ring plates ride the defs above
        foreach (var skill in _skills.Values) // art for the scanned defs (frames ride the json)
        {
            LoadTexture(skill.Icon);
            LoadTexture(skill.Disabled);
            foreach (string f in skill.Effect) LoadTexture(f);
            foreach (string f in skill.Hit) LoadTexture(f);
            foreach (string f in skill.Affected) LoadTexture(f);
        }
        _statBack = RoundedBacking(448, 406, new Color(62, 96, 118), 3);
        _skillBack = RoundedBacking(SkillW, SkillBodyH, new Color(62, 96, 118), 3);
        _questBack = RoundedBacking(QuestW, QuestBodyH, new Color(62, 96, 118), 3);
        _settingsBack = RoundedBacking(SettingsW, SettingsBodyH, new Color(62, 96, 118), 3);
        RoundTopCorners(Tex("hud_stathilong.png"), 2, 3);
        LoadNpcTextures(); // NPC stand textures (quest json already loaded in Initialize)
        if (_shotPath != null && _screen == Screen.Chars)
        {
            // frozen mid-motion: Update never runs in shots
            const int ghostPose = 4, portalPose = 2, motesPose = 1;
            if (_ghost.Data.Frames.Count < ghostPose + 1 || _portal.Data.Frames.Count < portalPose + 1 || _motes.Data.Frames.Count < motesPose + 1)
                throw new InvalidDataException("select clips lost frames: re-check shot poses");
            _ghost.Frame = ghostPose;
            _portal.Frame = portalPose;
            _motes.Frame = motesPose; // the mid-cycle flash frame would white out the shot
            _walkFrame = Math.Min(1, _delays["walk2"].Count - 1);
        }
        LoadTexture("ui/cursor.png"); // outside UiFiles (that table is plain pngs; the cursor ships origin jsons too)
        _cursorHot = LoadJson<CursorData>(Path.Combine(_assetsDir, "ui", "cursor.json"));
        LoadCursorSet(_hoverSet, "hover"); // WZ Cursor states 1 (buttons), 12 (pressed), 5 (items)
        LoadCursorSet(_clickSet, "click");
        LoadCursorSet(_itemSet, "item");
        SyncCursor(); // _cursorH starts 0, so this first call always builds
        _gfx = new UiGfx { Sb = _spriteBatch, Ui = _ui, Tex = TexByKey, Pixel = _pixel };
        BuildScreenScene();
        if (_shotScreen == "hud") _submenu.Enabled = true; // shot setup needs the built tree
        if (_shotScreen == "delete") _csDeleteModal.Enabled = true;
        _contentLoaded = true;
    }

    private void LoadCursorSet(CursorSet set, string stem)
    {
        set.Data = LoadJson<CursorAnimData>(Path.Combine(_assetsDir, "ui", $"cursor_{stem}.json"));
        if (set.Data.Frames.Count == 0)
            throw new InvalidDataException($"cursor_{stem}.json has no frames: re-export");
        foreach (var f in set.Data.Frames) LoadTexture(f.Image);
    }

    // The OS cursor matches the scene zoom: same scale as the 1366x768 blit,
    // rebuilt when the window size changes. Point sampling, like the blit.
    private void SyncCursor()
    {
        int screenH = GraphicsDevice.PresentationParameters.BackBufferHeight;
        if (screenH == _cursorH) return;
        _cursorH = screenH;
        int screenW = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var (_, _, _, h) = SceneMath.BlitRect(screenW, screenH, GameWidth, GameHeight);
        float s = (float)h / GameHeight;
        _cursorTex?.Dispose();
        _cursorTex = ScaleCursor(_textures["ui/cursor.png"], s);
        _cursorNormal = MouseCursor.FromTexture2D(_cursorTex, (int)Math.Round(_cursorHot.X * s), (int)Math.Round(_cursorHot.Y * s));
        BuildCursorSet(_hoverSet, s);
        BuildCursorSet(_clickSet, s);
        BuildCursorSet(_itemSet, s);
        ApplyCursor(); // a resize mid-press re-applies the current frame at the new zoom
    }

    private void BuildCursorSet(CursorSet set, float s)
    {
        foreach (var t in set.Tex) t.Dispose();
        set.Tex.Clear();
        set.Hot.Clear();
        foreach (var f in set.Data.Frames)
        {
            set.Tex.Add(ScaleCursor(_textures[f.Image], s));
            set.Hot.Add(new Point((int)Math.Round(f.X * s), (int)Math.Round(f.Y * s)));
        }
    }

    // One cursor texture at scene zoom (nearest-neighbor, like the blit).
    private Texture2D ScaleCursor(Texture2D src, float s)
    {
        var px = new Color[src.Width * src.Height];
        src.GetData(px);
        int w = (int)Math.Round(src.Width * s), h = (int)Math.Round(src.Height * s);
        var scaled = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                scaled[y * w + x] = px[(int)(y / s) * src.Width + (int)(x / s)]; // y/s < src.Height: y tops at round(src.Height*s)-1
        var tex = new Texture2D(GraphicsDevice, w, h);
        tex.SetData(scaled);
        return tex;
    }

    // Cursor states: pressed shows the click hand (state 12, static); a press
    // on an item freezes the grab on frame 0. Hover shows the blink (state 1)
    // over buttons, the grab (state 5) over inventory slots.
    private void SyncCursorState(float dtMs)
    {
        bool pressed = Mouse.GetState().LeftButton == ButtonState.Pressed;
        int want = CursorAnim.Select(_hoverId, _hoverItem, _hand.Empty, pressed); // full hands keep the grab anywhere
        CursorAnimData? data = want == 1 ? _hoverSet.Data : want == 2 ? _clickSet.Data : want == 3 ? _itemSet.Data : null;
        if (_cursor.Tick(want, data, dtMs)) ApplyCursor();
    }

    private CursorSet CurSet() => _cursor.State == 1 ? _hoverSet : _cursor.State == 2 ? _clickSet : _itemSet;

    private void ApplyCursor()
    {
        if (_cursor.State == 0) { Mouse.SetCursor(_cursorNormal); return; }
        var set = CurSet();
        int frame = _cursor.State == 4 ? 0 : _cursor.Frame;
        var hot = set.Hot[frame];
        _cursorAnim = MouseCursor.FromTexture2D(set.Tex[frame], hot.X, hot.Y);
        Mouse.SetCursor(_cursorAnim);
    }

    protected override void UnloadContent()
    {
        if (_ui != null) _ui.Dispose(); // Initialize may have thrown before LoadMenuContent ran
        if (_pixel != null) _pixel.Dispose();
        _panelTex?.Dispose();
        _cursorTex?.Dispose();
        foreach (var t in _hoverSet.Tex) t.Dispose();
        foreach (var t in _clickSet.Tex) t.Dispose();
        foreach (var t in _itemSet.Tex) t.Dispose();
        base.UnloadContent();
    }

    // One sink for file + panel. Socket threads call it too, so a dead disk
    // must never throw here: the file write is best-effort, the panel always updates.
    private void GameLog(string line)
    {
        try
        {
            _rollLog?.Write(line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        lock (_logLock)
        {
            _logLines.Add(line);
            if (_logLines.Count > 12) _logLines.RemoveRange(0, _logLines.Count - 12); // the panel shows 12
            _panelDirty = true;
        }
    }

    private string NetLogPath() =>
        Path.Combine(Path.GetDirectoryName(_assetsDir)!, "logs", $"net-{Environment.ProcessId}.log");

    private void StartNet(bool host, string? joinHost)
    {
        // net play often runs two instances on one PC; keep the unfocused one at full speed
        InactiveSleepTime = TimeSpan.Zero;
        _rollLog ??= new RollLog(NetLogPath());
        if (host)
        {
            _server = new GameServer(GameLog, _assetsDir);
            try
            {
                _server.Start();
            }
            catch (Exception ex)
            {
                _server = null;
                throw new IOException($"cannot host: port {Net.Port} busy? ({ex.Message})", ex);
            }
        }
        _net = new NetClient(GameLog);
        _net.Connect(host ? "127.0.0.1" : joinHost!);
        var chara = _save?.Slots[_slot];
        if (chara != null) { PushBuild(); PushName(); } // CLI sessions skip: the server defaults them to beginner
    }

    private void MenuTextInput(char c)
    {
        if (!FocusBelongs() || c is < ' ' or > '~') return;
        if (_focus == "name" && _name.Length < 12 && SaveStore.IsIdChar(c)) _name += c;
    }

    // One hit test for clicks and hovers (the menu Update keeps a short closure over this).
    private static bool HitRect(Rectangle r, int gx, int gy) =>
        gx >= r.X && gx < r.X + r.Width && gy >= r.Y && gy < r.Y + r.Height;

    // Typing and backspace only reach the focused field when it belongs to this screen.
    private bool FocusBelongs() => _screen == Screen.Setup && _focus == "name";

    private (int gx, int gy) MouseGamePos(MouseState ms)
    {
        int screenW = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int screenH = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var (bx, by, w, h) = SceneMath.BlitRect(screenW, screenH, GameWidth, GameHeight);
        return ((int)Math.Floor((ms.X - bx) * (float)GameWidth / w), (int)Math.Floor((ms.Y - by) * (float)GameHeight / h));
    }

    // One left-click edge for menu and game updates (both share _mouseWasDown).
    private bool ClickEdge(MouseState ms)
    {
        bool down = ms.LeftButton == ButtonState.Pressed;
        bool edge = down && !_mouseWasDown;
        _mouseWasDown = down;
        return edge;
    }

    private void UpdateMenu(float dtMs)
    {
        _hoverItem = false; // menus have no inventory slots
        var ms = Mouse.GetState();
        var (gx, gy) = MouseGamePos(ms);
        bool click = ClickEdge(ms);
        bool hit(Rectangle r) => HitRect(r, gx, gy);

        var kb = Keyboard.GetState();
        bool enter = kb.IsKeyDown(Keys.Enter) && !_enterWasDown;
        _enterWasDown = kb.IsKeyDown(Keys.Enter);
        bool esc = kb.IsKeyDown(Keys.Escape) && !_escWasDown;
        _escWasDown = kb.IsKeyDown(Keys.Escape);
        bool r = kb.IsKeyDown(Keys.R) && !_scanWasDown;
        _scanWasDown = kb.IsKeyDown(Keys.R);
        if (click || enter || esc || r) _error = ""; // old errors die on the next click, key, or Esc

        // backspace with key repeat (fresh press deletes one at once)
        bool back = kb.IsKeyDown(Keys.Back);
        if (back && !_backWasDown) { MenuBackspace(); _backTimer = 0.4f; }
        else if (back)
        {
            _backTimer -= 1f / 60;
            if (_backTimer <= 0) { MenuBackspace(); _backTimer = 0.05f; }
        }
        _backWasDown = back;

        if (click && hit(LogButtonRect())) { PlaySfx("click"); _panelOpen = !_panelOpen; return; }
        if (click && _panelOpen && hit(LogPanelRect())) return; // the open log panel swallows its own clicks (Start sits under it)
        switch (_screen)
        {
            case Screen.Saves: UpdateSaves(click, enter, gx, gy); break;
            case Screen.Server: UpdateServer(click, enter, esc, gx, gy, r); break;
            case Screen.Chars: UpdateChars(click, enter, esc, gx, gy, dtMs); break;
            case Screen.Job: UpdateJob(click, enter, esc, gx, gy); break;
            case Screen.Setup: UpdateSetup(click, enter, esc, gx, gy); break;
        }
    }

    // Hover sfx plays once per row entry (leaving plays nothing).
    private void SfxOnHover(string id)
    {
        if (id == _hoverId) return;
        _hoverId = id;
        if (id != "") PlaySfx("over");
    }

    // Menu scenes hold invisible Buttons (input only): row art, alpha
    // states, glass underlays and conditional buttons stay legacy-drawn.
    // Click sounds play by Button Kind; conditional clicks play their own.
    private static GameObject MenuButton(Rectangle r, string id, Action onClick, ButtonKind kind = ButtonKind.Silent)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        o.Add(new Button { LegacyId = id, Kind = kind, OnClick = onClick });
        return o;
    }

    private void BuildSavesScene()
    {
        _menuScene = new Scene();
        _menuScene.Add(MenuBg());
        int bodyH = SaveBodyH();
        var sign = new GameObject { NoHit = true };
        sign.Add(new Sign(bodyH));
        _menuScene.Add(sign);
        for (int i = 0; i < SaveRows(); i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            var b = MenuButton(SaveRowRect(i), "save" + i, () => ActivateSaveRow(r), ButtonKind.Click);
            string file = i < SaveStore.MaxSaves ? SaveBtnFiles[i] : "btn_exit.png";
            b.Add(new Picture("menu/" + file) { HoverAlpha = MenuAlpha.Hover, PressAlpha = MenuAlpha.Pressed });
            _menuScene.Add(b);
        }
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, PanelBottomS(bodyH) + 8, ErrRed));
        _menuScene.Add(err);
    }

    private static GameObject MenuBg()
    {
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(new Rectangle(0, 0, GameWidth, GameHeight)));
        bg.Add(new Picture("ui/worldbg.png", 0, 0));
        return bg;
    }

    private void BuildServerScene()
    {
        _menuScene = new Scene();
        _menuScene.Add(MenuBg());
        int rows = 2 + _hosts.Count; // This PC + LAN hosts + Cancel at the end
        int bodyH = ServerBodyH(rows);
        var sign = new GameObject { NoHit = true };
        sign.Add(new Sign(bodyH));
        _menuScene.Add(sign);
        for (int i = 0; i < rows; i++)
        {
            int r = i; // for-loop variables capture shared: copy per row
            GameObject b;
            if (i == rows - 1)
            {
                b = MenuButton(ServerRowRect(i, rows), "server" + i, () => ToSaves(), ButtonKind.Click);
                b.Add(new CancelRow(MenuInk));
            }
            else
            {
                b = MenuButton(ServerRowRect(i, rows), "server" + i, () => EnterChars(r == 0 ? null : _hosts[r - 1].Ip), ButtonKind.Click);
                string name = r == 0 ? "This PC" : _hosts[r - 1].Name;
                string ip = r == 0 ? "host here" : _hosts[r - 1].Ip;
                b.Add(new ServerRow(name, ip, MenuInk, ListGray));
            }
            _menuScene.Add(b);
        }
        var hint = new GameObject { NoHit = true };
        hint.Add(new CenterText(() => _scanTask != null ? "Searching..." : "R: Refresh", 683, PanelBottomS(bodyH) + 8, Hint));
        _menuScene.Add(hint);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, PanelBottomS(bodyH) + 32, ErrRed));
        _menuScene.Add(err);
    }

    private void BuildCharsScene()
    {
        _menuScene = new Scene();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(new Rectangle(0, 0, GameWidth, GameHeight)));
        bg.Add(new Picture("back/charselect.png"));
        _menuScene.Add(bg);
        for (int cell = 0; cell < PageSlots; cell++) // slots first (bottom): pager, buttons, modal cover them
        {
            int i = _page * PageSlots + cell;
            string id = _save!.Slots[i] != null ? "chars:slot" + cell : ""; // ghosts (empty slots) stay silent
            _menuScene.Add(MenuButton(SlotRect(cell), id, () => { if (_save!.Slots[i] != null) { PlaySfx("click"); _slot = i; } }));
        }
        var dolls = new GameObject { NoHit = true };
        dolls.Add(new Invoke(_ => DrawCharsSlots()));
        _menuScene.Add(dolls);
        var pill = WorldPillRect();
        var pillO = new GameObject { NoHit = true };
        pillO.Add(new Rect(pill));
        pillO.Add(new Picture("ui/selectworld.png", 0, 0));
        pillO.Add(new CenterText(() => ServerLabel(), pill.X + pill.Width / 2, pill.Y + 15, Ink));
        _menuScene.Add(pillO);
        var b = BoardRect();
        var board = new GameObject { NoHit = true };
        board.Add(new Rect(b));
        board.Add(new Picture("ui/cs_board.png", 0, 0));
        board.Add(new CenterText(() => $"{_save!.Slots.Count(s => s != null)}/{SaveStore.MaxHeroes}",
            b.X + CharLayout.BoardCount.X, b.Y + CharLayout.BoardCount.Y, Color.White, small: true));
        _menuScene.Add(board);
        _pager = new GameObject(); // container: no Rect (unhittable), cells tested through it
        var pager = PagerBgRect();
        var glass = new GameObject { NoHit = true };
        glass.Add(new Rect(pager));
        var bb = _textures["ui/cs_page_base.png"];
        glass.Add(new Picture("ui/cs_page_base.png", (pager.Width - bb.Width) / 2, 0) { Tint = new Color(255, 255, 255, CharLayout.PagerGlass) });
        _pager.AddChild(glass);
        var pbg = new GameObject { NoHit = true };
        pbg.Add(new Rect(pager));
        pbg.Add(new Picture("ui/cs_page_bg.png", 0, 0));
        _pager.AddChild(pbg);
        for (int c = 0; c < 7; c++)
        {
            int p = c; // for-loop variables capture shared: copy per cell
            var cb = MenuButton(PageCellRect(c), "chars:page" + c,
                () => { _page = p == 0 ? Math.Max(0, _page - 1) : p == 6 ? Math.Min(Pages - 1, _page + 1) : p - 1; BuildCharsScene(); _scenes.Show(_menuScene); }, ButtonKind.Click);
            string stem = c == 0 ? "page_l" : c == 6 ? "page_r" : $"page_{c}";
            cb.Add(new PagerCell(stem, c - 1 == _page, c != 0 && c != 6));
            _pager.AddChild(cb);
        }
        _menuScene.Add(_pager);
        var sel = _save!.Slots[_slot]; // select/delete exist only over a hero (slot clicks never empty it: no rebuild needed)
        if (sel != null)
        {
            var sb = MenuButton(SelectBtnRect(), "chars:select", () => { if (_save!.Slots[_slot] != null) { PlaySfx("click"); StartGame(ServerMode()); } });
            sb.Add(new Image("ui/cs_select"));
            _menuScene.Add(sb);
        }
        var nb = MenuButton(NewBtnRect(), "chars:new", () => { PlaySfx("click"); EnterJob(FirstEmptyHeroSlot()); });
        nb.Add(new Image("ui/cs_new"));
        _menuScene.Add(nb);
        if (sel != null)
        {
            var db = MenuButton(DeleteBtnRect(), "chars:delete", () => { if (_save!.Slots[_slot] != null) { PlaySfx("click"); _csDeleteModal.Enabled = true; } });
            db.Add(new Picture("ui/cs_delglass.png", 0, 0) { Tint = new Color(255, 255, 255, CharLayout.PagerGlass) });
            db.Add(new Image("ui/cs_delete"));
            _menuScene.Add(db);
        }
        var back = MenuButton(BackBtnRect(), "chars:back", () => EnterServer(), ButtonKind.Click);
        back.Add(new Round(8, new Color(0, 0, 0, CharLayout.PagerGlass))); // radius 8 matches the official delete backing
        back.Add(new Image("ui/cs_back"));
        _menuScene.Add(back);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, CharLayout.ErrorY, ErrRed));
        _menuScene.Add(err);
        _csDeleteModal = new GameObject { Modal = true };
        _csDeleteModal.Add(new Rect(DeletePanelRect())); // panel hits (not buttons): the box stays open, like legacy
        var dim = new GameObject { NoHit = true };
        dim.Add(new Dim(120));
        _csDeleteModal.AddChild(dim);
        var panel = new GameObject { NoHit = true };
        panel.Add(new Rect(DeletePanelRect()));
        panel.Add(new WhitePanel());
        _csDeleteModal.AddChild(panel);
        var dt = new GameObject(); // no Rect: draws, never hits
        dt.Add(new CenterText(() => $"Delete {_save!.Slots[_slot]!.Name}?", 683, CharLayout.DelTextY, Ink));
        _csDeleteModal.AddChild(dt);
        var dy = MenuButton(DeleteYesRect(), "chars:delyes", () => DoDelete(), ButtonKind.Click);
        dy.Add(new Image("ui/cs_delyes"));
        _csDeleteModal.AddChild(dy);
        var dn = MenuButton(DeleteNoRect(), "chars:delno", () => _csDeleteModal.Enabled = false, ButtonKind.Click);
        dn.Add(new Image("ui/cs_delno"));
        _csDeleteModal.AddChild(dn);
        _csDeleteModal.Enabled = false;
        _menuScene.Add(_csDeleteModal);
    }

    private void BuildJobScene()
    {
        _menuScene = new Scene();
        var l = ListBgRect();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(l));
        bg.Add(new Picture("ui/listbg.png", 0, 0));
        _menuScene.Add(bg);
        var cell = MenuButton(CellRect(), "job:cell",
            () => { _jobPicked = true; BuildJobScene(); _scenes.Show(_menuScene); }, ButtonKind.Click);
        if (_jobPicked)
        {
            cell.Add(new Picture("ui/cell_hero.png", 0, 0));
            cell.Add(new Border(Color.Gold));
        }
        else cell.Add(new ImgButton("ui/cell_hero.png"));
        _menuScene.Add(cell);
        var blurb = new GameObject { NoHit = true };
        blurb.Add(new CenterText(() => "A dummy warrior. Real jobs come later.", 683, 640, Color.White));
        _menuScene.Add(blurb);
        var make = MenuButton(MakeBtnRect(), "job:make", () => JobMake(), ButtonKind.Click);
        make.Add(new ImgButton("ui/btn_make.png"));
        _menuScene.Add(make);
        var cancel = MenuButton(JobCancelRect(), "job:cancel", () => JobToChars(), ButtonKind.Click);
        cancel.Add(new ImgButton("ui/btn_cancel.png"));
        _menuScene.Add(cancel);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, 55, ErrRed));
        _menuScene.Add(err);
    }

    private void BuildSetupScene()
    {
        _menuScene = new Scene();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(new Rectangle(0, 0, GameWidth, GameHeight)));
        bg.Add(new Picture("ui/setupbg.png", 0, 0));
        _menuScene.Add(bg);
        var preview = new GameObject { NoHit = true };
        preview.Add(new Invoke(_ => DrawSetupAvatar()));
        _menuScene.Add(preview);
        var labels = new GameObject { NoHit = true };
        labels.Add(new Text("Hero", 660, 200, Ink));
        labels.Add(new Text("Name", 660, 232, Ink));
        _menuScene.Add(labels);
        var name = new GameObject();
        name.Add(new Rect(NameFieldRect()));
        name.Add(new Button { OnClick = () => _focus = "name" }); // the name field is input, not a button: silent
        name.Add(new UiBox(() => _name, "1-12: A-Z a-z 0-9 _ -", () => _focus == "name", Ink, Hint));
        _menuScene.Add(name);
        var confirm = MenuButton(ConfirmBtnRect(), "setup:confirm", () => DoCreate(), ButtonKind.Click);
        confirm.Add(new ImgButton("ui/btn_confirm.png"));
        _menuScene.Add(confirm);
        var cancel = MenuButton(SetupCancelRect(), "setup:cancel", () => SetupToJob(), ButtonKind.Click);
        cancel.Add(new ImgButton("ui/btn_cancel.png"));
        _menuScene.Add(cancel);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 817, 375, ErrRed));
        _menuScene.Add(err);
    }

    private void JobMake()
    {
        if (!_jobPicked) { _error = "pick the Hero first"; return; }
        _name = "";
        _focus = "name";
        _screen = Screen.Setup;
        BuildSetupScene();
        _scenes.Show(_menuScene);
    }

    private void JobToChars()
    {
        _screen = Screen.Chars;
        BuildCharsScene();
        _scenes.Show(_menuScene);
    }

    private void SetupToJob()
    {
        _screen = Screen.Job;
        _focus = "";
        BuildJobScene();
        _scenes.Show(_menuScene);
    }

    // Menu screens rebuild on every entry (rows/slots follow live data);
    // layout reload reuses the same switch. A rebuild resets open-state
    // (submenu, modal), same as the panel positions.
    private void BuildScreenScene()
    {
        switch (_screen)
        {
            case Screen.Saves: BuildSavesScene(); _scenes.Show(_menuScene); break;
            case Screen.Server: BuildServerScene(); _scenes.Show(_menuScene); break;
            case Screen.Chars: BuildCharsScene(); _scenes.Show(_menuScene); break;
            case Screen.Job: BuildJobScene(); _scenes.Show(_menuScene); break;
            case Screen.Setup: BuildSetupScene(); _scenes.Show(_menuScene); break;
            case Screen.Game: BuildGameScene(); _scenes.Show(_gameScene); break;
            default: break;
        }
    }

    private void ToSaves()
    {
        _screen = Screen.Saves;
        _focus = "";
        BuildSavesScene();
        _scenes.Show(_menuScene);
    }

    // Three save rows, then Exit. Enter opens the first save with heroes
    // (or the first save when none have any yet).
    private void UpdateSaves(bool click, bool enter, int gx, int gy)
    {
        if (enter) { ActivateFirstSave(); return; }
        if (HitRect(LogButtonRect(), gx, gy)) SfxOnHover("log"); // the log plaque wins (its click runs before the switch too)
        else SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (click) ClickTree(_scenes.Current, gx, gy);
    }

    private int SaveRows() => SaveStore.MaxSaves + 1; // saves, then Exit

    private void ActivateFirstSave()
    {
        for (int i = 0; i < SaveStore.MaxSaves; i++)
            if (_store.Saves[i]!.Slots.Any(h => h != null)) { PickSave(i); return; }
        PickSave(0); // no heroes anywhere: Enter opens the first save
    }

    private void ActivateSaveRow(int i)
    {
        if (i < SaveStore.MaxSaves) { PickSave(i); return; }
        Exit();
    }

    // This PC first, then every host the LAN scan found. Enter picks this
    // PC (the default row); R re-scans while waiting for a host to appear.
    private void UpdateServer(bool click, bool enter, bool esc, int gx, int gy, bool r)
    {
        if (_scanTask != null && _scanTask.IsCompleted) { _hosts = _scanTask.Result; _scanTask = null; BuildServerScene(); _scenes.Show(_menuScene); }
        if (esc) { ToSaves(); return; }
        if (enter) { EnterChars(null); return; }
        if (HitRect(LogButtonRect(), gx, gy)) SfxOnHover("log"); // the log plaque wins (its click runs before the switch too)
        else SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (click) ClickTree(_scenes.Current, gx, gy);
        if (_screen == Screen.Server && r && _scanTask == null) _scanTask = Discovery.ScanAsync(); // row clicks may have left this screen
    }

    // Reaching the server list always re-scans: the LAN may have changed.
    private void EnterServer()
    {
        _screen = Screen.Server;
        _focus = "";
        _hosts = new();
        _scanTask = Discovery.ScanAsync();
        BuildServerScene();
        _scenes.Show(_menuScene);
    }

    private void UpdateChars(bool click, bool enter, bool esc, int gx, int gy, float dtMs)
    {
        if (HitRect(LogButtonRect(), gx, gy)) SfxOnHover("log");
        else SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        AdvanceClip(_ghost, dtMs);
        AdvanceClip(_portal, dtMs);
        AdvanceClip(_motes, dtMs);
        if (_walkSlot != _slot) { _walkSlot = _slot; _walkFrame = 0; _walkT = 0; } // fresh selection walks from frame 0
        _walkT += dtMs;
        var delays = _delays["walk2"];
        if (_walkT >= delays[_walkFrame]) { _walkT -= delays[_walkFrame]; _walkFrame = (_walkFrame + 1) % delays.Count; }
        if (_csDeleteModal.Enabled)
        {
            if (esc) { _csDeleteModal.Enabled = false; return; }
            if (click && ClickTree(_scenes.Current, gx, gy) == null) _csDeleteModal.Enabled = false; // outside click closes, silent
            return;
        }
        if (esc) { EnterServer(); return; }
        try
        {
            var pagerBefore = _pager; // snapshot: pager clicks rebuild the tree (and _pager) mid-dispatch
            GameObject? took = click ? ClickTree(_scenes.Current, gx, gy) : null;
            // Enter selects unless the pager took the click (legacy order:
            // pager, then select) or the click left this screen. Enter plays
            // nothing. Same-frame enter + new/delete/slot clicks resolve
            // mouse-first: legacy interleaved enter between the pager and the
            // buttons, and a unified dispatch cannot split them.
            if (_screen == Screen.Chars && enter && took?.Parent != pagerBefore && _save!.Slots[_slot] != null)
                StartGame(ServerMode());
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            && ex is not FileNotFoundException and not DirectoryNotFoundException)
        {
            _error = ex.Message; // save/net/data failures become menu errors (missing art crashes loud above)
            ToChars(); // a failed Start may have moved the map/camera: step back out
        }
        if (_screen != Screen.Chars) return;
    }

    private void UpdateJob(bool click, bool enter, bool esc, int gx, int gy)
    {
        if (HitRect(LogButtonRect(), gx, gy)) SfxOnHover("log");
        else SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (esc) { JobToChars(); return; }
        GameObject? took = click ? ClickTree(_scenes.Current, gx, gy) : null;
        // Enter confirms unless the click took something: legacy chained
        // cell, then make-or-enter, so any tree click swallows enter.
        if (enter && took == null && _screen == Screen.Job) JobMake();
    }

    private void UpdateSetup(bool click, bool enter, bool esc, int gx, int gy)
    {
        if (HitRect(LogButtonRect(), gx, gy)) SfxOnHover("log");
        else SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (esc) { SetupToJob(); return; }
        GameObject? took = click ? ClickTree(_scenes.Current, gx, gy) : null;
        // Enter confirms unless the click took something: legacy chained
        // name, then confirm-or-enter, so any tree click swallows enter.
        if (enter && took == null && _screen == Screen.Setup) DoCreate();
    }

    private void MenuBackspace()
    {
        if (!FocusBelongs()) return;
        if (_focus == "name" && _name.Length > 0) _name = _name[..^1];
    }

    // Save confirmed: pick a host next.
    private void PickSave(int slot)
    {
        _saveSlot = slot;
        _save = _store.Saves[slot];
        _hostPick = null;
        EnterServer();
    }

    // Char select draws in screen pixels (no map behind it now): park the
    // camera at the origin; StartGame snaps it back on entry.
    private void ZeroCamera() { _camX = _camY = 0; _cameraX = _cameraY = 0; }

    // Server choice confirmed: characters become the hub (first occupied selected).
    private void EnterChars(string? host)
    {
        _hostPick = host;
        _focus = "";
        ZeroCamera();
        SelectFirstHero();
        GameLog($"server: {ServerLabel()}");
        _screen = Screen.Chars;
        BuildCharsScene();
        _scenes.Show(_menuScene);
    }

    // First filled slot (or 0 when all are empty); the page follows it.
    private void SelectFirstHero()
    {
        _slot = 0;
        for (int i = 0; i < SaveStore.MaxHeroes; i++)
            if (_save!.Slots[i] != null) { _slot = i; break; }
        _page = _slot / PageSlots;
    }

    private int FirstEmptyHeroSlot()
    {
        for (int i = 0; i < SaveStore.MaxHeroes; i++)
            if (_save!.Slots[i] == null) return i;
        throw new InvalidDataException("slots full, delete one first");
    }

    private void EnterJob(int slot)
    {
        _slot = slot;
        _jobPicked = false;
        _screen = Screen.Job;
        BuildJobScene();
        _scenes.Show(_menuScene);
    }

    private void DoCreate()
    {
        bool created = false;
        try
        {
            _store.CreateHero(_saveSlot, _slot, _name);
            created = true; // from here a failed Save must roll the new slot back out
            _store.Save(_savesPath);
            GameLog($"created {_name}");
            _name = "";
            _focus = "";
            _page = _slot / PageSlots; // the new char stays on screen
            _screen = Screen.Chars;
            BuildCharsScene();
            _scenes.Show(_menuScene);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (created) _save!.Slots[_slot] = null;
            _error = ex.Message;
        }
    }

    private void DoDelete()
    {
        string name = _save!.Slots[_slot]!.Name;
        var backup = _save.Slots.ToList(); // delete compacts: roll back the whole row, not one slot
        try
        {
            _store.DeleteHero(_saveSlot, _slot);
            _store.Save(_savesPath);
            GameLog($"deleted {name}");
            SelectFirstHero(); // the deleted slot is empty now: show a filled one (or nothing when all are empty)
            _csDeleteModal.Enabled = false;
            BuildCharsScene(); // the fill-set changed: silent slots move
            _scenes.Show(_menuScene);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _save.Slots.Clear();
            _save.Slots.AddRange(backup); // a failed Save must roll the row back out
            _csDeleteModal.Enabled = false;
            _error = ex.Message;
        }
    }

    // mode: "host" = host here, else join that IP.
    private void StartGame(string mode)
    {
        var chara = _save!.Slots[_slot]!;
        _questLog.Sync(chara); // slot switches swap the chara: rebuild live states from the save
        if (chara.Map != _mapId) LoadMapData(chara.Map);
        LoadMapTextures(); // always: a failed texture load must retry, not skip into missing art
        if (chara.X != null && chara.Y != null)
        {
            _mover.X = chara.X.Value;
            _mover.Y = chara.Y.Value;
            _mover.Ground = null; // falls onto whatever is below (or respawns from the void)
            ResetMotion();
        }
        else
        {
            Spawn();
        }
        SnapCamera();
        _remotes.Clear();
        _names.Clear(); // ids restart every server run: peer tags must not cross sessions
        try
        {
            StartNet(mode == "host", mode == "host" ? null : mode);
        }
        catch
        {
            _net?.Dispose(); // failed join/host must not leak a half server
            _server?.Dispose();
            _net = null;
            _server = null;
            throw;
        }
        GameLog($"{chara.Name} entered the game ({mode})");
        _localWorld = null; ClearMobs(); ClearDrops(); DropNumbers(); DropFx(); _fighter.Cooldowns.Clear(); _fighter.Buffs.Clear(); _fighter.Orbs = 0; // net: the server pushes mobs
        var kb = Keyboard.GetState(); // menu trips don't track game keys: seed edges from live state
        _altWasDown = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        _upWasDown = kb.IsKeyDown(Keys.Up);
        _ctrlWasDown = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        _zWasDown = kb.IsKeyDown(Keys.Z);
        SeedSkillKeys(kb);
        _iWasDown = kb.IsKeyDown(Keys.I);
        _cWasDown = kb.IsKeyDown(Keys.C);
        _kWasDown = kb.IsKeyDown(Keys.K);
        _vWasDown = kb.IsKeyDown(Keys.V);
        _mover.WasInPortal = SceneMath.FindPortal(_map.Portals, _mover.X, _mover.Y) != null;
        _screen = Screen.Game;
        BuildGameScene();
        _scenes.Show(_gameScene);
    }

    private void ToChars()
    {
        try
        {
            SaveChara();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _error = ex.Message; } // disk full: say so on the menu, still step out
        _net?.Dispose();
        _server?.Dispose();
        _net = null;
        _server = null;
        _remotes.Clear();
        _localWorld = null;
        ClearMobs();
        ClearDrops();
        DropNumbers();
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0; // modals never survive the exit (same map skips the reload that clears them)
        _showShop = false; // NPC-bound like talk
        _talkKey = ""; _questKey = ""; _trackKey = ""; // cached #h names die with the char
        DropFx();
        _fighter.Cooldowns.Clear();
        _fighter.Buffs.Clear();
        _fighter.Orbs = 0;
        ZeroCamera(); // no map behind the select screen anymore
        var kb = Keyboard.GetState(); // held Enter/Back must not fall through as a fresh press
        _enterWasDown = kb.IsKeyDown(Keys.Enter);
        _backWasDown = kb.IsKeyDown(Keys.Back);
        _backTimer = 0;
        _focus = "";
        _escWasDown = true; // the held Esc must not fall through into menu nav
        _screen = Screen.Chars;
        BuildCharsScene();
        _scenes.Show(_menuScene);
    }

    private void SaveChara()
    {
        // CLI bypass has no save; menus never entered the game either:
        // saving menu-time coordinates would wipe the real saved position.
        // Shots never save: shot mode rides on a demo store, so saving
        // would wipe the player's real saves file.
        if (_shotPath != null || _save == null || _screen != Screen.Game) return;
        var chara = _save.Slots[_slot]!;
        _hand.Return(chara); // a held piece rejoins the bags before the write (the hand itself is never saved)
        chara.Map = _mapId;
        chara.X = (int)Math.Floor(_mover.X);
        chara.Y = (int)Math.Floor(_mover.Y);
        _store.Save(_savesPath);
        GameLog($"saved {chara.Name} at map {chara.Map} ({chara.X},{chara.Y})");
    }

    // The game hides the log plaque (log lives in the menu now): only the
    // open panel swallows its own clicks.
    private bool UpdateLogPanel(bool click, int gx, int gy)
    {
        if (!click) return false;
        return _panelOpen && HitRect(LogPanelRect(), gx, gy);
    }

    private void DrawMenu()
    {
        GraphicsDevice.Clear(_screen == Screen.Chars ? new Color(120, 170, 210) : _screen == Screen.Job ? new Color(16, 24, 64) : new Color(20, 24, 48));
        _spriteBatch.Begin(blendState: BlendState.NonPremultiplied);
        var ms0 = Mouse.GetState();
        var (gx, gy) = MouseGamePos(ms0);
        bool down = ms0.LeftButton == ButtonState.Pressed;

        _gfx.Pressed = down;
        _scenes.Current?.Draw(_gfx); // menus render from the tree now
        DrawLogOverlay(gx, gy, down);
        _spriteBatch.End();
    }

    // The doll strip stays code-drawn: the sim phase owns the doll,
    // the tree only orders it (behind the pill, above the grass).
    private void DrawCharsSlots()
    {
        for (int cell = 0; cell < PageSlots; cell++)
        {
            int i = _page * PageSlots + cell;
            var (fx, fy) = SlotFeet(cell);
            var s = _save!.Slots[i];
            bool faceRight = cell % 6 < 3; // outer slots face the middle
            if (s == null) { DrawClip(_ghost, fx, fy, !faceRight); continue; } // raw art faces right
            if (i == _slot)
            {
                // The totem sits behind the walker, mirrored with the facing.
                var pt = _textures["ui/cs_pillar.png"];
                int px = faceRight ? fx + CharLayout.Pillar.X : fx - (pt.Width + CharLayout.Pillar.X);
                DrawUi("cs_pillar.png", px, fy + CharLayout.Pillar.Y, !faceRight);
                DrawClip(_portal, fx, fy);
                DrawClip(_motes, fx, fy); // glow stays behind: the walker stays crisp
                DrawAvatar(fx, fy, "walk2", _walkFrame, faceRight, s.Equips);
            }
            else DrawAvatar(fx, fy, "stand2", 0, faceRight, s.Equips);
            DrawName(fx, fy + CharLayout.TagDown, s.Name, PlateFor(s.Equips));
        }
    }



    private UiClip LoadUiClip(string stem)
    {
        var c = new UiClip { Data = LoadJson<UiFramesData>(Path.Combine(_assetsDir, "ui", $"{stem}.json")) };
        if (c.Data.Frames.Count == 0)
            throw new InvalidDataException($"{stem} has no frames");
        foreach (var f in c.Data.Frames)
        {
            if (f.Delay <= 0)
                throw new InvalidDataException($"{stem} has a bad delay");
            LoadTexture(f.Image);
        }
        return c;
    }

    private static void AdvanceClip(UiClip c, float dtMs)
    {
        c.Timer += dtMs;
        if (c.Timer < c.Data.Frames[c.Frame].Delay) return;
        c.Timer -= c.Data.Frames[c.Frame].Delay; // keep the overflow (single step: 100ms+ delays, 16ms ticks)
        c.Frame = (c.Frame + 1) % c.Data.Frames.Count;
    }

    // One overlay frame: the anchor is the slot feet plus the set offset,
    // alpha runs A0->A1 across the frame's delay. Flip mirrors the art
    // around the anchor (ghosts face the shelf middle).
    private void DrawClip(UiClip c, int fx, int fy, bool flip = false)
    {
        var f = c.Data.Frames[c.Frame];
        float k = Math.Clamp(c.Timer / f.Delay, 0f, 1f);
        int a = f.A0 + (int)((f.A1 - f.A0) * k);
        var tex = _textures[f.Image];
        int ox = flip ? tex.Width - f.Ox : f.Ox;
        _spriteBatch.Draw(tex, new Vector2(fx + c.Data.OffX - ox, fy + c.Data.OffY - f.Oy), null,
            new Color(255, 255, 255, a), 0f, Vector2.Zero, 1f,
            flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    // The setup preview stays code-drawn (doll pixels, like the chars
    // strip): the tree only orders it above the backdrop.
    private void DrawSetupAvatar()
    {
        // DrawAvatar speaks world pixels: park the camera while previewing on art
        int camX = _cameraX, camY = _cameraY;
        _cameraX = 0;
        _cameraY = 0;
        DrawAvatar(450, 735, "stand2", 0, false);
        _cameraX = camX;
        _cameraY = camY;
    }

    // Wooden buttons answer with transparency (the log plaque still draws raw).
    private static float BtnAlpha(Rectangle r, int gx, int gy, bool down) =>
        !HitRect(r, gx, gy) ? 1f : down ? MenuAlpha.Pressed : MenuAlpha.Hover;

    private void DrawLogOverlay(int gx, int gy, bool down)
    {
        var log = LogButtonRect();
        if (_screen != Screen.Game)
            _spriteBatch.Draw(MenuTex("btn_log.png"), log, Color.White * BtnAlpha(log, gx, gy, down));
        if (!_panelOpen) return;
        List<string>? snap = null;
        lock (_logLock)
        {
            if (_panelDirty)
            {
                snap = _logLines.ToList(); // copy under the lock, bake outside it
                _panelDirty = false;
            }
        }
        if (snap != null)
        {
            int w = LogPanelRect().Width - 20;
            var wrapped = snap.SelectMany(l => _ui.WrapBig(l, w)).ToList();
            if (wrapped.Count > 12) wrapped.RemoveRange(0, wrapped.Count - 12);
            var tex = _ui.BakeBlock(wrapped, w); // bake first: a failed bake keeps the old panel
            _panelTex?.Dispose();
            _panelTex = tex;
        }
        TreeDraw.DrawSelf(_logRoot, _gfx); // bare root, never in a scene: hits stay code
    }

    private void DrawUi(string file, int x, int y, bool flip = false)
    {
        _spriteBatch.Draw(_textures["ui/" + file], new Vector2(x, y), null, Color.White, 0f,
            Vector2.Zero, 1f, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

}
