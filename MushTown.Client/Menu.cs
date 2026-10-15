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
    private string _sessionPath = "";
    private SaveData? _save;
    private int _saveSlot;
    private int _slot;
    private string? _hostPick; // null = this PC (host here), else the LAN IP to join
    private Task<List<FoundHost>>? _scanTask; // a running LAN scan (UpdateServer collects it; shots never start one)
    private List<FoundHost> _hosts = new();
    private bool _scanWasDown;
    private bool _jobPicked;
    private bool _deleteOpen;
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
    private bool _panelDirty = true;

    private static readonly Color Ink = new(50, 50, 50); // dark text on white art
    private static readonly Color Hint = new(153, 153, 153); // official hint gray
    private static readonly Color ErrRed = new(220, 60, 50);
    private static readonly Color ListGray = new(140, 149, 153); // official font:charNumNormal #8C9599
    private static readonly Color MenuInk = new(43, 8, 5); // baked button text median, sampled off btn_astra

    // Must match Export.cs UiFiles: every name here must be exported, or LoadTexture throws.
    private static readonly string[] UiFiles = new[]
    {
        "worldbg.png",
        "charinfo.png", "btn_start.png", "btn_create.png", "btn_delete.png",
        "btn_deleteyes.png", "selectworld.png", "tag_l.png", "tag_m.png", "tag_r.png",
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
    // menu-layout.json (DEBUG builds reload it live); code only centers.
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

    // Menu geometry in game pixels (1366x768), shared by click and draw code.
    // First pass from WZ origins/vectors, tuned against screenshots after.
    private static Rectangle CharInfoRect() => new(1135, 336, 200, 292);
    private static Rectangle StartBtnRect() => new(1145, 554, 180, 61);
    private static Rectangle WorldPillRect() => new(1135, 33, 200, 57);
    private static Rectangle CreateBtnRect() => new(498, 667, 170, 41);
    private static Rectangle DeleteBtnRect() => new(586, 722, 160, 33);
    private static Rectangle CancelBtnRect() => new(30, 695, 167, 43);
    private static Rectangle DeletePanelRect() => new(483, 284, 400, 200);
    private static Rectangle DeleteYesRect() => new(508, 410, 180, 30);
    private static Rectangle DeleteNoRect() => new(700, 402, 167, 43);
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
        string dataDir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "data");
        _savesPath = Path.Combine(dataDir, "saves.json");
        _sessionPath = Path.Combine(dataDir, "session.json");
        Window.TextInput += (_, e) => MenuTextInput(e.Character);
        if (_shotPath == null && !_host && _joinHost == null) // real menu session only: CLI never touches saves
        {
            _store = SaveStore.Load(_savesPath);
            _rollLog ??= new RollLog(NetLogPath());
            if (!TryRestoreSession()) _screen = Screen.Saves;
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
                "chars" or "delete" or "panel" => Screen.Chars,
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
            if (shotScreen == "hud") _showSubmenu = true;
            if (shotScreen == "delete") _deleteOpen = true; // the modal state gets its own shot
            if (shotScreen == "server") _hosts = new List<FoundHost> { new("192.168.1.4", Net.Port, "ACCEYURIKO") };
            _slot = _screen == Screen.Job || _screen == Screen.Setup ? 1 : 0; // job/setup target the empty slot
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
        EquipBag = new Dictionary<int, int> { { ItemData.Mop, 1 }, { ItemData.Sergeant, 1 } },
    };

    private void LoadMenuContent()
    {
        _ui = new UiText(GraphicsDevice, Path.Combine(_assetsDir, "fonts"));
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        foreach (string f in UiFiles) LoadTexture("ui/" + f); // missing file fails loud here
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
        foreach (int id in ItemData.AllIds) // item defs: json first (it names the icon), then the icon
        {
            var item = LoadJson<ItemData>(Path.Combine(_assetsDir, "item", $"{id}.json"));
            _items[id] = item;
            LoadTexture(item.Image);
        }
        foreach (int id in ItemData.EquipIds) // worn slots outside the beginner table would double-draw
        {
            string? slot = _items[id].Slot;
            if (slot == null || !ItemData.BeginnerImgs.ContainsKey(slot))
                throw new InvalidDataException($"equip {id} has a bad slot");
        }
        foreach (int id in ItemData.AllIds) // stale item jsons price 0: the shop would give stock away
            if (_items[id].Price <= 0)
                throw new InvalidDataException($"item {id} has a bad price");
        foreach (var tab in SkillTabs) // skills: json first (it names every frame), then all art
            foreach ((int id, string? _) in tab)
            {
                var skill = LoadJson<SkillData>(Path.Combine(_assetsDir, "skill", $"{id}.json"));
                _skills[id] = skill;
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
    }

    protected override void UnloadContent()
    {
        if (_ui != null) _ui.Dispose(); // Initialize may have thrown before LoadMenuContent ran
        if (_pixel != null) _pixel.Dispose();
        _panelTex?.Dispose();
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
        if (chara != null) PushBuild(); // CLI sessions skip: the server defaults them to beginner
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

    private void UpdateMenu()
    {
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

        if (click && hit(LogButtonRect())) { _panelOpen = !_panelOpen; return; }
        if (click && _panelOpen && hit(LogPanelRect())) return; // the open log panel swallows its own clicks (Start sits under it)
        switch (_screen)
        {
            case Screen.Saves: UpdateSaves(click, enter, esc, hit); break;
            case Screen.Server: UpdateServer(click, enter, esc, hit, r); break;
            case Screen.Chars: UpdateChars(click, enter, esc, hit); break;
            case Screen.Job: UpdateJob(click, enter, esc, hit); break;
            case Screen.Setup: UpdateSetup(click, enter, esc, hit); break;
        }
    }

    // Three save rows, then Exit. Enter opens the first save with heroes
    // (or the first save when none have any yet).
    private void UpdateSaves(bool click, bool enter, bool esc, Func<Rectangle, bool> hit)
    {
        int rows = SaveRows();
        if (enter) { ActivateFirstSave(); return; }
        for (int i = 0; i < rows; i++)
            if (click && hit(SaveRowRect(i))) { ActivateSaveRow(i); return; }
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
    private void UpdateServer(bool click, bool enter, bool esc, Func<Rectangle, bool> hit, bool r)
    {
        if (_scanTask != null && _scanTask.IsCompleted) { _hosts = _scanTask.Result; _scanTask = null; }
        int rows = 2 + _hosts.Count; // This PC + LAN hosts + Cancel at the end
        if (esc)
        {
            _screen = Screen.Saves;
            _focus = "";
            return;
        }
        if (enter) { EnterChars(null); return; }
        for (int i = 0; i < rows; i++)
            if (click && hit(ServerRowRect(i, rows)))
            {
                if (i == rows - 1) { _screen = Screen.Saves; _focus = ""; return; }
                EnterChars(i == 0 ? null : _hosts[i - 1].Ip); return;
            }
        if (r && _scanTask == null) _scanTask = Discovery.ScanAsync();
    }

    // Reaching the server list always re-scans: the LAN may have changed.
    private void EnterServer()
    {
        _screen = Screen.Server;
        _focus = "";
        _hosts = new();
        _scanTask = Discovery.ScanAsync();
    }

    private void UpdateChars(bool click, bool enter, bool esc, Func<Rectangle, bool> hit)
    {
        if (_deleteOpen)
        {
            if (esc || (click && hit(DeleteNoRect()))) { _deleteOpen = false; return; }
            if (click && hit(DeleteYesRect())) DoDelete(); // Enter never confirms: wiping a char must be a deliberate click
            else if (click && !hit(DeletePanelRect())) _deleteOpen = false;
            return;
        }
        if (esc || (click && hit(CancelBtnRect()))) { EnterServer(); return; }
        try
        {
            if (((click && hit(StartBtnRect())) || enter) && _save!.Slots[_slot] != null)
                StartGame(ServerMode());
            else if (click && hit(CreateBtnRect())) EnterJob(FirstEmptyHeroSlot());
            else if (click && hit(DeleteBtnRect()) && _save!.Slots[_slot] != null) _deleteOpen = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            && ex is not FileNotFoundException and not DirectoryNotFoundException)
        {
            _error = ex.Message; // save/net/data failures become menu errors (missing art crashes loud above)
            ToChars(); // a failed Start may have moved the map/camera: step back out so chars draws the spawn view again
        }
        // buttons win over avatars (no overlap on the spawn map, verified in w75_chars)
        if (_screen != Screen.Chars) return; // Start may have entered the game above: no avatar check on the new camera
        for (int i = 0; i < SaveStore.MaxHeroes; i++)
            if (click && hit(AvatarRect(i)))
            {
                if (_save!.Slots[i] == null) EnterJob(i); // empty slot goes straight to the job screen
                else _slot = i;
                return;
            }
    }

    private void UpdateJob(bool click, bool enter, bool esc, Func<Rectangle, bool> hit)
    {
        if (esc || (click && hit(JobCancelRect()))) { _screen = Screen.Chars; return; }
        if (click && hit(CellRect())) _jobPicked = true;
        else if ((click && hit(MakeBtnRect())) || enter)
        {
            if (!_jobPicked) { _error = "pick the Hero first"; return; }
            _name = "";
            _focus = "name";
            _screen = Screen.Setup;
        }
    }

    private void UpdateSetup(bool click, bool enter, bool esc, Func<Rectangle, bool> hit)
    {
        if (esc || (click && hit(SetupCancelRect()))) { _screen = Screen.Job; _focus = ""; return; }
        if (click && hit(NameFieldRect())) _focus = "name";
        else if ((click && hit(ConfirmBtnRect())) || enter) DoCreate();
    }

    private void MenuBackspace()
    {
        if (!FocusBelongs()) return;
        if (_focus == "name" && _name.Length > 0) _name = _name[..^1];
    }

    // A past session skips saves and the server pick (the pick defaults to
    // this PC and updates on every EnterChars, so Chars can always land
    // somewhere valid; Cancel still goes back to re-pick).
    private bool TryRestoreSession()
    {
        var s = Session.TryLoad(_sessionPath);
        if (s == null || (uint)s.Slot >= SaveStore.MaxSaves
            || (s.Host != null && !System.Net.IPAddress.TryParse(s.Host, out _)))
            return false;
        _saveSlot = s.Slot;
        _save = _store.Saves[s.Slot];
        _hostPick = s.Host;
        _focus = "";
        SelectFirstHero();
        GameLog($"welcome back to {SaveNames[_saveSlot]}");
        _screen = Screen.Chars;
        return true;
    }

    // The session cache: which save is in plus the host pick, so the next
    // launch opens on characters. Written on every save and server pick.
    private void SaveSession() =>
        new Session { Slot = _saveSlot, Host = _hostPick }.Save(_sessionPath);

    // Save confirmed: pick a host next (the pick writes the session too).
    private void PickSave(int slot)
    {
        _saveSlot = slot;
        _save = _store.Saves[slot];
        _hostPick = null;
        try { SaveSession(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _error = ex.Message; } // cache only: still enter
        EnterServer();
    }

    // Server choice confirmed: characters become the hub (first occupied selected).
    private void EnterChars(string? host)
    {
        _hostPick = host;
        try { SaveSession(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _error = ex.Message; } // cache only: still enter
        _focus = "";
        SelectFirstHero();
        GameLog($"server: {ServerLabel()}");
        _screen = Screen.Chars;
    }

    // First filled slot (or 0 when all are empty).
    private void SelectFirstHero()
    {
        _slot = 0;
        for (int i = 0; i < SaveStore.MaxHeroes; i++)
            if (_save!.Slots[i] != null) { _slot = i; break; }
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
            _screen = Screen.Chars;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (created) _save!.Slots[_slot] = null;
            _error = ex.Message;
        }
    }

    private void DoDelete()
    {
        CharaSlot? old = _save!.Slots[_slot];
        try
        {
            _store.DeleteHero(_saveSlot, _slot);
            _store.Save(_savesPath);
            GameLog($"deleted {old!.Name}");
            SelectFirstHero(); // the deleted slot is empty now: show a filled one (or nothing when all are empty)
            _deleteOpen = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _save.Slots[_slot] = old; // a failed Save must roll the slot back out
            _deleteOpen = false;
            _error = ex.Message;
        }
    }

    // mode: "host" = host here, else join that IP.
    private void StartGame(string mode)
    {
        var chara = _save!.Slots[_slot]!;
        if (chara.Map != _mapId) LoadMapData(chara.Map);
        LoadMapTextures(); // always: a failed texture load must retry, not skip into missing art
        if (chara.X != null && chara.Y != null)
        {
            _px = chara.X.Value;
            _py = chara.Y.Value;
            _ground = null; // falls onto whatever is below (or respawns from the void)
            ResetMotion();
        }
        else
        {
            Spawn();
        }
        SnapCamera();
        _remotes.Clear();
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
        _localWorld = null; _mobs.Clear(); _drops.Clear(); _numbers.Clear(); _fx.Clear(); _skillCdMs.Clear(); _buffMs.Clear(); _orbs = 0; // net: the server pushes mobs
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
        _wasInPortal = SceneMath.FindPortal(_map.Portals, _px, _py) != null;
        _screen = Screen.Game;
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
        _mobs.Clear();
        _drops.Clear();
        _numbers.Clear();
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0; // modals never survive the exit (same map skips the reload that clears them)
        _showShop = false; // NPC-bound like talk
        _talkKey = ""; _questKey = ""; _trackKey = ""; // cached #h names die with the char
        _fx.Clear();
        _skillCdMs.Clear();
        _buffMs.Clear();
        _orbs = 0;
        try
        {
            // the chars screen always shows the spawn map, whatever was played last
            if (_mapId != SpawnMapId) { LoadMapData(SpawnMapId); LoadMapTextures(); }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            && ex is not FileNotFoundException and not DirectoryNotFoundException)
        {
            if (_error == "") _error = ex.Message; // a save failure above outranks this backdrop failure
        }
        Spawn();
        SnapCamera();
        var kb = Keyboard.GetState(); // held Enter/Back must not fall through as a fresh press
        _enterWasDown = kb.IsKeyDown(Keys.Enter);
        _backWasDown = kb.IsKeyDown(Keys.Back);
        _backTimer = 0;
        _focus = "";
        _escWasDown = true; // the held Esc must not fall through into menu nav
        _screen = Screen.Chars;
    }

    private void SaveChara()
    {
        // CLI bypass has no save; menus never entered the game either:
        // saving menu-time coordinates would wipe the real saved position.
        // Shots never save: shot mode rides on a demo store, so saving
        // would wipe the player's real saves file.
        if (_shotPath != null || _save == null || _screen != Screen.Game) return;
        var chara = _save.Slots[_slot]!;
        chara.Map = _mapId;
        chara.X = (int)Math.Floor(_px);
        chara.Y = (int)Math.Floor(_py);
        _store.Save(_savesPath);
        GameLog($"saved {chara.Name} at map {chara.Map} ({chara.X},{chara.Y})");
    }

    // True when the click lands on the log button or the open panel: the caller must skip the HUD (both panels overlap the panel).
    private bool UpdateLogButton(bool click, int gx, int gy)
    {
        if (!click) return false;
        if (HitRect(LogButtonRect(), gx, gy)) { _panelOpen = !_panelOpen; return true; }
        return _panelOpen && HitRect(LogPanelRect(), gx, gy);
    }

    // Feet of slot i on the spawn ground (all three share the line, clamped inside it).
    private (int fx, int fy) AvatarFeet(int i)
    {
        int a = Math.Min(_spawnGround.X1, _spawnGround.X2);
        int b = Math.Max(_spawnGround.X1, _spawnGround.X2);
        // a stub shorter than 40 leaves no room to spread: stack all three mid-ground
        int fx = b - a < 40 ? (a + b) / 2 : Math.Clamp(_map.Info.SpawnX + (i - 1) * 150, a + 20, b - 20);
        return (fx, SceneMath.FootholdY(_spawnGround, fx));
    }

    private Rectangle AvatarRect(int i)
    {
        var (fx, fy) = AvatarFeet(i);
        return new Rectangle(fx - _cameraX - 70, fy - _cameraY - 170, 140, 170);
    }

    private void DrawMenu()
    {
        GraphicsDevice.Clear(_screen == Screen.Chars ? new Color(120, 170, 210) : _screen == Screen.Job ? new Color(16, 24, 64) : new Color(20, 24, 48));
        _spriteBatch.Begin(blendState: BlendState.NonPremultiplied);
        var ms0 = Mouse.GetState();
        var (gx, gy) = MouseGamePos(ms0);
        bool down = ms0.LeftButton == ButtonState.Pressed;

        switch (_screen)
        {
            case Screen.Saves: DrawSaves(gx, gy, down); break;
            case Screen.Server: DrawServer(gx, gy, down); break;
            case Screen.Chars: DrawChars(gx, gy); break;
            case Screen.Job: DrawJob(gx, gy); break;
            case Screen.Setup: DrawSetup(gx, gy); break;
        }
        DrawLogOverlay(gx, gy, down);
        _spriteBatch.End();
    }

    // The sign board: header, 3-slice body (the cream center stretches),
    // base. The caps' solid bars overlap the body 12px so no background
    // shows at the joints; the leaf tips stop short of the buttons.
    private void DrawSign(int bodyH)
    {
        int x = (GameWidth - MenuTex("sign_mid.png").Width) / 2;
        int top = PanelTop(bodyH), body = BodyY(bodyH);
        var mid = MenuTex("sign_mid.png");
        // Chained bounds: each slice starts where the last ended, so separate
        // truncations can never open a 1px seam at the joints.
        int y0 = MSY(body, bodyH), y1 = y0 + MSS(MenuLayout.Slice);
        int y3 = MSY(body + bodyH, bodyH), y2 = y3 - MSS(MenuLayout.Slice);
        int dx = MSX(x), dw = MSS(mid.Width);
        _spriteBatch.Draw(mid, new Rectangle(dx, y0, dw, y1 - y0),
            new Rectangle(0, 0, mid.Width, MenuLayout.Slice), Color.White);
        _spriteBatch.Draw(mid, new Rectangle(dx, y1, dw, y2 - y1),
            new Rectangle(0, MenuLayout.Slice, mid.Width, mid.Height - MenuLayout.Slice * 2), Color.White);
        _spriteBatch.Draw(mid, new Rectangle(dx, y2, dw, y3 - y2),
            new Rectangle(0, mid.Height - MenuLayout.Slice, mid.Width, MenuLayout.Slice), Color.White);
        var head = MenuTex("sign_top.png");
        _spriteBatch.Draw(head, MSR(new Rectangle((GameWidth - head.Width) / 2, top, head.Width, head.Height), bodyH), Color.White);
        var foot = MenuTex("sign_bot.png");
        _spriteBatch.Draw(foot, MSR(new Rectangle((GameWidth - foot.Width) / 2, body + bodyH - MenuLayout.BotUp, foot.Width, foot.Height), bodyH), Color.White);
    }

    // Wooden buttons answer with transparency: hover 0.9, pressed 0.6.
    private static float BtnAlpha(Rectangle r, int gx, int gy, bool down) =>
        !HitRect(r, gx, gy) ? 1f : down ? 0.6f : 0.9f;

    // One server row: blank pill plus name left + IP right. Text bakes at
    // display size and draws unscaled (scaling the bake blurs it).
    private void DrawServerRow(Rectangle r, string name, string ip, int gx, int gy, bool down)
    {
        _spriteBatch.Draw(MenuTex("btn_blank.png"), r, Color.White * BtnAlpha(r, gx, gy, down));
        var tr = _ui.GetList(ip);
        int m = MSS(MenuLayout.TextMargin);
        var tl = _ui.GetList(TruncateList(name, r.Width - m - tr.Width - m - MSS(MenuLayout.TextGap)));
        int cy = r.Y + (r.Height - tl.Height) / 2 + MSS(MenuLayout.TextDown);
        _spriteBatch.Draw(tl, new Vector2(r.X + m, cy), MenuInk);
        _spriteBatch.Draw(tr, new Vector2(r.Right - m - tr.Width, cy), ListGray);
    }

    // Cancel plaque: the last server row, blank pill plus centered label.
    private void DrawCancelRow(Rectangle r, int gx, int gy, bool down)
    {
        _spriteBatch.Draw(MenuTex("btn_blank.png"), r, Color.White * BtnAlpha(r, gx, gy, down));
        var t = _ui.GetListCancel("Cancel");
        int cy = r.Y + (r.Height - t.Height) / 2 + MSS(MenuLayout.CancelDown);
        _spriteBatch.Draw(t, new Vector2(r.X + (r.Width - t.Width) / 2, cy), MenuInk);
    }

    // Cuts text to a pixel width (server names can run past the row).
    private string TruncateList(string text, int maxWidth)
    {
        if (_ui.GetList(text).Width <= maxWidth) return text;
        while (text.Length > 0 && _ui.GetList(text + "…").Width > maxWidth) text = text[..^1];
        return text + "…";
    }

    private void DrawSaves(int gx, int gy, bool down)
    {
        DrawUi("worldbg.png", 0, 0);
        int bodyH = SaveBodyH();
        DrawSign(bodyH);
        for (int i = 0; i < SaveRows(); i++)
        {
            var r = SaveRowRect(i);
            var t = MenuTex(i < SaveStore.MaxSaves ? SaveBtnFiles[i] : "btn_exit.png");
            _spriteBatch.Draw(t, r, Color.White * BtnAlpha(r, gx, gy, down));
        }
        if (_error != "") DrawTextCentered(_error, 683, PanelBottomS(bodyH) + 8, ErrRed);
    }

    private void DrawServer(int gx, int gy, bool down)
    {
        DrawUi("worldbg.png", 0, 0);
        int rows = 2 + _hosts.Count, bodyH = ServerBodyH(rows);
        DrawSign(bodyH);
        for (int i = 0; i < rows; i++)
        {
            var r = ServerRowRect(i, rows);
            if (i == 0) DrawServerRow(r, "This PC", "host here", gx, gy, down);
            else if (i < rows - 1) DrawServerRow(r, _hosts[i - 1].Name, _hosts[i - 1].Ip, gx, gy, down);
            else DrawCancelRow(r, gx, gy, down);
        }
        DrawTextCentered(_scanTask != null ? "Searching..." : "R: Refresh", 683, PanelBottomS(bodyH) + 8, Hint);
        if (_error != "") DrawTextCentered(_error, 683, PanelBottomS(bodyH) + 32, ErrRed);
    }

    private void DrawChars(int gx, int gy)
    {
        DrawMapBack();
        int shown = 0; // tags stagger across 3 heights by draw order: clamped neighbors never share one
        for (int i = 0; i < SaveStore.MaxHeroes; i++)
        {
            var (fx, fy) = AvatarFeet(i);
            var s = _save!.Slots[i];
            if (s == null)
            {
                DrawTextCentered("+", fx - _cameraX, fy - _cameraY - 110, new Color(255, 255, 255, 150));
                continue;
            }
            DrawAvatar(fx, fy, "stand2", 0, false, s.Equips);
            DrawTag(fx - _cameraX, fy - _cameraY - 130 - shown * 34, s.Name);
            shown++;
        }
        DrawMapFront();
        var pill = WorldPillRect();
        DrawUi("selectworld.png", pill.X, pill.Y);
        DrawTextCentered(ServerLabel(), pill.X + pill.Width / 2, pill.Y + 15, Ink);
        var sel = _save!.Slots[_slot];
        if (sel != null)
        {
            var p = CharInfoRect();
            DrawUi("charinfo.png", p.X, p.Y);
            DrawTextCentered($"Lv {sel.Level}", p.X + 100, p.Y + 21, Ink);
            DrawTextCentered(sel.Name, p.X + 100, p.Y + 73, Ink);
            DrawTextCentered("Hero", p.X + 100, p.Y + 110, Ink); // dummy job until the real job system lands
            DrawTextCentered("0", p.X + 100, p.Y + 178, Ink); // no combat yet (W8): attack power reads 0
            DrawImgButton("btn_start.png", StartBtnRect(), gx, gy);
        }
        DrawImgButton("btn_create.png", CreateBtnRect(), gx, gy);
        if (sel != null) DrawImgButton("btn_delete.png", DeleteBtnRect(), gx, gy);
        DrawImgButton("btn_cancel.png", CancelBtnRect(), gx, gy);
        if (_error != "") DrawTextCentered(_error, 683, 640, ErrRed);
        if (!_deleteOpen) return;
        DrawDim(120);
        DrawWhitePanel(DeletePanelRect());
        DrawTextCentered($"Delete {sel!.Name}?", 683, 320, Ink);
        DrawImgButton("btn_deleteyes.png", DeleteYesRect(), gx, gy);
        DrawImgButton("btn_cancel.png", DeleteNoRect(), gx, gy);
    }

    private void DrawJob(int gx, int gy)
    {
        var l = ListBgRect();
        DrawUi("listbg.png", l.X, l.Y);
        var c = CellRect();
        DrawUi("cell_hero.png", c.X, c.Y);
        if (_jobPicked) DrawBoxBorder(c, Color.Gold);
        else DrawHover(c, gx, gy, 40);
        DrawTextCentered("A dummy warrior. Real jobs come later.", 683, 640, Color.White);
        DrawImgButton("btn_make.png", MakeBtnRect(), gx, gy);
        DrawImgButton("btn_cancel.png", JobCancelRect(), gx, gy);
        if (_error != "") DrawTextCentered(_error, 683, 55, ErrRed);
    }

    private void DrawSetup(int gx, int gy)
    {
        DrawUi("setupbg.png", 0, 0);
        // DrawAvatar speaks world pixels: park the camera while previewing on art
        int camX = _cameraX, camY = _cameraY;
        _cameraX = 0;
        _cameraY = 0;
        DrawAvatar(450, 735, "stand2", 0, false);
        _cameraX = camX;
        _cameraY = camY;
        DrawText("Hero", 660, 200, Ink);
        DrawText("Name", 660, 232, Ink);
        DrawUiBox(NameFieldRect(), _name, "1-12: A-Z a-z 0-9 _ -", _focus == "name");
        DrawImgButton("btn_confirm.png", ConfirmBtnRect(), gx, gy);
        DrawImgButton("btn_cancel.png", SetupCancelRect(), gx, gy);
        if (_error != "") DrawTextCentered(_error, 817, 375, ErrRed);
    }

    private void DrawLogOverlay(int gx, int gy, bool down)
    {
        var log = LogButtonRect();
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
        var r = LogPanelRect();
        _spriteBatch.Draw(_pixel, r, new Color(10, 12, 28, 230));
        if (_panelTex != null)
            _spriteBatch.Draw(_panelTex, new Vector2(r.X + 10, r.Y + 10), Color.White);
    }

    private void DrawUi(string file, int x, int y)
    {
        _spriteBatch.Draw(_textures["ui/" + file], new Vector2(x, y), Color.White);
    }

    // Fitted nametag: dark 3-slice pill with the name centered on it.
    private void DrawTag(int cx, int y, string name)
    {
        var tex = _ui.Get(name);
        var l = _textures["ui/tag_l.png"];
        var m = _textures["ui/tag_m.png"];
        var r = _textures["ui/tag_r.png"];
        int w = tex.Width + 24;
        int x = cx - w / 2;
        _spriteBatch.Draw(l, new Vector2(x, y), Color.White);
        _spriteBatch.Draw(m, new Rectangle(x + l.Width, y, w - l.Width - r.Width, m.Height), Color.White);
        _spriteBatch.Draw(r, new Vector2(x + w - r.Width, y), Color.White);
        _spriteBatch.Draw(tex, new Vector2(cx - tex.Width / 2, y + (m.Height - tex.Height) / 2), Color.White);
    }

    private void DrawText(string text, int x, int y, Color color)
    {
        _spriteBatch.Draw(_ui.Get(text), new Vector2(x, y), color);
    }

    private void DrawTextCentered(string text, int cx, int y, Color color)
    {
        var tex = _ui.Get(text);
        _spriteBatch.Draw(tex, new Vector2(cx - tex.Width / 2, y), color);
    }

    // Art button: the png carries its own label, hover is a white overlay.
    private void DrawImgButton(string file, Rectangle r, int gx, int gy)
    {
        DrawUi(file, r.X, r.Y);
        DrawHover(r, gx, gy, 40);
    }

    // Full-screen dim behind the delete modal.
    private void DrawDim(int alpha)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, GameWidth, GameHeight), new Color(0, 0, 0, alpha));
    }

    // White hover overlay (art buttons take 40, white boxes and rows take 60).
    private void DrawHover(Rectangle r, int gx, int gy, int alpha)
    {
        if (HitRect(r, gx, gy)) _spriteBatch.Draw(_pixel, r, new Color(255, 255, 255, alpha));
    }



    // Self-drawn white modal panel (the delete modal has no official art).
    private void DrawWhitePanel(Rectangle r)
    {
        _spriteBatch.Draw(_pixel, r, Color.White);
        DrawBoxBorder(r, new Color(150, 150, 150));
    }

    private void DrawUiBox(Rectangle r, string text, string hint, bool focused)
    {
        _spriteBatch.Draw(_pixel, r, Color.White);
        DrawBoxBorder(r, focused ? Color.Gold : new Color(160, 160, 160));
        string show = text == "" ? hint : text;
        if (show == "") return;
        var tex = _ui.Get(show);
        _spriteBatch.Draw(tex, new Vector2(r.X + 10, r.Y + (r.Height - tex.Height) / 2), text == "" ? Hint : Ink);
    }

    private void DrawBoxBorder(Rectangle r, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, 2), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y + r.Height - 2, r.Width, 2), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, 2, r.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X + r.Width - 2, r.Y, 2, r.Height), color);
    }
}
