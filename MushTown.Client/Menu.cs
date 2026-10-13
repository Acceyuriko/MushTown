using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// W7 lobby: login -> 3 character slots -> solo/host/join menu -> game.
// CLI (--host/--join/--shot) skips all of this with a throwaway identity.
public partial class MushTownGame
{
    private enum Screen { Login, Select, Menu, Game }

    private Screen _screen = Screen.Game;
    private AccountStore _store = new();
    private string _accountsPath = "";
    private Account? _account;
    private int _slot;
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

    // Menu geometry in game pixels (1366x768), shared by click and draw code.
    private static Rectangle UserRect() => new(483, 280, 400, 44);
    private static Rectangle PassRect() => new(483, 360, 400, 44);
    private static Rectangle LoginRect() => new(483, 420, 195, 48);
    private static Rectangle RegisterRect() => new(688, 420, 195, 48);
    private static Rectangle SlotRect(int i) => new(483, 250 + i * 70, 400, 58);
    private static Rectangle NameRect() => new(483, 480, 400, 44);
    private static Rectangle CreateRect() => new(483, 536, 400, 48);
    private static Rectangle SoloRect() => new(483, 280, 400, 52);
    private static Rectangle HostRect() => new(483, 344, 400, 52);
    private static Rectangle JoinRect() => new(483, 408, 195, 52);
    private static Rectangle IpRect() => new(688, 408, 195, 52);
    private static Rectangle BackRect() => new(483, 476, 400, 48);
    private static Rectangle LogButtonRect() => new(1266, 708, 80, 40);
    private static Rectangle PanelRect() => new(900, 228, 446, 470); // right of the menu column, above the log button

