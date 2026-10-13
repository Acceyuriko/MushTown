using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// W7.5 lobby: login -> server/channel -> characters -> job -> setup -> game.
// Art is official modern UI from assets/ui (normal states only, hover is a
// white overlay); every string is baked white and tinted at draw time.
// W7 logic (accounts, slots, save, net) is reused unchanged.
public partial class MushTownGame
{
    private enum Screen { Login, Server, Chars, Job, Setup, Game }

    private Screen _screen = Screen.Game;
    private AccountStore _store = new();
    private string _accountsPath = "";
    private string _sessionPath = "";
    private Account? _account;
    private int _slot;
    private int _serverPick; // 0 solo, 1 host, 2 join
    private bool _joinOpen; // join-IP popup (solo/host go straight in, no popup)
    private bool _registerOpen;
    private bool _jobPicked;
    private bool _deleteOpen;
    private string _focus = "";
    private string _user = "", _pass = "", _name = "", _ip = "127.0.0.1";
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

    // Must match Export.cs UiFiles: every name here must be exported, or LoadTexture throws.
    private static readonly string[] UiFiles = new[]
    {
        "loginbg.png", "loginwin.png", "btn_login.png",
        "worldbg.png", "row_top.png", "row_mid.png", "row_bot.png",
        "ch_top.png", "ch_mid.png", "ch_bot.png",
        "charinfo.png", "btn_start.png", "btn_create.png", "btn_delete.png",
        "btn_deleteyes.png", "selectworld.png", "tag_l.png", "tag_m.png", "tag_r.png",
        "listbg.png", "cell_hero.png", "btn_make.png",
        "setupbg.png", "btn_confirm.png", "btn_cancel.png",
    };

    // Menu geometry in game pixels (1366x768), shared by click and draw code.
    // First pass from WZ origins/vectors, tuned against screenshots after.
    private static Rectangle WinRect() => new(517, 261, 332, 246);
    private static Rectangle IdRect() => new(544, 291, 278, 45);
    private static Rectangle PwRect() => new(544, 337, 278, 44);
    private static Rectangle LoginBtnRect() => new(544, 393, 278, 53);
    private static Rectangle RegTextRect() => new(517, 464, 166, 30);
    private static Rectangle QuitTextRect() => new(683, 464, 166, 30);
    private static Rectangle RegPanelRect() => new(483, 204, 400, 360);
    private static Rectangle RegUserRect() => new(513, 290, 340, 44);
    private static Rectangle RegPassRect() => new(513, 370, 340, 44);
    private static Rectangle RegGoRect() => new(513, 428, 165, 48);
    private static Rectangle RegBackRect() => new(688, 428, 165, 48);
    private static Rectangle ServerRowRect(int i) => new(1184, 12 + i * 50, 170, 50);
    private static Rectangle JoinPanelRect() => new(459, 210, 448, 290);
    private static Rectangle JoinIpRect() => new(583, 350, 200, 40);
    private static Rectangle JoinBtnRect() => new(608, 400, 150, 44);
    private static Rectangle CharInfoRect() => new(1135, 336, 200, 292);
    private static Rectangle StartBtnRect() => new(1145, 554, 180, 61);
    private static Rectangle WorldPillRect() => new(1135, 33, 200, 57);
    private static Rectangle CreateBtnRect() => new(498, 667, 170, 41);
    private static Rectangle DeleteBtnRect() => new(586, 722, 160, 33);
    private static Rectangle CancelBtnRect() => new(30, 695, 167, 43);
    private static Rectangle LogoutTextRect() => new(210, 695, 120, 43);
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
    private static Rectangle LogButtonRect() => new(1266, 708, 80, 40);
    private static Rectangle LogPanelRect() => new(900, 228, 446, 470);

    private static readonly string[] ServerNames = new[] { "Solo", "Host", "Join" };

    // Mode for StartGame: null = solo, "host" = host, else join that ip.
    private string? ServerMode() => _serverPick == 0 ? null : _serverPick == 1 ? "host" : _ip;

    private string ServerLabel() => _serverPick == 0 ? "Solo" : _serverPick == 1 ? $"Host {Net.Port}" : _ip;

