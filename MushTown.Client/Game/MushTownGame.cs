using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// Renders the exported map + playable character. Weekly acceptance view.
public partial class MushTownGame : Game
{
    // The game is always drawn at this size, then scaled up to fill the screen.
    internal const int GameWidth = 1366; // UI components center on these (Sign, Dim, BossBar, Minimap)
    internal const int GameHeight = 768;

    // The feet live in Shared now (PlayerMover); only the map-switch beat stays here.
    private const int LoadBeat = 10;      // black frames shown on map switch (~170ms)

    private const int SpawnMapId = 10000; // Maple Island: Southperry

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetsDir;
    private readonly string? _shotPath;
    private readonly int _shotMap;
    private readonly int _shotTravel;
    private readonly string _shotAction;
    private readonly int _shotFrame;
    private readonly bool _shotFacingRight;
    private readonly bool _host;
    private readonly string? _joinHost;
    private readonly string? _shotScreen;
    private readonly int _shotTalk; // --talk <npc>: which NPC the talk shot opens
    private readonly int _shotJob; // --job <id>: which class the job shot poses
    private readonly bool _shotHit;
    private readonly int _shotSkill; // --skill <id>: 0 means no skill pose
    private readonly int _shotSkillTab; // --skilltab: which job tab the skill window opens on
    private readonly bool _shotDrop; // --drop: swing until the first kill, show its drops
    private readonly bool _shotWear; // --wear: the demo wears its whole EQUIP bag
    private readonly int _shotAiticks; // --aiticks <n>: n 100ms AI steps at the posed feet

    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _scene = null!;
    private RenderTarget2D _blur = null!; // the screen at 1/4 size: the frosted glass source

    private MapData _map = null!;
    private int _mapId;
    private readonly Dictionary<string, CharaData> _charas = new();
    private readonly Dictionary<(string img, string action), CharaData> _gear = new();
    private readonly Dictionary<string, List<int>> _delays = new();
    private List<int> _layers = null!;
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly CameraComp _camera = new();
    private double _timeMs; // scrolling backs drift with this (0 in shot mode: still image)
    private int _loadBeat;
    private List<PortalComp> _portals = new(); // the draw list, aligned with _map.Portals: the anim state lives in the comps
    private List<NpcArt> _npcArts = new(); // live NPC views, one per spawn

    // The sim world: one root holding the player, the camera, the map
    // furniture, the mob/drop/peer views and the damage numbers.
    private readonly GameObject _world = new();
    private GameObject _mapObj = new(); // rebuilt per map load: portal + NPC children
    private readonly PlayerMover _mover = new();
    // The audio object: the music player is its component.
    private readonly GameObject _audio = new();
    private readonly BgmPlayer _bgm;
    private FootholdItem _spawnGround = null!;
    private string _shownAction = "stand1"; // last action the frames advanced on (resets the frame on change)
    private int _frame;
    private float _timerMs;
    private readonly Fighter _fighter = new(); // combat clocks, ticked at frame-top (never in the world step)
    private bool _canLeap = true; // War Leap: once per airtime, refreshed on ground or ladder

    // Net peers, keyed by server id. Empty in shot mode and after a dropped link.
    private GameServer? _server;
    private NetClient? _net;
    private ChatWindow _chatWindow = null!; // the chat is always on screen
    private GuideWindow _guideWindow = null!; // the contents guide window
    private GuideCardWindow _guideCardWindow = null!; // the small guide popup
    private WorldMapWindow _worldMapWindow = null!; // the world map window
    private readonly Dictionary<int, PeerArt> _remotes = new();
    private readonly PlateArt _heroPlate = new(); // the hero's tag rides the player object (SyncHeroPlate fills it)

    // Mobs, keyed by server uid. Shot mode and dropped links drive a local
    // MobWorld; net play applies server events.
    private MobWorld? _localWorld;
    private readonly Dictionary<int, MobArt> _mobs = new();
    private readonly Dictionary<int, MobData> _mobData = new();
    private readonly List<FloatingNumber> _numbers = new();
    private readonly List<SkillFx> _fx = new(); // skill effects in flight: casts, hit sparks, buff loops
    private readonly Stack<FloatingNumber> _numberPool = new(); // freelists: spawn pops, removal pushes, live order untouched
    private readonly Stack<SkillFx> _fxPool = new();
    private readonly Dictionary<int, DmgData> _dmg = new(); // damage skins by key, loaded once each

    private readonly Dictionary<int, DropArt> _drops = new();

    private readonly string _layoutPath;
    private readonly string _menuLayoutPath;
    private readonly string _charLayoutPath;
    private readonly string _jobLayoutPath;
    private readonly string _setupLayoutPath;
#if DEBUG
    private FileSystemWatcher? _layoutWatcher;
    private volatile bool _layoutDirty; // set by the watcher thread, consumed on the game thread
    private int _layoutRetry;           // ms to wait before a failed reload is tried again
    private volatile int _layoutTries;  // failed tries of the current save (the watcher thread resets it)
#endif