    private void InitializeMenu(string? shotScreen)
    {
        _accountsPath = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "data", "accounts.json");
        Window.TextInput += (_, e) => MenuTextInput(e.Character);
        if (_shotPath == null && !_host && _joinHost == null) // real menu session only: CLI never touches accounts
        {
            _store = AccountStore.Load(_accountsPath);
            _rollLog ??= new RollLog(NetLogPath());
            _screen = Screen.Login;
        }
        if (shotScreen != null) // menu screenshots ride on a demo account
        {
            _account = new Account { User = "demo" };
            _account.Slots[0] = new CharaSlot { Name = "hero", Level = 5, Map = 10000, X = 100, Y = 200 };
            _account.Slots[2] = new CharaSlot { Name = "mage", Level = 2, Map = 20000 };
            _screen = shotScreen switch { "select" => Screen.Select, "menu" => Screen.Menu, _ => Screen.Login };
            if (_screen == Screen.Menu) _slot = 0;
            if (_screen == Screen.Select) _slot = 1; // empty slot: the creation row shows
            _panelOpen = true; // menu shots prove the log panel renders too
            GameLog("MushTown w7 menu shot");
            GameLog("the panel shows the last 12 lines");
        }
    }

    private void LoadMenuContent()
    {
        _ui = new UiText(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
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
            _server = new GameServer(GameLog);
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
        Screen.Select => _focus == "name",
        Screen.Menu => _focus == "ip",
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
        bool Hit(Rectangle r) => HitRect(r, gx, gy);

        var kb = Keyboard.GetState();
        bool enter = kb.IsKeyDown(Keys.Enter) && !_enterWasDown;
        _enterWasDown = kb.IsKeyDown(Keys.Enter);
        bool esc = kb.IsKeyDown(Keys.Escape) && !_escWasDown;
        _escWasDown = kb.IsKeyDown(Keys.Escape);
        if (click || enter || esc) _error = ""; // old errors die on the next interaction

        // backspace with key repeat (fresh press deletes one at once)
        bool back = kb.IsKeyDown(Keys.Back);
        if (back && !_backWasDown) { MenuBackspace(); _backTimer = 0.4f; }
        else if (back)
        {
            _backTimer -= 1f / 60;
            if (_backTimer <= 0) { MenuBackspace(); _backTimer = 0.05f; }
        }
        _backWasDown = back;

        if (click && Hit(LogButtonRect())) { _panelOpen = !_panelOpen; return; }
        switch (_screen)
        {
            case Screen.Login:
                if (click && Hit(UserRect())) _focus = "user";
                else if (click && Hit(PassRect())) _focus = "pass";
                else if ((click && Hit(LoginRect())) || enter) DoLogin();
                else if (click && Hit(RegisterRect()))
                {
                    bool created = false;
                    try
                    {
                        _account = _store.Register(_user, _pass); // one hash: reuse the new account, don't log in again
                        created = true; // from here a failed Save must roll the new user back out
                        _store.Save(_accountsPath);
                        GameLog($"registered {_user}");
                        _error = "";
                        _pass = "";
                        _name = "";
                        _slot = 0;
                        _screen = Screen.Select;
                    }
                    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                    {
                        if (created) { _store.Users.Remove(_user); _account = null; }
                        _error = ex.Message;
                    }
                }
                break;
            case Screen.Select:
                if (esc) { _screen = Screen.Login; _account = null; _focus = "user"; break; }
                for (int i = 0; i < 3; i++)
                    if (click && Hit(SlotRect(i)))
                    {
                        _slot = i;
                        if (_account!.Slots[i] != null) { _screen = Screen.Menu; _focus = ""; }
                        else { _focus = "name"; _name = ""; }
                    }
                if (_account!.Slots[_slot] == null && click && Hit(NameRect())) _focus = "name";
                else if (_account!.Slots[_slot] == null && ((click && Hit(CreateRect())) || (enter && _focus == "name")))
                {
                    bool created = false;
                    try
                    {
                        _store.CreateChara(_account!.User, _slot, _name);
                        created = true; // from here a failed Save must roll the new slot back out
                        _store.Save(_accountsPath);
                        GameLog($"created {_name}");
                        _error = "";
                        _name = "";
                        _focus = "";
                    }
                    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                    {
                        if (created) _account!.Slots[_slot] = null;
                        _error = ex.Message;
                    }
                }
                break;
            case Screen.Menu:
                if (esc) { _screen = Screen.Select; _focus = ""; break; }
                try
                {
                    if ((click && Hit(SoloRect())) || (enter && _focus != "ip")) StartGame(null);
                    else if ((click && Hit(JoinRect())) || (enter && _focus == "ip")) StartGame(_ip);
                    else if (click && Hit(HostRect())) StartGame("host");
                    else if (click && Hit(IpRect())) _focus = "ip";
                    else if (click && Hit(BackRect())) { _screen = Screen.Select; _focus = ""; }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { _error = ex.Message; }
                break;
        }
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
            _error = "";
            _pass = "";
            _name = "";
            _slot = 0;
            _screen = Screen.Select;
        }
        catch (InvalidDataException ex) { _error = ex.Message; }
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
        _error = "";
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
        var kb = Keyboard.GetState(); // menu trips don't track game keys: seed edges from live state
        _altWasDown = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        _upWasDown = kb.IsKeyDown(Keys.Up);
        _wasInPortal = SceneMath.FindPortal(_map.Portals, _px, _py) != null;
        _screen = Screen.Game;
    }

    private void ToMenu()
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
        var kb = Keyboard.GetState(); // held Enter/Back must not fall through as a fresh press
        _enterWasDown = kb.IsKeyDown(Keys.Enter);
        _backWasDown = kb.IsKeyDown(Keys.Back);
        _backTimer = 0;
        _focus = "";
        _escWasDown = true; // the held Esc must not fall through into menu nav
        _screen = Screen.Menu;
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

    private void UpdateLogButton()
    {
        var ms = Mouse.GetState();
        if (!ClickEdge(ms)) return;
        var (gx, gy) = MouseGamePos(ms);
        if (HitRect(LogButtonRect(), gx, gy)) _panelOpen = !_panelOpen;
    }

    private void DrawMenu()
    {
        GraphicsDevice.Clear(new Color(20, 24, 48));
        _spriteBatch.Begin(blendState: BlendState.NonPremultiplied);
        var (gx, gy) = MouseGamePos(Mouse.GetState());

        switch (_screen)
        {
            case Screen.Login:
                DrawTextCentered("MushTown", 683, 180, Color.White);
                DrawText("用户名", 483, 248, Color.Gray);
                DrawField(UserRect(), _user, _focus == "user", gx, gy);
                DrawText("密码", 483, 328, Color.Gray);
                DrawField(PassRect(), new string('*', _pass.Length), _focus == "pass", gx, gy);
                DrawButton(LoginRect(), "登录", gx, gy);
                DrawButton(RegisterRect(), "注册", gx, gy);
                if (_error != "") DrawText(_error, 483, 478, new Color(255, 120, 100));
                break;
            case Screen.Select:
                DrawTextCentered($"选择角色 - {_account!.User}", 683, 180, Color.White);
                for (int i = 0; i < 3; i++)
                {
                    var s = _account.Slots[i];
                    DrawButton(SlotRect(i), s == null ? "[ 空 ]" : $"{s.Name}  Lv{s.Level}", gx, gy);
                }
                if (_account.Slots[_slot] == null)
                {
                    DrawText("新角色名", 483, 448, Color.Gray);
                    DrawField(NameRect(), _name, _focus == "name", gx, gy);
                    DrawButton(CreateRect(), "创建", gx, gy);
                }
                if (_error != "") DrawText(_error, 483, 600, new Color(255, 120, 100));
                break;
            case Screen.Menu:
                var chara = _account!.Slots[_slot]!;
                DrawTextCentered($"{chara.Name}  Lv{chara.Level}", 683, 200, Color.White);
                DrawButton(SoloRect(), "单机开始", gx, gy);
                DrawButton(HostRect(), "开房", gx, gy);
                DrawButton(JoinRect(), "加入", gx, gy);
                DrawField(IpRect(), _ip, _focus == "ip", gx, gy);
                DrawButton(BackRect(), "返回", gx, gy);
                if (_error != "") DrawText(_error, 483, 540, new Color(255, 120, 100));
                break;
        }
        DrawLogOverlay(gx, gy);
        _spriteBatch.End();
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
            var tex = _ui.BakeBlock(snap, PanelRect().Width - 20); // bake first: a failed bake keeps the old panel
            _panelTex?.Dispose();
            _panelTex = tex;
        }
        var r = PanelRect();
        _spriteBatch.Draw(_pixel, r, new Color(10, 12, 28, 230));
        if (_panelTex != null)
            _spriteBatch.Draw(_panelTex, new Vector2(r.X + 10, r.Y + 10), Color.White);
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

    private void DrawField(Rectangle r, string text, bool focused, int gx, int gy)
    {
        _spriteBatch.Draw(_pixel, r, new Color(40, 44, 80));
        DrawBoxBorder(r, focused ? Color.Gold : new Color(100, 110, 160));
        var tex = _ui.Get(text == "" ? " " : text);
        _spriteBatch.Draw(tex, new Vector2(r.X + 10, r.Y + (r.Height - tex.Height) / 2), Color.White);
    }

    private void DrawBoxBorder(Rectangle r, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, 2), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y + r.Height - 2, r.Width, 2), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, 2, r.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X + r.Width - 2, r.Y, 2, r.Height), color);
    }
}