    private void InitializeMenu(string? shotScreen)
    {
        string dataDir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "data");
        _accountsPath = Path.Combine(dataDir, "accounts.json");
        _sessionPath = Path.Combine(dataDir, "session.json");
        Window.TextInput += (_, e) => MenuTextInput(e.Character);
        if (_shotPath == null && !_host && _joinHost == null) // real menu session only: CLI never touches accounts
        {
            _store = AccountStore.Load(_accountsPath);
            _rollLog ??= new RollLog(NetLogPath());
            if (!TryRestoreSession()) _screen = Screen.Login;
        }
        if (shotScreen != null) // menu screenshots ride on a demo account
        {
            _account = new Account { User = "demo" };
            _account.Slots[0] = new CharaSlot { Name = "hero", Level = 5, Map = 10000, X = 100, Y = 200, Mesos = 12345 };
            _account.Slots[2] = new CharaSlot { Name = "mage", Level = 2, Map = 20000 };
            _screen = shotScreen switch
            {
                "server" => Screen.Server,
                "chars" or "delete" or "panel" => Screen.Chars,
                "job" => Screen.Job,
                "setup" => Screen.Setup,
                "inv" or "use" or "stat" or "hud" => Screen.Game,
                _ => Screen.Login,
            };
            if (shotScreen is "inv" or "use" or "hud") _showInv = true;
            if (shotScreen == "use") _invTab = 1;
            if (shotScreen is "stat" or "hud") _showStat = true;
            if (shotScreen == "hud") _showSubmenu = true;
            if (shotScreen == "register") _registerOpen = true; // the two modal states get their own shots
            if (shotScreen == "delete") _deleteOpen = true;
            _slot = _screen == Screen.Job || _screen == Screen.Setup ? 1 : 0; // job/setup target the empty slot
            if (_screen == Screen.Server) { _joinOpen = true; _focus = "ip"; } // the join popup proves popups render
            if (_screen == Screen.Setup) { _name = "newhero"; _focus = "name"; }
            if (shotScreen == "register") _focus = "user"; // shots mirror real focus (gold borders included)
            _panelOpen = shotScreen == "panel"; // art shots stay clean; the panel shot pairs with chars (Start sits under it)
            GameLog("MushTown w7.5 menu shot");
        }
    }

    private void LoadMenuContent()
    {
        _ui = new UiText(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        foreach (string f in UiFiles) LoadTexture("ui/" + f); // missing file fails loud here
        foreach (string f in HudFiles) LoadTexture("ui/" + f);
        for (int d = 0; d <= 9; d++) { LoadTexture($"ui/hud_lv{d}.png"); LoadTexture($"ui/hud_num{d}.png"); }
        _statBack = RoundedBacking(448, 406, new Color(62, 96, 118), 3);
        RoundTopCorners(Tex("hud_stathilong.png"), 2, 3);
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
    }

    private void MenuTextInput(char c)
    {
        if (!FocusBelongs() || c is < ' ' or > '~') return;
        string id = _focus;
        if (id == "user" && _user.Length < 12 && AccountStore.IsIdChar(c)) _user += c;
        else if (id == "pass" && _pass.Length < 32) _pass += c;
        else if (id == "name" && _name.Length < 12 && AccountStore.IsIdChar(c)) _name += c;
        else if (id == "ip" && _ip.Length < 15 && (char.IsDigit(c) || c == '.')) _ip += c;
    }

    // One hit test for clicks and hovers (the menu Update keeps a short closure over this).
    private static bool HitRect(Rectangle r, int gx, int gy) =>
        gx >= r.X && gx < r.X + r.Width && gy >= r.Y && gy < r.Y + r.Height;

    // Typing and backspace only reach the focused field when it belongs to this screen.
    private bool FocusBelongs() => _screen switch
    {
        Screen.Login => _focus is "user" or "pass",
        Screen.Server => _focus == "ip",
        Screen.Setup => _focus == "name",
        _ => false,
    };

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
        if (click || enter || esc) _error = ""; // old errors die on the next click, Enter, or Esc

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
            case Screen.Login: UpdateLogin(click, enter, esc, hit); break;
            case Screen.Server: UpdateServer(click, enter, esc, hit); break;
            case Screen.Chars: UpdateChars(click, enter, esc, hit); break;
            case Screen.Job: UpdateJob(click, enter, esc, hit); break;
            case Screen.Setup: UpdateSetup(click, enter, esc, hit); break;
        }
    }

    private void UpdateLogin(bool click, bool enter, bool esc, Func<Rectangle, bool> hit)
    {
        if (_registerOpen)
        {
            if (esc) { CloseRegister(); return; }
            if (click && hit(RegUserRect())) _focus = "user";
            else if (click && hit(RegPassRect())) _focus = "pass";
            else if ((click && hit(RegGoRect())) || enter) DoRegister();
            else if (click && hit(RegBackRect())) CloseRegister();
            else if (click && !hit(RegPanelRect())) CloseRegister(); // outside click closes (popup/delete do the same)
            return;
        }
        if (click && hit(IdRect())) _focus = "user";
        else if (click && hit(PwRect())) _focus = "pass";
        else if ((click && hit(LoginBtnRect())) || enter) DoLogin();
        else if (click && hit(RegTextRect())) { _registerOpen = true; _focus = "user"; }
        else if (click && hit(QuitTextRect())) Exit();
    }

    // The register panel closes from three paths (Esc, Back, outside click).
    private void CloseRegister()
    {
        _registerOpen = false;
        _focus = "user";
    }

    private void UpdateServer(bool click, bool enter, bool esc, Func<Rectangle, bool> hit)
    {
        if (esc || (click && hit(CancelBtnRect())))
        {
            if (_joinOpen) { CloseJoin(); return; } // Esc/Cancel closes the popup first (register/delete do the same)
            _screen = Screen.Chars; // back to characters: logout is its own button now (quitting with Esc used to log out)
            _focus = "";
            return;
        }
        if (!_joinOpen) // the open popup is modal: rows wait until it closes
        {
            for (int i = 0; i < 3; i++)
                if (click && hit(ServerRowRect(i)))
                {
                    if (i == 2) { _joinOpen = true; _focus = "ip"; } // join needs an IP first
                    else EnterChars(i);
                    return;
                }
            if (click && hit(LogoutTextRect())) Logout();
        }
        if (!_joinOpen) return;
        if (click && hit(JoinIpRect())) { _focus = "ip"; return; }
        if ((click && hit(JoinBtnRect())) || enter) EnterChars(2);
        else if (click && !hit(JoinPanelRect())) CloseJoin(); // a closed popup takes its focus with it
    }

    private void CloseJoin()
    {
        _joinOpen = false;
        _focus = "";
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
        if (esc || (click && hit(CancelBtnRect()))) { _screen = Screen.Server; _focus = ""; return; }
        try
        {
            if (((click && hit(StartBtnRect())) || enter) && _account!.Slots[_slot] != null)
                StartGame(ServerMode());
            else if (click && hit(CreateBtnRect())) EnterJob(FirstEmptySlot());
            else if (click && hit(DeleteBtnRect()) && _account!.Slots[_slot] != null) _deleteOpen = true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            && ex is not FileNotFoundException and not DirectoryNotFoundException)
        {
            _error = ex.Message; // save/net/data failures become menu errors (missing art crashes loud above)
            ToChars(); // a failed Start may have moved the map/camera: step back out so chars draws the spawn view again
        }
        // buttons win over avatars (no overlap on the spawn map, verified in w75_chars)
        if (_screen != Screen.Chars) return; // Start may have entered the game above: no avatar check on the new camera
        for (int i = 0; i < 3; i++)
            if (click && hit(AvatarRect(i)))
            {
                if (_account!.Slots[i] == null) EnterJob(i); // empty slot goes straight to the job screen
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
        if (_focus == "user" && _user.Length > 0) _user = _user[..^1];
        else if (_focus == "pass" && _pass.Length > 0) _pass = _pass[..^1];
        else if (_focus == "name" && _name.Length > 0) _name = _name[..^1];
        else if (_focus == "ip" && _ip.Length > 0) _ip = _ip[..^1];
    }

    private void DoLogin()
    {
        try
        {
            _account = _store.Login(_user, _pass);
            _serverPick = 0;
            SaveSession();
            _pass = "";
            _focus = "";
            _screen = Screen.Server;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _account = null; // a failed SaveSession must not leave a login behind
            _error = ex.Message;
        }
    }

    private void DoRegister()
    {
        bool created = false;
        try
        {
            _account = _store.Register(_user, _pass); // one hash: reuse the new account, don't log in again
            created = true; // from here a failed Save must roll the new user back out
            _serverPick = 0;
            SaveSession();
            _store.Save(_accountsPath);
            GameLog($"registered {_user}");
            _pass = "";
            _focus = "";
            _registerOpen = false;
            _screen = Screen.Server;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (created) { _store.Users.Remove(_user); _account = null; }
            _error = ex.Message;
        }
    }

    private void Logout()
    {
        try { File.Delete(_sessionPath); } // missing file is a no-op (a failed save or an outside delete)
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _error = ex.Message;
            return; // still logged in: the session survived, so stay on Server
        }
        _account = null;
        _focus = "user";
        _screen = Screen.Login;
    }

    // Stay logged in: a past session skips login and the server pick (the pick
    // defaults to solo at login and updates on every EnterChars, so Chars can
    // always land somewhere valid; Cancel still goes back to re-pick).
    private bool TryRestoreSession()
    {
        var s = Session.TryLoad(_sessionPath);
        if (s?.User == null || !_store.Users.TryGetValue(s.User, out var account)
            || (uint)s.Server > 2 || (s.Server == 2 && !System.Net.IPAddress.TryParse(s.Ip, out _)))
            return false;
        _account = account;
        _user = account.User; // logout shows the last name, like a typed login
        _serverPick = s.Server;
        if (System.Net.IPAddress.TryParse(s.Ip, out _)) _ip = s.Ip; // keep a good IP for the join popup, drop garbage
        _focus = "";
        SelectFirstChara();
        GameLog($"welcome back {_account.User}");
        _screen = Screen.Chars;
        return true;
    }

    // The login cache: who is in plus the server pick, so the next launch
    // opens on characters. Written on login, register, and every server pick.
    private void SaveSession() =>
        new Session { User = _account!.User, Server = _serverPick, Ip = _ip }.Save(_sessionPath);

    // Server choice confirmed: characters become the hub (first occupied selected).
    private void EnterChars(int server)
    {
        if (server == 2 && !System.Net.IPAddress.TryParse(_ip, out _)) { _error = $"bad IP {_ip}"; return; } // the join popup stays open
        _serverPick = server;
        try { SaveSession(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _error = ex.Message; } // cache only: still enter
        _joinOpen = false;
        _focus = "";
        SelectFirstChara();
        GameLog($"server: {ServerLabel()}");
        _screen = Screen.Chars;
    }

    // First filled slot (or 0 when all are empty).
    private void SelectFirstChara()
    {
        _slot = 0;
        for (int i = 0; i < 3; i++)
            if (_account!.Slots[i] != null) { _slot = i; break; }
    }

    private int FirstEmptySlot()
    {
        for (int i = 0; i < 3; i++)
            if (_account!.Slots[i] == null) return i;
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
            _store.CreateChara(_account!.User, _slot, _name);
            created = true; // from here a failed Save must roll the new slot back out
            _store.Save(_accountsPath);
            GameLog($"created {_name}");
            _name = "";
            _focus = "";
            _screen = Screen.Chars;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (created) _account!.Slots[_slot] = null;
            _error = ex.Message;
        }
    }

    private void DoDelete()
    {
        CharaSlot? old = _account!.Slots[_slot];
        try
        {
            _store.DeleteChara(_account.User, _slot);
            _store.Save(_accountsPath);
            GameLog($"deleted {old!.Name}");
            SelectFirstChara(); // the deleted slot is empty now: show a filled one (or nothing when all are empty)
            _deleteOpen = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _account.Slots[_slot] = old; // a failed Save must roll the slot back out
            _deleteOpen = false;
            _error = ex.Message;
        }
    }

    // mode: null = solo, "host" = host, else join that ip.
    private void StartGame(string? mode)
    {
        var chara = _account!.Slots[_slot]!;
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
        if (mode != null)
        {
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
        }
        GameLog($"{chara.Name} entered the game ({mode ?? "solo"})");
        if (mode == null) StartSoloWorld(); // solo drives its own mob world
        else { _soloWorld = null; _mobs.Clear(); _numbers.Clear(); } // net: the server pushes mobs
        var kb = Keyboard.GetState(); // menu trips don't track game keys: seed edges from live state
        _altWasDown = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        _upWasDown = kb.IsKeyDown(Keys.Up);
        _ctrlWasDown = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        _iWasDown = kb.IsKeyDown(Keys.I);
        _cWasDown = kb.IsKeyDown(Keys.C);
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
        _soloWorld = null;
        _mobs.Clear();
        _numbers.Clear();
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
        // CLI bypass has no account; menus never entered the game either:
        // saving menu-time coordinates would wipe the real saved position
        if (_account == null || _screen != Screen.Game) return;
        var chara = _account.Slots[_slot]!;
        chara.Map = _mapId;
        chara.X = (int)Math.Floor(_px);
        chara.Y = (int)Math.Floor(_py);
        _store.Save(_accountsPath);
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
        var (gx, gy) = MouseGamePos(Mouse.GetState());

        switch (_screen)
        {
            case Screen.Login: DrawLogin(gx, gy); break;
            case Screen.Server: DrawServer(gx, gy); break;
            case Screen.Chars: DrawChars(gx, gy); break;
            case Screen.Job: DrawJob(gx, gy); break;
            case Screen.Setup: DrawSetup(gx, gy); break;
        }
        DrawLogOverlay(gx, gy);
        _spriteBatch.End();
    }

    private void DrawLogin(int gx, int gy)
    {
        DrawUi("loginbg.png", 0, 0);
        var w = WinRect();
        DrawUi("loginwin.png", w.X, w.Y);
        DrawUiBox(IdRect(), _user, "Login ID", _focus == "user");
        DrawUiBox(PwRect(), new string('*', _pass.Length), "Password", _focus == "pass");
        DrawImgButton("btn_login.png", LoginBtnRect(), gx, gy);
        DrawTextButton(RegTextRect(), "Register", gx, gy);
        DrawTextButton(QuitTextRect(), "Quit", gx, gy);
        if (_error != "" && !_registerOpen) DrawTextCentered(_error, 683, 522, ErrRed);
        if (!_registerOpen) return;
        DrawDim(120);
        DrawWhitePanel(RegPanelRect());
        DrawTextCentered("Register", 683, 234, Ink);
        DrawText("Login ID", 513, 262, Hint);
        DrawUiBox(RegUserRect(), _user, "", _focus == "user");
        DrawText("Password", 513, 342, Hint);
        DrawUiBox(RegPassRect(), new string('*', _pass.Length), "", _focus == "pass");
        DrawTextButton(RegGoRect(), "Register", gx, gy);
        DrawTextButton(RegBackRect(), "Back", gx, gy);
        if (_error != "") DrawTextCentered(_error, 683, 488, ErrRed);
    }

    private void DrawServer(int gx, int gy)
    {
        DrawUi("worldbg.png", 0, 0);
        DrawUi("row_top.png", 1184, 12);
        DrawUi("row_mid.png", 1184, 61);
        DrawUi("row_bot.png", 1184, 94);
        for (int i = 0; i < 3; i++)
        {
            var r = ServerRowRect(i);
            DrawHover(r, gx, gy, 60);
            if (_joinOpen && i == 2) DrawBoxBorder(r, Color.Gold);
            DrawTextCentered(ServerNames[i], r.X + r.Width / 2, r.Y + 12, Ink);
        }
        if (_joinOpen)
        {
            DrawDim(100);
            var p = JoinPanelRect();
            DrawUi("ch_top.png", p.X, p.Y);
            for (int k = 0; k < 5; k++) DrawUi("ch_mid.png", p.X, p.Y + 66 + k * 34);
            DrawUi("ch_bot.png", p.X, p.Y + 236);
            // the frame art carries a channel grid: cover it with a white
            // inset, our rows draw on that instead
            _spriteBatch.Draw(_pixel, new Rectangle(p.X + 18, p.Y + 66, p.Width - 36, 224), Color.White);
            DrawTextCentered("Join Game", 683, 288, Ink);
            DrawTextCentered("Host IP:", 683, 322, Hint);
            DrawUiBox(JoinIpRect(), _ip, "", _focus == "ip");
            DrawUiBox(JoinBtnRect(), "Join World", "", false);
            DrawHover(JoinBtnRect(), gx, gy, new Color(0, 0, 0, 40));
        }
        DrawImgButton("btn_cancel.png", CancelBtnRect(), gx, gy);
        DrawTextButton(LogoutTextRect(), "Logout", gx, gy);
        if (_error != "") DrawTextCentered(_error, 683, 540, ErrRed);
    }

    private void DrawChars(int gx, int gy)
    {
        DrawMapBack();
        int shown = 0; // tags stagger across 3 heights by draw order: clamped neighbors never share one
        for (int i = 0; i < 3; i++)
        {
            var (fx, fy) = AvatarFeet(i);
            var s = _account!.Slots[i];
            if (s == null)
            {
                DrawTextCentered("+", fx - _cameraX, fy - _cameraY - 110, new Color(255, 255, 255, 150));
                continue;
            }
            DrawAvatar(fx, fy, "stand2", 0, false);
            DrawTag(fx - _cameraX, fy - _cameraY - 130 - shown * 34, s.Name);
            shown++;
        }
        DrawMapFront();
        var pill = WorldPillRect();
        DrawUi("selectworld.png", pill.X, pill.Y);
        DrawTextCentered(ServerLabel(), pill.X + pill.Width / 2, pill.Y + 15, Ink);
        var sel = _account!.Slots[_slot];
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

    private void DrawLogOverlay(int gx, int gy)
    {
        DrawButton(LogButtonRect(), "日志", gx, gy);
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
            var tex = _ui.BakeBlock(snap, LogPanelRect().Width - 20); // bake first: a failed bake keeps the old panel
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

    private void DrawButton(Rectangle r, string text, int gx, int gy)
    {
        bool hover = HitRect(r, gx, gy);
        _spriteBatch.Draw(_pixel, r, hover ? new Color(80, 95, 180) : new Color(60, 70, 140));
        var tex = _ui.Get(text);
        _spriteBatch.Draw(tex, new Vector2(r.X + (r.Width - tex.Width) / 2, r.Y + (r.Height - tex.Height) / 2), Color.White);
    }

    // Art button: the png carries its own label, hover is a white overlay.
    private void DrawImgButton(string file, Rectangle r, int gx, int gy)
    {
        DrawUi(file, r.X, r.Y);
        DrawHover(r, gx, gy, 40);
    }

    // Full-screen dim behind a modal (the server popup dims less: the world art stays readable).
    private void DrawDim(int alpha)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, GameWidth, GameHeight), new Color(0, 0, 0, alpha));
    }

    // White hover overlay (art buttons take 40, white boxes and rows take 60).
    private void DrawHover(Rectangle r, int gx, int gy, int alpha) =>
        DrawHover(r, gx, gy, new Color(255, 255, 255, alpha));

    // The Join button is a white box: it takes a dark veil (white-on-white would be invisible).
    private void DrawHover(Rectangle r, int gx, int gy, Color veil)
    {
        if (HitRect(r, gx, gy)) _spriteBatch.Draw(_pixel, r, veil);
    }

    // Self-drawn white modal panel (register and delete have no official art).
    private void DrawWhitePanel(Rectangle r)
    {
        _spriteBatch.Draw(_pixel, r, Color.White);
        DrawBoxBorder(r, new Color(150, 150, 150));
    }

    private void DrawTextButton(Rectangle r, string text, int gx, int gy)
    {
        bool hover = HitRect(r, gx, gy);
        DrawHover(r, gx, gy, 60);
        var tex = _ui.Get(text);
        _spriteBatch.Draw(tex, new Vector2(r.X + (r.Width - tex.Width) / 2, r.Y + (r.Height - tex.Height) / 2),
            hover ? Ink : Hint);
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