    public MushTownGame(string? shotPath, int shotMap, int shotTravel, string shotAction, int shotFrame, bool shotFacingRight, bool host, string? joinHost, string? shotScreen = null, int shotTalk = 0, bool shotHit = false, int shotSkill = 0, int shotSkillTab = 0, bool shotDrop = false, int shotAiticks = 0, bool shotWear = false, int shotJob = 0)
    {
        _shotPath = shotPath;
        _shotMap = shotMap;
        _shotTravel = shotTravel;
        _shotAction = shotAction;
        _shotFrame = shotFrame;
        _shotFacingRight = shotFacingRight;
        _host = host;
        _joinHost = joinHost;
        _shotScreen = shotScreen;
        _shotTalk = shotTalk;
        _shotJob = shotJob;
        _shotHit = shotHit;
        _shotSkill = shotSkill;
        _shotSkillTab = shotSkillTab;
        _shotDrop = shotDrop;
        _shotAiticks = shotAiticks;
        _shotWear = shotWear;
        _panelHosts = new[] { _invHost, _statHost, _skillHost, _questHost, _settingsHost, _shopHost }; // the panel-id order the Z row and the hit lookups use
        _assetsDir = FindAssetsDir();
        _bgm = new BgmPlayer(Path.Combine(_assetsDir, "sound"), _shotPath != null);
        _audio.Add(_bgm);
        string dir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "layouts");
        _layoutPath = Path.Combine(dir, "hud-layout.json");
        _menuLayoutPath = Path.Combine(dir, "menu-layout.json");
        _charLayoutPath = Path.Combine(dir, "charselect-layout.json");
        _jobLayoutPath = Path.Combine(dir, "jobselect-layout.json");
        _setupLayoutPath = Path.Combine(dir, "setup-layout.json");
        // The chat is the first window: the other windows open on top of it.
        // The chat sends the line to the server. Without a server (single
        // player) the line stays in the chat.
        _chatWindow = new ChatWindow(this, text =>
        {
            if (_net != null && _net.Connected)
            {
                // the chat target decides where the line goes: the world or
                // the party
                if (_chatWindow.PartyTarget) _net.SendPartyChat(text);
                else _net.SendChat(text);
                return;
            }
            _chatWindow.AddLine(_save?.Slots[_slot]?.Name ?? "Me", text);
        });
        new MinimapWindow(this); // the new windows keep themselves in the list
        _worldMapWindow = new WorldMapWindow(this);
        new KeyConfigWindow(this);
        _partyWindow = new PartyWindow(this);
        _partySearchWindow = new PartySearchWindow(this);
        _guideWindow = new GuideWindow(this);
        _guideCardWindow = new GuideCardWindow(this);
        _menuWindow = new MenuWindow(this);
        _partyAskWindow = new PartyAskWindow(this); // the invite popup raises itself when it opens
        // One raise row for everything that can come to the front: pinned
        // windows at the bottom, then the panels, then the other windows.
        foreach (var w in UiWindow.List) if (Pinned(w)) _raiseRow.Add(w.Host);
        _raiseRow.AddRange(_panelHosts);
        foreach (var w in UiWindow.List) if (!Pinned(w)) _raiseRow.Add(w.Host);
        RenumberRow();
        if (_shotPath == null) _rollLog ??= new RollLog(NetLogPath()); // the first reload below must be able to log a bad file
        TryReloadLayout();
#if DEBUG
        // One watcher for the folder: every layout file reloads on save.
        _layoutWatcher = new FileSystemWatcher(dir, "*.json");
        _layoutWatcher.Changed += (_, _) => { _layoutDirty = true; _layoutTries = 0; }; // a new save gets a fresh retry budget
        _layoutWatcher.Renamed += (_, _) => { _layoutDirty = true; _layoutTries = 0; }; // some editors save by rename
        _layoutWatcher.Created += (_, _) => { _layoutDirty = true; _layoutTries = 0; }; // ...or delete-then-create
        _layoutWatcher.EnableRaisingEvents = true;
#endif
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;

        // borderless fullscreen at desktop resolution; the game image itself
        // keeps its own size and gets scaled up to fill the screen
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        _graphics.PreferredBackBufferWidth = mode.Width;
        _graphics.PreferredBackBufferHeight = mode.Height;

        // The tree step order: the player moves first, then the map furniture
        // (portals), the mobs and the pooled numbers and skill effects, all of
        // them children of the world root. Only "the player first" matters (a
        // buff loop reads its position); the rest never depend on each other,
        // so the pooled ones tick in spawn order. Camera and audio ride the
        // tree but do not tick.
        var player = new GameObject();
        player.Add(_mover);
        player.Add(_heroPlate);
        _world.AddChild(player);
        var cam = new GameObject();
        cam.Add(_camera);
        _world.AddChild(cam);
        _world.AddChild(_audio); // the music player component
        Exiting += (_, _) => { SaveChara(); _net?.Dispose(); _server?.Dispose(); };
        if (_shotPath != null && OperatingSystem.IsWindows())
            HideWindowEarly(); // headless shots: hide before first paint (load takes seconds)
    }

    // A game event for the player: it goes to the log file and to the chat
    // window (the Etc. tab).
    internal void LogEvent(string line)
    {
        GameLog(line);
        _chatWindow.AddSystem(line);
    }

    // Hides every window of this process, every 25ms, until exit. One
    // mechanism: MainWindowHandle lags creation by ~1s, and hiding once
    // (Initialize/Update) still leaves the black window up through load.
    private static void HideWindowEarly()
    {
        uint pid = (uint)Environment.ProcessId;
        var t = new Thread(() =>
        {
            for (int i = 0; i < 1600; i++) // 40s cap; shot mode exits in seconds
            {
                EnumWindows((h, _) =>
                {
                    GetWindowThreadProcessId(h, out uint wpid);
                    if (wpid == pid && IsWindowVisible(h)) ShowWindow(h, 0);
                    return true;
                }, IntPtr.Zero);
                Thread.Sleep(25);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    protected override void Initialize()
    {
        Window.ClientSizeChanged += OnClientSizeChanged;

        LoadQuestData(); // NPC/quest json before maps (views validate placements)
        LoadManifest(); // the ship list loads before any scan proves against it
        LoadItemDefs(); // scanned roster before the doll art loop (and before customs validate rewards)
        ValidateCustomQuests();
        ValidateDropTables(); // after item defs (drop ids prove against them)
        LoadShops(); // keeper stocks prove against the same defs
        LoadSkillDefs();
        _mapId = _shotPath != null ? _shotMap : SpawnMapId;
        LoadMapData(_mapId);
        var artGears = _equips.Where(id => _items[id].HasArt).ToList();
        foreach (string action in Net.Actions)
        {
            var chara = LoadJson<CharaData>(Path.Combine(_assetsDir, "chara", "body", $"{action}.json"));
            if (chara.Frames.Count == 0)
                throw new InvalidDataException($"character {action} has no frames");
            if (chara.Frames.Any(f => f.Delay <= 0))
                throw new InvalidDataException($"character {action} has a bad delay");
            if (chara.Frames.Any(f => f.Parts.Count == 0))
                throw new InvalidDataException($"character {action} has an empty frame");
            _charas[action] = chara;
            _delays[action] = chara.Frames.Select(f => f.Delay).ToList();
            foreach (int id in artGears) // wearable art: same frames as beginner (delays ride the beginner file)
            {
                string imgId = id.ToString("D8");
                var gear = LoadJson<CharaData>(Path.Combine(_assetsDir, "chara", "gear", imgId, $"{action}.json"));
                if (gear.Frames.Count != chara.Frames.Count)
                    throw new InvalidDataException($"gear {imgId} {action} has {gear.Frames.Count} frames, want {chara.Frames.Count}");
                if (gear.Frames.Any(f => f.Parts.Count == 0))
                    throw new InvalidDataException($"gear {imgId} {action} has an empty frame");
                _gear[(imgId, action)] = gear;
            }
        }

        // player starts on the ground under the spawn point
        Spawn();
        // camera starts snapped on target (no opening swoop); Update eases it after moves
        SnapCamera();

        if (_host || _joinHost != null)
            StartNet(_host, _joinHost);

        if (_shotPath != null)
        {
            if (!_charas.ContainsKey(_shotAction))
                throw new InvalidDataException($"unknown action: {_shotAction}");
            // Job screens pose the spine clip, not the char: --frame is the clip frame there.
            // The setup page always draws the stand1 doll, whatever --action says.
            string shotChar = _shotScreen == "setup" ? "stand1" : _shotAction;
            if (_shotScreen is not ("job" or "job2") && _shotFrame >= _charas[shotChar].Frames.Count)
                throw new InvalidDataException($"action {shotChar} has no frame {_shotFrame}");
            _mover.Action = _shotAction;
            _frame = _shotFrame;
            _mover.FacingRight = _shotFacingRight;
            // a shot never types, so a shot named *balloon* shows a sample line
            if (Path.GetFileNameWithoutExtension(_shotPath)!.Contains("balloon"))
                ShowBalloon("Hi! I am going to Southperry now, see you soon!");
            // a shot named *guideboss* or *guidespecial* opens that guide tab
            string shotName = Path.GetFileNameWithoutExtension(_shotPath)!;
            if (shotName.Contains("guideboss")) _guideWindow.SetTab(1);
            if (shotName.Contains("guidespecial")) _guideWindow.SetTab(2);
            // a shot named *scroll* scrolls the map list of the world map
            if (shotName.Contains("scroll")) _worldMapWindow.ScrollToEnd();
            // the world map travel: the shot lands on the map it travelled to
            if (_shotTravel != 0)
            {
                TravelTo(_shotTravel);
                _loadBeat = 0; // shots skip Update: the black beat would never end
            }
        }

        InitializeMenu(_shotScreen);
        LoadAudio(); // sounds last: needs the save dir (settings)
        PlayScreenBgm(); // the login theme over the menus, the map track in the game
        base.Initialize();
    }

    // Screen size changed (resolution switch, moving to another monitor):
    // follow the new size so the upscaling stays correct.
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        var mode = GraphicsDevice.Adapter.CurrentDisplayMode;
        if (_graphics.PreferredBackBufferWidth == mode.Width &&
            _graphics.PreferredBackBufferHeight == mode.Height) return;

        _graphics.PreferredBackBufferWidth = mode.Width;
        _graphics.PreferredBackBufferHeight = mode.Height;
        _graphics.ApplyChanges();
    }

    // One map's json plus draw order plus spawn ground. Runs at startup and on
    // every map switch; textures load separately (see LoadMapTextures).
    private void LoadMapData(int mapId)
    {
        var map = LoadJson<MapData>(Path.Combine(_assetsDir, "maps", $"{mapId}.json"));
        // inside a map layer objects come before tiles, then by z, then by file order
        // (OrderBy is stable, so the file index stays the tiebreak) (FrmMapRender.cs:1353-1412)
        map.Objs = map.Objs.OrderBy(o => o.Layer).ThenBy(o => o.Z).ToList();
        map.Tiles = map.Tiles.OrderBy(t => t.Layer).ThenBy(t => t.Z).ToList();
        var layers = map.Tiles.Select(t => t.Layer).Concat(map.Objs.Select(o => o.Layer)).Distinct().OrderBy(l => l).ToList();
        var spawnGround = SceneMath.GroundBelow(map.Footholds, map.Info.SpawnX, map.Info.SpawnY)
            ?? throw new InvalidDataException($"map {mapId}: no foothold under spawn ({map.Info.SpawnX},{map.Info.SpawnY})");
        var portalDelays = map.Portals.Select(p => p.Frames.Select(f => f.Delay).ToList()).ToList();
        for (int i = 0; i < portalDelays.Count; i++)
            if (portalDelays[i].Any(x => x <= 0))
                throw new InvalidDataException($"map {mapId} portal {i} has a bad delay");
        // stale maps predate bgm/minimap: an empty image would load the assets
        // dir as a png, a zero width would divide by zero in ToDot
        if (map.Info.Bgm == "" || map.MiniMap.Image == "" || map.MiniMap.Width <= 0 || map.MiniMap.Height <= 0)
            throw new InvalidDataException($"map {mapId} is stale: re-run export");
        // all checks passed: commit everything at once, so a bad map leaves
        // the old map untouched and the menu can retry cleanly
        _mapId = mapId;
        _map = map;
        _mover.Map = map; // the feet read the same ground
        _layers = layers;
        _spawnGround = spawnGround;
        _world.RemoveChild(_mapObj); // old furniture leaves with the old map (first load: never added, false)
        _mapObj = new GameObject();
        _portals = new List<PortalComp>();
        _npcArts = new List<NpcArt>();
        for (int i = 0; i < map.Portals.Count; i++)
        {
            var o = new GameObject();
            _portals.Add(o.Add(new PortalComp { Portal = map.Portals[i], Delays = portalDelays[i] }));
            _mapObj.AddChild(o);
        }
        foreach (var s in map.Npcs)
        {
            var o = new GameObject();
            _npcArts.Add(o.Add(new NpcArt { Spawn = s }));
            _mapObj.AddChild(o);
        }
        _world.AddChild(_mapObj);
        EndTalkAndCheckNpcs(); // NPCs are static per map (talk ends with the map)
    }

    // Stand on this map's spawn ground.
    private void Spawn()
    {
        _mover.X = _map.Info.SpawnX;
        _mover.Ground = _spawnGround;
        _mover.Y = SceneMath.FootholdY(_mover.Ground, _map.Info.SpawnX);
        ResetMotion();
    }

    // Stop all motion: still speeds, no ladder, no drop-through, no swing.
    private void ResetMotion()
    {
        _mover.Reset();
        _fighter.AttackLock = 0;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        // PreserveContents: the blur copy leaves the scene for a moment, and a
        // plain render target is wiped every time it is bound again
        _scene = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight, false,
            SurfaceFormat.Color, DepthFormat.Depth16, 0, RenderTargetUsage.PreserveContents);
        _blur = new RenderTarget2D(GraphicsDevice, GameWidth / BlurBackdrop.Scale, GameHeight / BlurBackdrop.Scale);

        LoadMapTextures();
        foreach (var chara in _charas.Values)
            foreach (var frame in chara.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
        foreach (var gear in _gear.Values)
            foreach (var frame in gear.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
        LoadMenuContent();
        _gfx.Blur = _blur; // the frosted glass source for the MENU window
        bool gameShot = _shotScreen is null or "inv" or "use" or "stat" or "equip" or "hud" or "skill" or "quest" or "talk" or "settings" or "shop" or "minimap" or "worldmap" or "keys" or "menu" or "guide" or "guidecard" or "party" or "partysearch" or "partyask";
        if (_shotWear && !gameShot)
            throw new InvalidDataException("--wear needs a game shot (menu screens have no demo to dress)");
        if (_shotPath != null && gameShot) // game shots show mobs; menu shots don't
        {
            SetupShotMobs();
            SyncHeroPlate(); // staging is done (wear/buff/quest all staged the demo); shots skip Update
        }
    }

    private void LoadMapTextures()
    {
        // textures stay cached across maps (the map list is small, bounded); nothing unloads
        foreach (var item in _map.Backs) LoadTexture(item.Image);
        foreach (var item in _map.Tiles) LoadTexture(item.Image);
        foreach (var item in _map.Objs) LoadTexture(item.Image);
        foreach (var item in _map.Portals) foreach (var f in item.Frames) LoadTexture(f.Image);
        LoadTexture(_map.MiniMap.Image);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_shotPath != null)
        {
            base.Update(gameTime);
            return; // shots never update: no clicks, no saves, player prefs untouched
        }
        SyncCursor(); // a window resize rebuilds the OS cursor at the new scene zoom
        _ui.Trim(); // the text cache gives the old textures back before the draw
#if DEBUG
        if (_layoutDirty && (_layoutRetry -= (int)gameTime.ElapsedGameTime.TotalMilliseconds) <= 0)
        {
            if (TryReloadLayout()) { _layoutDirty = false; _layoutRetry = 0; _layoutTries = 0; }
            else if (++_layoutTries >= 5) { _layoutDirty = false; _layoutRetry = 0; _layoutTries = 0; } // the file stays bad: the log has the line, no loop
            else _layoutRetry = 300; // the editor still holds the file: try again, the reader only needs a free moment
        }
#endif
        if (_screen != Screen.Game)
        {
            UpdateMenu((float)gameTime.ElapsedGameTime.TotalMilliseconds);
            SyncCursorState((float)gameTime.ElapsedGameTime.TotalMilliseconds);
            base.Update(gameTime);
            return;
        }

        var kb = Keyboard.GetState();
        // One layer per press: the edge is taken here, before the chat sees the
        // keys. The chat keeps its own Esc (it leaves the input), so the layer
        // below runs only while the chat does not have the input.
        bool esc = FreshKey(kb, Keys.Escape, ref _escWasDown);
        // Esc closes one layer. The chat keeps its own Esc (it leaves the input),
        // so the layer runs here, before the poll: a frame that closes a window
        // must not eat the click or the key the player pressed with it.
        if (esc && !_chatWindow.Focused)
        {
            if (Holding) { ClearHeld(); PlaySfx("click"); base.Update(gameTime); return; } // the piece a click picked up goes back, nothing changes
            var escChara = _save?.Slots[_slot];
            if (escChara != null && !_hand.Empty) { _hand.Return(escChara); PlaySfx("click"); base.Update(gameTime); return; } // hands first: Esc drops the held piece back, panels stay
            if (_talkNpc != 0) { _talkNpc = 0; _talkQuest = 0; _talkPage = 0; _uiScene.ClearHover(); base.Update(gameTime); return; } // modal first: Esc closes talk before panels
            if (EscCloseFront()) { _uiScene.ClearHover(); PlaySfx("click"); base.Update(gameTime); return; } // one layer per press: the top of the raise row
            base.Update(gameTime); return; // nothing open: Esc rests (exit lives in the menu)
        }
        var ms = Mouse.GetState(); // the click edge and the game coordinates of this frame
        bool click = ClickEdge(ms);
        var (mx, my) = MouseGamePos(ms);
        bool held = ms.LeftButton == ButtonState.Pressed;
        InstallPendingKeys(); // a key window Save/Apply waits for this frame's poll
        _chatWindow.UpdateFrame(); // the wheel and a fold a click asked for last frame: that click already reached the open window
        _input.Capture = _chatWindow.Focused; // typing must not fire the actions the same keys bind
        _input.Poll(kb, new Point(mx, my), click, held, ms.ScrollWheelValue);
        // Enter starts a chat line: the input row only shows while one is typed.
        // The check sits before the chat block, so the Enter that sends a line
        // cannot open the next one.
        if (!_chatWindow.Focused && !_menuWindow.Show && _talkNpc == 0 && _input.Fresh(Keys.Enter))
            _chatWindow.FocusInput(kb);
        if (_chatWindow.Focused)
        {
            _chatWindow.UpdateKeys(kb, (float)gameTime.ElapsedGameTime.TotalMilliseconds);
            kb = new KeyboardState(); // the arrows and Up/Down must not move the character while typing
        }
        // Talk needs presence: walking out of reach closes the modal.
        if (_talkNpc != 0 && !_npcArts.Any(a => a.Spawn.NpcId == _talkNpc && InTalkReach(a)))
        {
            _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
            _talkHost.Enabled = false; // the one early layer write: this frame's hover and click run later and would still see the modal
        }
        if (RightEdge(ms) && Holding) { ClearHeld(); PlaySfx("click"); } // the other way to put the piece back
        UpdateHud(kb, click, held, mx, my);
        // The MENU window is modal for the character: while it is open the
        // character ignores the keys. The key window is not: editing keys
        // still leaves the saved preset live, so the character keeps acting
        // (MapleStory does the same), and Save swaps the table at the next
        // Update. The draft never reaches the live table before Save.
        bool blocked = _menuWindow.Show;
        // Talk runs after the HUD: a same-frame panel key and Talk both fire
        // (the HUD's guards see _talkNpc still 0; a modal window blocks Talk).
        if (_talkNpc == 0 && !blocked && !Holding && _input.Fresh(HotkeyAction.Talk)) TalkNearest(); // a piece in hand would float over the dialog
        if (blocked) kb = new KeyboardState();
        SyncCursorState((float)gameTime.ElapsedGameTime.TotalMilliseconds);
        if (_loadBeat > 0) _loadBeat--;

        _timeMs = gameTime.TotalGameTime.TotalMilliseconds;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float dtMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
        _fighter.Tick(dtMs);
        foreach (int id in _fighter.ExpiredBuffs) EndBuff(id); // same spot, same order (ticks run first now, ends after)
        if (_fighter.RegenDue) PayoutRegen();
        if (_save?.Slots[_slot] is CharaSlot c) // learn/expire shrinks the pool: clamp, never heal
            c.Hp = Math.Min(c.Hp, Progression.BuffedMaxHp(c.Level, OwnHpPct()));

        // fresh edges: portal tap, drop and ladder leap fire once per tap
        // (portal also fires on walking in with Up held; grab and jump use held keys)
        bool alt = !blocked && _input.Down(HotkeyAction.Jump);
        bool freshAlt = alt && _input.Fresh(HotkeyAction.Jump);
        bool up = kb.IsKeyDown(Keys.Up);
        bool freshUp = up && _input.Fresh(Keys.Up);
        bool down = kb.IsKeyDown(Keys.Down);
        // The pickup is a bindable action like the attack; a modal window
        // takes the keys, so Z does not pick a drop up there.
        bool freshZ = !blocked && _input.Fresh(HotkeyAction.Pickup);

        // Face the held direction before skills strike: same-frame turn+act
        // uses the new facing (the in-step update then agrees).
        int dirNow = (kb.IsKeyDown(Keys.Right) ? 1 : 0) - (kb.IsKeyDown(Keys.Left) ? 1 : 0);
        if (_fighter.AttackLock <= 0 && dirNow != 0 && _mover.Climb == null) _mover.FacingRight = dirNow > 0;

        // Ctrl swings the weapon (not on a ladder): the swing locks movement
        // for one playthrough; net play sends it, the local world judges it right here.
        // No swing without a judge (the disconnect frame has neither yet).
        if (!blocked && _input.Fresh(HotkeyAction.Attack) && _mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge())
        {
            _fighter.AttackLock = _delays["swingO1"].Sum();
            PlaySfx(SwingKey());
            if (_net != null && _net.Connected) _net.SendAttack();
            else if (_localWorld != null)
            {
                foreach (var h in _localWorld.Strike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, Random.Shared, OwnBuild()))
                    ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                DrainDrops(_localWorld);
            }
        }

        // Z grabs the nearest drop in reach: net play asks the server (a miss
        // answers nothing), the local world judges it right here. The client
        // pre-filters the judge box, so a nearer-but-out drop never blocks one.
        if (freshZ && HasJudge())
        {
            int best = 0;
            float bestD2 = float.MaxValue;
            foreach (var kv in _drops)
            {
                float dx = kv.Value.X - _mover.X, dy = kv.Value.Y - _mover.Y;
                if (Math.Abs(dx) > MobWorld.PickDx || Math.Abs(dy) > MobWorld.PickDy) continue;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestD2) { bestD2 = d2; best = kv.Key; }
            }
            if (best != 0)
            {
                if (_net != null && _net.Connected) _net.SendPickup(best);
                else if (_localWorld != null)
                {
                    var d = _localWorld.Pickup(best, _mapId, _mover.X, _mover.Y);
                    if (d != null) ApplyDropTake(d.Uid, true, d.Kind, d.Id, d.Count);
                }
            }
        }

        // A skill casts through the binding table: no hardcoded keys. The
        // skills have no default bindings, so this loop is idle until a key
        // is bound to one. Two skills on the same frame cast in ascending id
        // order, not in the table's insertion order.
        if (!blocked)
        {
            List<int>? fresh = null;
            foreach (var action in _input.Actions)
                if (action.Target.Kind == BindKind.Skill && action.Fresh) (fresh ??= new()).Add(action.Target.Id);
            if (fresh != null)
            {
                fresh.Sort();
                foreach (int id in fresh) CastSkill(id);
            }
        }

        // The feet step in Shared: snapshot in, outputs drained after. Skills
        // above already poked the state (leaps, dashes); travel and respawn
        // land after the step, same frame.
        _mover.Input = new PlayerInput
        {
            Dir = dirNow,
            Up = up,
            Down = down,
            JumpHeld = alt,
            JumpPressed = freshAlt,
            UpPressed = freshUp,
            Locked = _fighter.AttackLock > 0,
            Walk = OwnWalk(),
            Jump = OwnJump(),
        };
        TreeUpdate.UpdateObject(_world, dt);
        // Before the draw: a corpse and a finished effect both sit on
        // Frame == Frames.Count, and the draw reads that frame. A number does
        // not crash, it just keeps drawing and ageing. All three belong here.
        MobArt.CollectDead(_mobs, _world);
        PoolSweep.CollectDead(_numbers, _world, _numberPool);
        PoolSweep.CollectDead(_fx, _world, _fxPool);
        if (_mover.JumpSfx) PlaySfx("jump");
        if (_mover.Travel != null)
        {
            SwitchMap(_mover.Travel.Tm, _mover.Travel.Tn);
        }

        if (_mover.FellOut)
        {
            Console.WriteLine($"fell out of the world at ({_mover.X:F0},{_mover.Y:F0}), respawned at spawn");
            Spawn();
            SnapCamera(); // teleport: snap, don't swoosh across the map
        }
        if (_mover.Ground != null || _mover.Climb != null) _canLeap = true; // grounded or held: the next airtime gets its leap back

        if (_mover.Action != _shownAction)
        {
            _shownAction = _mover.Action;
            _frame = 0;
            _timerMs = 0;
        }
        if (_mover.Climb == null || _mover.ClimbMoving) // still on a ladder: hold the pose
            (_frame, _timerMs) = SceneMath.AdvanceFrame(_frame, _timerMs, dtMs, _delays[_shownAction]);

        var (targetX, targetY) = CameraTarget();
        _camera.Follow(dt, targetX, targetY);

        // peers: send our state every frame (24 bytes x 60Hz is nothing on LAN,
        // so no timers), apply theirs as-is (no interpolation yet)
        if (_net != null)
        {
            if (_net.Connected)
                _net.SendState(_mapId, _mover.X, _mover.Y, Net.ActionCode(_mover.Action), (byte)_frame, _mover.FacingRight);
            while (_net.TryTake(out var e))
                if (e.Gone)
                    RemovePeer(e.Id); // the name stays: ids never repeat, and a map-switch Gone is not a goodbye
                else if (e.Map != _mapId)
                    RemovePeer(e.Id); // peer on another map: not shown here
                else if (e.Frame < _charas[Net.ActionName(e.Action)].Frames.Count)
                    ApplyRemote(e); // bad frame: not ours, drop it
            while (_net.TryTakeName(out var n))
            {
                _names[n.Id] = n;
                if (_remotes.TryGetValue(n.Id, out var p)) // a live peer shows it now (later peers pull it at spawn)
                {
                    var plate = p.Owner.Get<PlateArt>()!; // spawn always hangs one
                    plate.HasName = true;
                    plate.Name = n.Name;
                    plate.Plate = n.Plate;
                }
            }
            while (_net.TryTakeChat(out var chat)) _chatWindow.AddLine(chat.Name, chat.Text);
            // the party: the member list, the invite list and the invite that
            // waits for an answer (the party chat lines are chat lines)
            while (_net.TryTakeParty(out var party)) SetPartyMembers(party.Members);
            while (_net.TryTakePartyPlayers(out var players)) SetPartyPlayers(players.Players);
            while (_net.TryTakePartyAsk(out var ask)) SetPartyAsk(ask);
            // the party share of a kill: the same exp path as our own kill
            while (_net.TryTakeExp(out int exp))
            {
                LogEvent($"party share +{exp} exp"); // the log line, like the other game events
                GainExp(exp);
            }
            // Quest events drain before mob hits: a level-up inside ApplyHit
            // re-syncs, and the sync must carry the counters this frame's
            // kills pushed (not wipe them on the server's copy).
            while (_net.TryTakeQuest(out var q))
            {
                if (q.Kill) ApplyQuestKill(q);
                else ApplyQuestOk(q);
            }
            while (_net.TryTakeMob(out var m))
            {
                if (m.Map != _mapId) continue; // another map's fight: not shown here
                if (m.Spawn) ApplySpawn(m.Uid, m.Mob, m.X, m.Y, m.MaxHp, m.HpLeft);
                else ApplyHit(m.Uid, m.Damage, m.HpLeft, m.Attacker == _net.PlayerId, m.Exp, m.Skill);
            }
            while (_net.TryTakeMove(out var mv))
            {
                if (mv.Map != _mapId) continue; // another map's walk: not shown here
                ApplyMove(mv.Uid, mv.X, mv.Y, mv.FacingRight);
            }
            while (_net.TryTakeHurt(out var h))
            {
                if (h.Map != _mapId) continue; // another map's touch: not ours (we moved since)
                if (!ApplyHurt(h.Damage)) continue;
                while (_net.TryTakeHurt(out _)) { } // died: drop touches judged at the corpse
                break;
            }
            while (_net.TryTakeDrop(out var d))
            {
                if (d.Map != _mapId) continue; // another map's loot: not shown here
                if (d.Spawn) ApplyDropSpawn(d.Uid, d.X, d.Y, d.Kind, d.Id, d.Count);
                else ApplyDropTake(d.Uid, d.Taker == _net.PlayerId, d.Kind, d.Id, d.Count);
            }
            while (_net.TryTakeFx(out var f))
            {
                if (f.Map != _mapId) continue; // another map's cast: not shown here
                SpawnCastFx(f.Skill, f.X, f.Y, f.FacingRight);
            }
            if (!_net.Connected && _remotes.Count > 0)
                ClearPeers(); // link lost: peers vanish, the local world takes over
            if (!_net.Connected && (PartyMembers.Length > 0 || PartyAsk != null || PartyPlayers.Length > 0))
                ClearParty(); // link lost: the party is gone with it
            if (!_net.Connected && _localWorld == null)
                StartLocalWorld(); // link lost: a fresh local world keeps the mobs working (kills come back: the drop resets the session)
        }

        // Local AI (dropped links tick it live; shots pose it frozen and
        // never reach Update): the world steps every frame and we apply its
        // events like net ones.
        if (_localWorld != null)
        {
            var players = new List<(int id, int map, float x, float y)> { (0, _mapId, _mover.X, _mover.Y) };
            var ev = _localWorld.Tick((int)dtMs, players);
            foreach (var mv in ev.Moves) ApplyMove(mv.Uid, mv.X, mv.Y, mv.FacingRight);
            foreach (var h in ev.Hurts)
                if (ApplyHurt(h.Damage))
                    break; // died: the rest were judged at the corpse
            ApplySpawns(ev.Spawns);
            foreach (var t in ev.Takes) if (_drops.Remove(t.uid, out var gone)) _world.RemoveChild(gone.Owner);
        }
        SyncHeroPlate();

        base.Update(gameTime);
    }

    // character around 70% down the screen, clamped to map bounds
    private (int x, int y) CameraTarget() => (
        SceneMath.ClampCamera((int)Math.Floor(_mover.X) - GameWidth / 2, _map.Info.VrLeft, _map.Info.VrRight, GameWidth),
        SceneMath.ClampCamera((int)Math.Floor(_mover.Y) - (int)(GameHeight * 0.7), _map.Info.VrTop, _map.Info.VrBottom, GameHeight));

    // Land on the ground under the target portal (same map or another one).
    // The exporter already proved the portal and its ground exist, so a miss
    // here means hand-edited assets.
    private void SwitchMap(int tm, string tn)
    {
        PlaySfx("portal");
        if (tm != _mapId) EnterMap(tm);
        var land = _map.Portals.FirstOrDefault(p => p.Pn == tn)
            ?? throw new InvalidDataException($"map {tm} has no portal '{tn}'");
        _mover.X = land.X;
        _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, land.X, land.Y)
            ?? throw new InvalidDataException($"map {tm} portal '{tn}' has no ground below");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, land.X);
        _mover.OnTravel();
        _fighter.AttackLock = 0;
        SnapCamera();
        ReseedPortalState(); // the arrival spot, not the old one
    }

    // The world map: travel to another map and land on its spawn point.
    internal void TravelTo(int mapId)
    {
        PlaySfx("portal");
        EnterMap(mapId);
        Spawn(); // the new map's spawn ground (the map data proved it exists)
        _mover.OnTravel(); // no instant re-travel through the portal next to it
        ReseedPortalState();
        SnapCamera();
    }

    // The arrival spot may be a portal: the mover must not step into it again.
    private void ReseedPortalState() =>
        _mover.WasInPortal = SceneMath.FindPortal(_map.Portals, _mover.X, _mover.Y) != null;

    // The map change: the data, the art, the mobs and the peers.
    private void EnterMap(int mapId)
    {
        LoadMapData(mapId);
        LoadMapTextures();
        PlayScreenBgm(); // the new map has its own track
        ClearPeers(); // peers stay on the old map; their live states refill this one
        if (_localWorld != null) RefreshLocalMobs(); // the local world re-lists the new map
        else { ClearMobs(); ClearDrops(); DropNumbers(); DropFx(); RespawnBuffLoops(); } // net: the server re-pushes them
        _worldMapWindow.OnMapChanged(); // the list follows the player and marks the map we stand on
        _loadBeat = LoadBeat; // real map change: black beat (same-map jumps stay instant)
    }

    private void SnapCamera()
    {
        var (x, y) = CameraTarget();
        _camera.Snap(x, y);
    }

    protected override void Draw(GameTime gameTime)
    {
        // draw the game at its own size, then blow the image up to the window
        GraphicsDevice.SetRenderTarget(_scene);
        if (_loadBeat > 0)
            GraphicsDevice.Clear(Color.Black);
        else if (_screen == Screen.Game)
            DrawScene();
        else
            DrawMenu();
        if (_screen == Screen.Game)
        {
            SyncLayers(); // whatever this frame changed (click, Esc, net, map, death) lands in the tree before the draw
            if (_menuWindow.Show) UpdateBlur(); // the frosted glass source, before the windows draw
            _spriteBatch.Begin(blendState: BlendState.NonPremultiplied);
            if (_shotPath == null)
            {
                var ms = Mouse.GetState();
                var (gx, gy) = MouseGamePos(ms);
                DrawHud(gx, gy, ms.LeftButton == ButtonState.Pressed);
            }
            else
            {
                var (mx, my) = ShotMouse();
                if (mx >= 0) { _uiScene.Layout(); _uiScene.Hover(mx, my); } // shots never run Update: the tips read the hover here
                DrawHud(mx, my, false); // no hand: clean art
            }
            _spriteBatch.End();
        }
        GraphicsDevice.SetRenderTarget(null);

        if (_shotPath == null)
        {
            BlitScene();
            base.Draw(gameTime);
            return;
        }

        // screenshot mode: blit into a target of window size, so the saved image is
        // exactly what the screen shows (back buffer read-back is broken in fullscreen)
        using var target = new RenderTarget2D(GraphicsDevice,
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight);
        GraphicsDevice.SetRenderTarget(target);
        BlitScene();
        GraphicsDevice.SetRenderTarget(null);
        SaveShot(target, _shotPath);
        Exit();
    }

    // The mouse of a shot: *hptip* hovers the HP bar (the text tip), *itemtip*
    // the ring in the demo bag (the item tooltip); every other shot has none.
    // Shots never run Update, so the hover has to be refreshed at the draw.
    private (int X, int Y) ShotMouse()
    {
        string name = Path.GetFileNameWithoutExtension(_shotPath!)!;
        if (name.Contains("hptip")) return (600, 720);
        if (name.Contains("itemtip")) return (1283, 376);
        return (-10, -10);
    }

    // A copy of the screen at 1/4 size: the MENU window draws it back up
    // over its own rect, and the upscale is the blur.
    private void UpdateBlur()
    {
        GraphicsDevice.SetRenderTarget(_blur);
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        _spriteBatch.Draw(_scene, new Rectangle(0, 0, _blur.Width, _blur.Height), Color.White);
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(_scene);
    }

    // Scales the 1366x768 game image up to fill the window, keeping the aspect ratio.
    private void BlitScene()
    {
        int screenW = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int screenH = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var (bx, by, w, h) = SceneMath.BlitRect(screenW, screenH, GameWidth, GameHeight);

        // the bars left over when the screen is not 1366x768-shaped
        GraphicsDevice.Clear(Color.Black);

        // Linear on purpose: the art is hand-drawn and the window scale is not
        // whole (2.8x on a 4K screen), so nearest leaves stair-step edges.
        // Linear smooths the art and costs nothing. Baked UI text gets softer.
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        _spriteBatch.Draw(_scene, new Rectangle(bx, by, w, h), Color.White);
        _spriteBatch.End();
    }

    internal static T LoadJson<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"missing exported json: {Path.GetFullPath(path)} (run: dotnet run --project MushTown.Exporter -- export)", path);

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"cannot parse json: {path}");
    }

    // True when the files were read: a save that lands while the editor still
    // holds the file fails here (IOException) and the caller tries again in a
    // moment. A file with a typo never reaches this: Guard and
    // PanelLayout.ReloadAll catch it per file and it comes back in the list.
    private bool TryReloadLayout()
    {
        try
        {
            foreach (string error in ReloadLayout()) GameLog($"layout: {error}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GameLog($"layout: {ex.Message}");
            return false; // the editor still holds the file, or it is half written: try again in 300 ms
        }

    }

    // File wins: reload positions and snap dragged panels back to them.
    private List<string> ReloadLayout()
    {
        var errors = new List<string>();
        Guard(errors, _layoutPath, () => Layout.Load(_layoutPath));
        Guard(errors, _menuLayoutPath, () => MenuLayout.Load(_menuLayoutPath));
        Guard(errors, _charLayoutPath, () => CharLayout.Load(_charLayoutPath));
        Guard(errors, _jobLayoutPath, () => JobLayout.Load(_jobLayoutPath));
        Guard(errors, _setupLayoutPath, () => SetupLayout.Load(_setupLayoutPath));
        errors.AddRange(PanelLayout.ReloadAll()); // the panel files (layouts/panel-*.json)
        if (_ui != null) _ui.RefreshFonts(); // null only on the ctor reload, before content loads
        _invPos = new Point(0, 0); _invPlaced = false; _invBuilt = false; // the inventory takes its spot from its own file again
        _statPos = new Point(0, 0); _statPlaced = false; _statBuilt = false; // the stat window takes its spot from its own file again
        _questPos = new Point(0, 0); _questDragged = false; _questBuilt = false; // the quest window takes its spot from its own file again
        _skillPos = new Point(0, 0); _skillDragged = false; _skillBuilt = false; // the skill window takes its spot from its own file again
        _settingsPos = SettingsLayoutPos(); _settingsBuilt = false;
        _shopPos = new Point(0, 0); _shopPlaced = false; _shopBuilt = false; // the shop window takes its spot from its own file again
        _dragInv = null; _dragStat = null; _dragSkill = null; _dragQuest = null; _dragSettings = null; _dragShop = null; // a reload mid-drag drops the grab
        foreach (var w in UiWindow.List) { w.Pos = w.LayoutPos(); w.Dirty = true; w.Drag = null; } // the new windows go back to the file spot
        if (_contentLoaded) BuildScreenScene(); // ctor reload skips: no textures yet
        return errors;
    }

    // One file with a typo must not stop the rest of the reload: the error goes
    // back to the caller (one line in the log) and the other files still load.
    private static void Guard(List<string> errors, string path, Action load)
    {
        try { load(); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { errors.Add($"{path}: {ex.Message}"); }
    }

    // The layouts folder (next to assets/). Every panel reads its own file
    // from here: layouts/panel-<name>.json.
    public static string LayoutsDir => Path.Combine(Path.GetDirectoryName(FindAssetsDir())!, "layouts");

    // The assets folder (the exported art and the json files).
    internal static string AssetsDir => FindAssetsDir();

    // The game data the new windows read.
    internal MapData GameMap => _map;
    internal float PlayerX => _mover.X;
    internal float PlayerY => _mover.Y;
    internal CharaSlot? Hero => _save?.Slots[_slot];
    internal int Meso => _save?.Slots[_slot]?.Mesos ?? 0;

    private static string FindAssetsDir()
    {
        // run from bin/Debug/net8.0 -> walk up to the repo root (where MushTown.sln is)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "assets");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"no assets folder above {AppContext.BaseDirectory} (run: dotnet run --project MushTown.Exporter -- export)");
    }

    private static void SaveShot(Texture2D image, string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var fs = File.Create(path);
        image.SaveAsPng(fs, image.Width, image.Height);
    }
}
