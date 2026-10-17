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
    private const float NumberMs = 800; // damage numbers rise and fade over this long
    private const int NumberRise = 60; // px a damage number floats up
    private const int BarWidth = 40; // mob HP bar width, px
    private const int BossBarW = 400; // boss HP bar width, px
    private const int FxDelay = 100; // skill effect frames share one delay (WZ ships no delays for them)
    private const int FxCastDy = 40; // cast effects center on the torso (bare frames: no WZ origins)
    private const int DashPx = 220; // Rush/Flash Blade carry the caster this far along facing
    private const float DiveV = 900f; // Leap Attack slams down this fast (past MaxFall on purpose)
    private const int LeapRange = 400; // Leap Attack needs a platform within this far below

    private const int SpawnMapId = 10000; // Maple Island: Southperry

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetsDir;
    private readonly string? _shotPath;
    private readonly int _shotMap;
    private readonly string _shotAction;
    private readonly int _shotFrame;
    private readonly bool _shotFacingRight;
    private readonly bool _host;
    private readonly string? _joinHost;
    private readonly string? _shotScreen;
    private readonly int _shotTalk; // --talk <npc>: which NPC the talk shot opens
    private readonly bool _shotHit;
    private readonly int _shotSkill; // --skill <id>: 0 means no skill pose
    private readonly int _shotSkillTab; // --skilltab: which job tab the skill window opens on
    private readonly bool _shotDrop; // --drop: swing until the first kill, show its drops
    private readonly bool _shotWear; // --wear: the demo wears its whole EQUIP bag
    private readonly int _shotAiticks; // --aiticks <n>: n 100ms AI steps at the posed feet

    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _scene = null!;

    private MapData _map = null!;
    private int _mapId;
    private readonly Dictionary<string, CharaData> _charas = new();
    private readonly Dictionary<(string img, string action), CharaData> _gear = new();
    private readonly Dictionary<string, List<int>> _delays = new();
    private List<int> _layers = null!;
    private readonly Dictionary<string, Texture2D> _textures = new();
    private int _cameraX, _cameraY;
    private float _camX, _camY, _camVX, _camVY;
    private double _timeMs; // scrolling backs drift with this (0 in shot mode: still image)
    private int _loadBeat;
    private List<(int frame, float timer)> _portalAnims = new(); // one anim state per portal, aligned with _map.Portals
    private List<List<int>> _portalDelays = new();

    // The sim world: one root object holding the player (mobs join in S5-S6).
    private readonly GameObject _world = new();
    private readonly PlayerMover _mover = new();
    private FootholdItem _spawnGround = null!;
    private string _shownAction = "stand2"; // last action the frames advanced on (resets the frame on change)
    private int _frame;
    private float _timerMs;
    private bool _altWasDown, _upWasDown, _ctrlWasDown;
    private bool _qWasDown, _wWasDown, _eWasDown, _rWasDown, _tWasDown, _yWasDown, _uWasDown, _fWasDown, _gWasDown, _hWasDown, _jWasDown, _lWasDown, _zWasDown;
    private readonly Fighter _fighter = new(); // combat clocks, ticked at frame-top (never in the world step)
    private bool _canLeap = true; // War Leap: once per airtime, refreshed on ground or ladder

    // Net peers, keyed by server id. Empty in shot mode and after a dropped link.
    private GameServer? _server;
    private NetClient? _net;
    private readonly Dictionary<int, RemoteEvent> _remotes = new();

    // Mobs, keyed by server uid. Shot mode and dropped links drive a local
    // MobWorld; net play applies server events.
    private MobWorld? _localWorld;
    private readonly Dictionary<int, MobView> _mobs = new();
    private readonly Dictionary<int, MobData> _mobData = new();
    private readonly List<DamageNumber> _numbers = new();
    private readonly List<SkillFx> _fx = new(); // skill cast effects + hit sparks in flight
    private DmgData? _dmg; // basic damage skin digits, loaded once

    private sealed class MobView
    {
        public int Mob;
        public float X;
        public float Y;
        public bool FacingRight; // AI poses face along (raw art faces left)
        public int Hp;
        public int MaxHp;
        public string Action = ""; // idle, hit1, die1
        public int Frame;
        public float Timer;
    }

    // One ground drop: a meso pile (Id = amount) or a potion (Id + Count).
    private sealed class DropView
    {
        public float X;
        public float Y;
        public int Kind;
        public int Id;
        public int Count;
    }
    private readonly Dictionary<int, DropView> _drops = new();

    private sealed class DamageNumber
    {
        public string Text = "";
        public float X; // head position when the hit landed
        public float Y;
        public float Age;
    }

    // One skill effect in flight: a cast effect (centered on the caster), a
    // hit spark (centered on the mob's body), or a buff loop (riding the
    // player until the buff ends). One-shots play once, then are removed.
    private sealed class SkillFx
    {
        public int Skill;
        public bool Hit;
        public bool Loop;
        public float X;
        public float Y;
        public bool FacingRight;
        public int Frame;
        public float Timer;
    }

    private readonly string _layoutPath;
    private readonly string _menuLayoutPath;
    private readonly string _charLayoutPath;
#if DEBUG
    private FileSystemWatcher? _layoutWatcher;
    private FileSystemWatcher? _menuLayoutWatcher;
    private FileSystemWatcher? _charLayoutWatcher;
    private volatile bool _layoutDirty; // set by the watcher thread, consumed on the game thread
#endif

    public MushTownGame(string? shotPath, int shotMap, string shotAction, int shotFrame, bool shotFacingRight, bool host, string? joinHost, string? shotScreen = null, int shotTalk = 0, bool shotHit = false, int shotSkill = 0, int shotSkillTab = 0, bool shotDrop = false, int shotAiticks = 0, bool shotWear = false)
    {
        _shotPath = shotPath;
        _shotMap = shotMap;
        _shotAction = shotAction;
        _shotFrame = shotFrame;
        _shotFacingRight = shotFacingRight;
        _host = host;
        _joinHost = joinHost;
        _shotScreen = shotScreen;
        _shotTalk = shotTalk;
        _shotHit = shotHit;
        _shotSkill = shotSkill;
        _shotSkillTab = shotSkillTab;
        _shotDrop = shotDrop;
        _shotAiticks = shotAiticks;
        _shotWear = shotWear;
        _assetsDir = FindAssetsDir();
        string dir = Path.Combine(Path.GetDirectoryName(_assetsDir)!, "layouts");
        _layoutPath = Path.Combine(dir, "hud-layout.json");
        _menuLayoutPath = Path.Combine(dir, "menu-layout.json");
        _charLayoutPath = Path.Combine(dir, "charselect-layout.json");
        TryReloadLayout();
#if DEBUG
        _layoutWatcher = new FileSystemWatcher(dir, Path.GetFileName(_layoutPath));
        _layoutWatcher.Changed += (_, _) => _layoutDirty = true;
        _layoutWatcher.Renamed += (_, _) => _layoutDirty = true; // some editors save by rename
        _layoutWatcher.Created += (_, _) => _layoutDirty = true; // ...or delete-then-create
        _layoutWatcher.EnableRaisingEvents = true;
        _menuLayoutWatcher = new FileSystemWatcher(dir, Path.GetFileName(_menuLayoutPath));
        _menuLayoutWatcher.Changed += (_, _) => _layoutDirty = true;
        _menuLayoutWatcher.Renamed += (_, _) => _layoutDirty = true;
        _menuLayoutWatcher.Created += (_, _) => _layoutDirty = true;
        _menuLayoutWatcher.EnableRaisingEvents = true;
        _charLayoutWatcher = new FileSystemWatcher(dir, Path.GetFileName(_charLayoutPath));
        _charLayoutWatcher.Changed += (_, _) => _layoutDirty = true;
        _charLayoutWatcher.Renamed += (_, _) => _layoutDirty = true;
        _charLayoutWatcher.Created += (_, _) => _layoutDirty = true;
        _charLayoutWatcher.EnableRaisingEvents = true;
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

        var player = new GameObject();
        player.Add(_mover);
        _world.AddChild(player);
        Exiting += (_, _) => { SaveChara(); _net?.Dispose(); _server?.Dispose(); };
        if (_shotPath != null && OperatingSystem.IsWindows())
            HideWindowEarly(); // headless shots: hide before first paint (load takes seconds)
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
            if (_shotFrame >= _charas[_shotAction].Frames.Count)
                throw new InvalidDataException($"action {_shotAction} has no frame {_shotFrame}");
            _mover.Action = _shotAction;
            _frame = _shotFrame;
            _mover.FacingRight = _shotFacingRight;
        }

        InitializeMenu(_shotScreen);
        LoadAudio(); // sounds last: needs the save dir (settings) and the map (BGM)
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
        var portalAnims = map.Portals.Select(_ => (0, 0f)).ToList();
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
        _portalAnims = portalAnims;
        _portalDelays = portalDelays;
        EndTalkAndCheckNpcs(); // NPCs are static per map (talk ends with the map)
        PlayBgm(); // same track across a switch keeps playing
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
        _scene = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);

        LoadMapTextures();
        foreach (var chara in _charas.Values)
            foreach (var frame in chara.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
        foreach (var gear in _gear.Values)
            foreach (var frame in gear.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
        LoadMenuContent();
        bool gameShot = _shotScreen is null or "inv" or "use" or "stat" or "hud" or "skill" or "quest" or "talk" or "settings" or "shop";
        if (_shotWear && !gameShot)
            throw new InvalidDataException("--wear needs a game shot (menu screens have no demo to dress)");
        if (_shotPath != null && gameShot) // game shots show mobs; menu shots don't
        {
            SetupShotMobs();
            SyncPanelScenes(); // staging (accept/open) runs after the scene build and shots skip Update
        }
    }

    // Shot mode: a frozen local world. --hit plants the player mid-swing next
    // to the first mob and lands one real strike (numbers and all); --skill
    // poses a skill at lv 1: a buff casts for real (loop + timer, no mobs
    // needed), a damage skill freezes mid-effect with judged hit sparks, a
    // utility (leap) only poses its effect and judges nothing.
    private void SetupShotMobs()
    {
        StartLocalWorld();
        SetupShotQuests(); // quest/talk screens run the real accept/open path (other shots no-op)
        if (_shotWear) // --wear runs the real Wear path on the demo (plain shots grow one, like --skill buffs)
        {
            _save ??= new SaveData();
            _save.Slots[_slot] ??= DemoChara();
            foreach (int id in _equips)
                if (!Wear(id)) throw new InvalidDataException($"--wear found no {id} in the demo bag");
        }
        if (_shotSkill != 0 && !_skills.ContainsKey(_shotSkill))
            throw new InvalidDataException($"unknown skill: {_shotSkill}");
        if (_shotSkill != 0 && _skills[_shotSkill].Kind == SkillData.KindBuff)
        {
            // buff shots run the real path (learn, pay, cast): a demo that
            // cannot afford the pose fails loud instead of faking it
            _save ??= new SaveData();
            var chara = _save.Slots[_slot] ??= DemoChara();
            var data = _skills[_shotSkill];
            if (!chara.SkillLv.ContainsKey(_shotSkill) && !Progression.LearnSkill(chara, data))
                throw new InvalidDataException($"--skill {_shotSkill} cannot learn: demo Lv.{chara.Level} needs Lv.{data.ReqLevel}");
            if (!TryPaySkill(_shotSkill))
                throw new InvalidDataException($"--skill {_shotSkill} cannot cast: needs Lv.{data.ReqLevel} and {data.Mp} MP");
            CastBuff(_shotSkill);
            RunShotAi();
            SnapCamera();
            return;
        }
        if (_shotHit || _shotSkill != 0 || _shotDrop)
        {
            var world = _localWorld!; // StartLocalWorld just built it
            var first = world.MobsOn(_mapId).FirstOrDefault()
                ?? throw new InvalidDataException($"--hit/--skill/--drop needs mobs, map {_mapId} has none");
            _mover.X = first.X - 50;
            _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, (int)first.Y)
                ?? throw new InvalidDataException($"--hit/--skill/--drop found no ground near mob {first.Mob}");
            _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
            ResetMotion();
            _mover.FacingRight = true;
            _mover.Action = "swingP1";
            _frame = 1;
            // fixed seed: the evidence shot shows the same number every run
            if (_shotSkill != 0)
            {
                if (_skills[_shotSkill].Kind == SkillData.KindPassive)
                    throw new InvalidDataException($"--skill {_shotSkill} is passive (no art to pose)");
                var chara = _save?.Slots[_slot];
                if (chara != null) // window shots learn and pay best-effort (cooldowns show); plain shots have no one
                {
                    if (!chara.SkillLv.ContainsKey(_shotSkill)) Progression.LearnSkill(chara, _skills[_shotSkill]);
                    TryPaySkill(_shotSkill);
                }
                SpawnCastFx(_shotSkill, _mover.X, _mover.Y, true);
                int n = _skills[_shotSkill].Effect.Count;
                if (n <= 5) throw new InvalidDataException($"--skill {_shotSkill} needs 6+ effect frames, got {n}");
                _fx[0].Frame = 5; // mid-effect: the small first frames prove nothing
                if (world.IsDamageSkill(_shotSkill))
                {
                    int pct = Progression.SkillDamage(_skills[_shotSkill], 1);
                    foreach (var h in world.SkillStrike(_mapId, _mover.X, _mover.Y, true, new Random(0), OwnBuild(), _shotSkill, pct))
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                    DrainDrops(world);
                }
            }
            else if (_shotDrop)
            {
                // swing until the first kill (one fixed seed: same corpse, same loot)
                var rng = new Random(0);
                bool killed = false;
                for (int i = 0; i < 20 && !killed; i++)
                    foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, rng, OwnBuild()))
                    {
                        ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                        killed |= h.HpLeft == 0;
                    }
                if (!killed) throw new InvalidDataException($"--drop killed nothing in 20 swings, map {_mapId}");
                DrainDrops(world);
            }
            else
            {
                foreach (var h in world.Strike(_mapId, _mover.X, _mover.Y, true, new Random(0), OwnBuild()))
                    ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
                DrainDrops(world);
            }
        }
        RunShotAi();
        SnapCamera();
    }

    // --aiticks <n>: n 100ms AI steps at the posed feet. Moves and respawns
    // show; touches don't (the demo must not die mid-pose). Zero ticks skips.
    private void RunShotAi()
    {
        var world = _localWorld!; // StartLocalWorld just built it
        var players = new List<(int id, int map, float x, float y)> { (0, _mapId, _mover.X, _mover.Y) };
        for (int i = 0; i < _shotAiticks; i++)
        {
            var ev = world.Tick(100, players);
            foreach (var mv in ev.Moves) ApplyMove(mv.Uid, mv.X, mv.Y, mv.FacingRight);
            ApplySpawns(ev.Spawns);
            foreach (var t in ev.Takes) _drops.Remove(t.uid);
        }
    }

    private void LoadMapTextures()
    {
        // textures stay cached across maps (6 maps, bounded); nothing unloads
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
#if DEBUG
        if (_layoutDirty)
        {
            _layoutDirty = false;
            TryReloadLayout();
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
        if (kb.IsKeyDown(Keys.Escape))
        {
            var escChara = _save?.Slots[_slot];
            if (escChara != null && !_hand.Empty) { _hand.Return(escChara); PlaySfx("click"); base.Update(gameTime); return; } // hands first: Esc drops the held piece back, panels stay
            if (_talkNpc != 0) { _talkNpc = 0; _talkQuest = 0; _talkPage = 0; base.Update(gameTime); return; } // modal first: Esc closes talk before panels
            if (_submenu.Enabled || _showInv || _showStat || _showSkill || _showQuest || _showSettings || _showShop || _panelOpen)
            {
                _submenu.Enabled = false; _showInv = false; _showStat = false; _showSkill = false;
                _showQuest = false; _showSettings = false; _showShop = false; _panelOpen = false;
                _dragInv = _dragStat = _dragSkill = _dragQuest = _dragSettings = _dragShop = null; // hiding mid-drag drops the grab
                PlaySfx("click");
                base.Update(gameTime); return;
            }
            base.Update(gameTime); return; // nothing open: Esc rests (exit lives in the menu)
        }
        // Talk needs presence: walking out of reach closes the modal.
        if (_talkNpc != 0 && !_map.Npcs.Any(n => n.NpcId == _talkNpc
            && Math.Abs(n.X - _mover.X) <= QuestLog.TalkDx && Math.Abs(n.Cy - _mover.Y) <= QuestLog.TalkDy))
        {
            _talkNpc = 0; _talkQuest = 0; _talkPage = 0;
        }
        var ms = Mouse.GetState(); // one click edge for the log panel and the HUD
        bool click = ClickEdge(ms);
        var (mx, my) = MouseGamePos(ms);
        if (!UpdateLogPanel(click, mx, my))
            UpdateHud(kb, click, ms.LeftButton == ButtonState.Pressed, mx, my);
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
        bool alt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        bool freshAlt = alt && !_altWasDown;
        _altWasDown = alt;
        bool up = kb.IsKeyDown(Keys.Up);
        bool freshUp = up && !_upWasDown;
        _upWasDown = up;
        bool down = kb.IsKeyDown(Keys.Down);
        bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        bool freshCtrl = ctrl && !_ctrlWasDown;
        _ctrlWasDown = ctrl;
        // Skill keys (Q/W/E/R/T/Y/U/F/G/H/J/L): one fresh edge each.
        bool freshQ = FreshKey(kb, Keys.Q, ref _qWasDown);
        bool freshW = FreshKey(kb, Keys.W, ref _wWasDown);
        bool freshE = FreshKey(kb, Keys.E, ref _eWasDown);
        bool freshR = FreshKey(kb, Keys.R, ref _rWasDown);
        bool freshT = FreshKey(kb, Keys.T, ref _tWasDown);
        bool freshY = FreshKey(kb, Keys.Y, ref _yWasDown);
        bool freshU = FreshKey(kb, Keys.U, ref _uWasDown);
        bool freshF = FreshKey(kb, Keys.F, ref _fWasDown);
        bool freshG = FreshKey(kb, Keys.G, ref _gWasDown);
        bool freshH = FreshKey(kb, Keys.H, ref _hWasDown);
        bool freshJ = FreshKey(kb, Keys.J, ref _jWasDown);
        bool freshL = FreshKey(kb, Keys.L, ref _lWasDown);
        bool freshZ = FreshKey(kb, Keys.Z, ref _zWasDown);

        // Face the held direction before skills strike: same-frame turn+act
        // uses the new facing (the in-step update then agrees).
        int dirNow = (kb.IsKeyDown(Keys.Right) ? 1 : 0) - (kb.IsKeyDown(Keys.Left) ? 1 : 0);
        if (_fighter.AttackLock <= 0 && dirNow != 0 && _mover.Climb == null) _mover.FacingRight = dirNow > 0;

        // Ctrl swings the polearm (not on a ladder): the swing locks movement
        // for one playthrough; net play sends it, the local world judges it right here.
        // No swing without a judge (the disconnect frame has neither yet).
        if (freshCtrl && _mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge())
        {
            _fighter.AttackLock = _delays["swingP1"].Sum();
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

        // Plain rings (Q Slash, T Brandish, G Intrepid, H Beam, L Scarring):
        // same judge gate as a swing, and the cast locks movement like one
        // (the pose follows from the lock). TryPaySkill runs last: no
        // judge, no charge.
        foreach ((bool fresh, int skill) in new[]
        {
            (freshQ, SkillData.SlashBlast), (freshT, SkillData.Brandish), (freshG, SkillData.IntrepidSlash),
            (freshH, SkillData.BeamBlade), (freshL, SkillData.ScarringSword),
        })
            if (fresh && _mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge() && TryPaySkill(skill))
                CastActive(skill);

        // W leaps again mid-air (never on the ground or a ladder): once per
        // airtime plus the cooldown, full jump velocity, drift unchanged.
        // No attack-lock check on purpose: the leap stays available mid-swing.
        if (freshW && _mover.Ground == null && _mover.Climb == null && _canLeap && TryPaySkill(SkillData.WarLeap))
        {
            _mover.Vy = OwnJump();
            _canLeap = false;
            PlaySfx("jump");
            SpawnCastFx(SkillData.WarLeap, _mover.X, _mover.Y, _mover.FacingRight);
            if (_net != null && _net.Connected) _net.SendSkillCast(SkillData.WarLeap, 0);
        }

        // E slams down from mid-air (never grounded): needs a platform in
        // range below, rings at once, then dives (drift steers the landing).
        if (freshE && _mover.Ground == null && _mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge() &&
            DiveGround() && TryPaySkill(SkillData.LeapAttack))
        {
            _mover.Vy = DiveV;
            CastActive(SkillData.LeapAttack);
        }

        // R/U buff (Iron Body, Spirit Blade): client-side timer + stats, the
        // cast relays for peers. Recast refreshes. No judge needed: landing
        // the buff never touches a mob.
        foreach ((bool fresh, int skill) in new[] { (freshR, SkillData.IronBody), (freshU, SkillData.SpiritBlade) })
            if (fresh && _mover.Climb == null && _fighter.AttackLock <= 0 && TryPaySkill(skill))
                CastBuff(skill);

        // F toggles Combo Attack: recast cancels free (orbs drop with it).
        if (freshF && _mover.Climb == null && _fighter.AttackLock <= 0)
        {
            if (_fighter.Buffs.ContainsKey(SkillData.ComboAttack)) EndBuff(SkillData.ComboAttack);
            else if (TryPaySkill(SkillData.ComboAttack)) CastBuff(SkillData.ComboAttack);
        }

        // Y/J dash then ring at the landing (Flash Blade, Rush).
        foreach ((bool fresh, int skill) in new[] { (freshY, SkillData.FlashBlade), (freshJ, SkillData.Rush) })
            if (fresh && _mover.Climb == null && _fighter.AttackLock <= 0 && HasJudge() && TryPaySkill(skill))
            {
                Dash();
                CastActive(skill);
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
        if (_mover.JumpSfx) PlaySfx("jump");
        if (_mover.Travel != null)
        {
            SwitchMap(_mover.Travel.Tm, _mover.Travel.Tn);
            _mover.WasInPortal = SceneMath.FindPortal(_map.Portals, _mover.X, _mover.Y) != null; // arrival spot, not the old one
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
        for (int i = 0; i < _map.Portals.Count; i++) // portal gates shimmer on their own loop
            if (_map.Portals[i].Frames.Count > 0)
                _portalAnims[i] = SceneMath.AdvanceFrame(_portalAnims[i].frame, _portalAnims[i].timer, dtMs, _portalDelays[i]);

        // Mob anims: idles loop, flinch returns to idle, death removes the body.
        foreach (int uid in _mobs.Keys.ToList())
        {
            var m = _mobs[uid];
            var frames = MobFrames(m.Mob, m.Action);
            m.Timer += dtMs;
            if (m.Timer < frames[m.Frame].Delay) continue;
            m.Timer -= frames[m.Frame].Delay; // keep the overflow (single step: delays run >= 100ms, ticks 16ms)
            if (++m.Frame < frames.Count) continue;
            if (m.Action == "die1") _mobs.Remove(uid);
            else
            {
                if (m.Action == "hit1") m.Action = MobIdle(m.Mob);
                m.Frame = 0;
            }
        }
        for (int i = _numbers.Count - 1; i >= 0; i--)
        {
            _numbers[i].Age += dtMs;
            if (_numbers[i].Age >= NumberMs) _numbers.RemoveAt(i);
        }
        for (int i = _fx.Count - 1; i >= 0; i--) // skill effects play once, then leave
        {
            var f = _fx[i];
            if (f.Loop) // buff loops ride the player and wrap until the buff ends
            {
                f.X = _mover.X;
                f.Y = _mover.Y - FxCastDy;
                f.Timer += dtMs;
                if (f.Timer < FxDelay) continue;
                f.Timer -= FxDelay;
                var loop = _skills[f.Skill].Affected;
                f.Frame = (f.Frame + 1) % loop.Count;
                continue;
            }
            var frames = f.Hit ? _skills[f.Skill].Hit : _skills[f.Skill].Effect;
            f.Timer += dtMs;
            if (f.Timer < FxDelay) continue;
            f.Timer -= FxDelay; // keep the overflow (single step: 100ms delays, 16ms ticks)
            if (++f.Frame < frames.Count) continue;
            _fx.RemoveAt(i);
        }

        var (targetX, targetY) = CameraTarget();
        (_camX, _camVX) = SceneMath.SpringStep(_camX, _camVX, targetX, dt);
        (_camY, _camVY) = SceneMath.SpringStep(_camY, _camVY, targetY, dt);
        _cameraX = (int)Math.Round(_camX);
        _cameraY = (int)Math.Round(_camY);

        // peers: send our state every frame (24 bytes x 60Hz is nothing on LAN,
        // so no timers), apply theirs as-is (no interpolation yet)
        if (_net != null)
        {
            if (_net.Connected)
                _net.SendState(_mapId, _mover.X, _mover.Y, Net.ActionCode(_mover.Action), (byte)_frame, _mover.FacingRight);
            while (_net.TryTake(out var e))
                if (e.Gone)
                    _remotes.Remove(e.Id); // the name stays: ids never repeat, and a map-switch Gone is not a goodbye
                else if (e.Map != _mapId)
                    _remotes.Remove(e.Id); // peer on another map: not shown here
                else if (e.Frame < _charas[Net.ActionName(e.Action)].Frames.Count)
                    _remotes[e.Id] = e; // bad frame: not ours, drop it
            while (_net.TryTakeName(out var n))
                _names[n.Id] = n;
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
                _remotes.Clear(); // link lost: peers vanish, the local world takes over
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
            foreach (var t in ev.Takes) _drops.Remove(t.uid);
        }

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
        if (tm != _mapId)
        {
            LoadMapData(tm);
            LoadMapTextures();
            _remotes.Clear(); // peers stay on the old map; their live states refill this one
            if (_localWorld != null) RefreshLocalMobs(); // the local world re-lists the new map
            else { _mobs.Clear(); _drops.Clear(); _numbers.Clear(); _fx.Clear(); RespawnBuffLoops(); } // net: the server re-pushes them
            _loadBeat = LoadBeat; // real map change: black beat (same-map jumps stay instant)
        }
        var land = _map.Portals.FirstOrDefault(p => p.Pn == tn)
            ?? throw new InvalidDataException($"map {tm} has no portal '{tn}'");
        _mover.X = land.X;
        _mover.Ground = SceneMath.ArrivalGround(_map.Footholds, land.X, land.Y)
            ?? throw new InvalidDataException($"map {tm} portal '{tn}' has no ground below");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, land.X);
        _mover.OnTravel();
        _fighter.AttackLock = 0;
        SnapCamera();
    }

    private void SnapCamera()
    {
        var (x, y) = CameraTarget();
        _camX = x;
        _camY = y;
        _camVX = 0;
        _camVY = 0;
        _cameraX = x;
        _cameraY = y;
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
            _spriteBatch.Begin(blendState: BlendState.NonPremultiplied);
            if (_shotPath == null)
            {
                var ms = Mouse.GetState();
                var (gx, gy) = MouseGamePos(ms);
                DrawHud(gx, gy, ms.LeftButton == ButtonState.Pressed);
                DrawLogOverlay(gx, gy, ms.LeftButton == ButtonState.Pressed);
            }
            else DrawHud(-10, -10, false); // shots: clean art, no hover, no log
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

    // Scales the 1366x768 game image up to fill the window, keeping the aspect ratio.
    private void BlitScene()
    {
        int screenW = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int screenH = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var (bx, by, w, h) = SceneMath.BlitRect(screenW, screenH, GameWidth, GameHeight);

        // the bars left over when the screen is not 1366x768-shaped
        GraphicsDevice.Clear(Color.Black);

        // Point sampling on purpose: the magnified pixels stay sharp instead of blurring.
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_scene, new Rectangle(bx, by, w, h), Color.White);
        _spriteBatch.End();
    }

    private void DrawScene()
    {
        // sky colour: only visible where the map's own backgrounds do not reach
        GraphicsDevice.Clear(new Color(120, 170, 210));

        // exported png files carry straight (not premultiplied) alpha
        _spriteBatch.Begin(blendState: BlendState.NonPremultiplied);

        DrawMapBack();

        // the player, then each peer, then each mob: current frames, actors
        // by feet height (lower feet draw in front; ties keep this order)
        int feetX = (int)Math.Floor(_mover.X), feetY = (int)Math.Floor(_mover.Y);
        var draws = new List<(int y, Action draw)>();
        var hero = _save?.Slots[_slot];
        draws.Add((feetY, () =>
        {
            DrawAvatar(feetX, feetY, _mover.Action, _frame, _mover.FacingRight, hero?.Equips);
            if (hero != null)
                DrawName(feetX - _cameraX, feetY + NameDown - _cameraY, hero.Name, PlateFor(hero.Equips));
        }
        ));
        foreach (var r in _remotes.Values.OrderBy(r => r.Id)) // peers show beginner art (only the name plate crosses, via Name)
        {
            int rx = (int)Math.Floor(r.X), ry = (int)Math.Floor(r.Y);
            draws.Add((ry, () =>
            {
                DrawAvatar(rx, ry, Net.ActionName(r.Action), r.Frame, r.FacingRight);
                if (_names.TryGetValue(r.Id, out var n))
                    DrawName(rx - _cameraX, ry + NameDown - _cameraY, n.Name, n.Plate);
            }
            ));
        }
        foreach (int uid in _mobs.Keys.OrderBy(u => u))
        {
            var m = _mobs[uid];
            draws.Add(((int)Math.Floor(m.Y), () => DrawMob(m)));
        }
        foreach (int uid in _drops.Keys.OrderBy(u => u))
        {
            var d = _drops[uid];
            draws.Add(((int)Math.Floor(d.Y), () => DrawDrop(d)));
        }
        foreach (var n in _map.Npcs)
            draws.Add((n.Cy, () => DrawNpc(n)));
        foreach ((int _, Action draw) in draws.OrderBy(d => d.y))
            draw();

        DrawMapFront();
        DrawFx();
        DrawNumbers();

        _spriteBatch.End();
    }

    private void DrawMob(MobView m)
    {
        var frame = MobFrames(m.Mob, m.Action)[m.Frame];
        var tex = _textures[frame.Image];
        // WZ mob art has no origin: center-bottom anchor at the feet
        int x = (int)Math.Floor(m.X) - tex.Width / 2;
        int y = (int)Math.Floor(m.Y) - tex.Height;
        DrawTexture(frame.Image, x, y, m.FacingRight); // raw art faces left; the AI faces along
        if (m.Hp >= m.MaxHp || m.Hp <= 0) return; // fresh and dying mobs hide their bar
        int px = (int)Math.Floor(m.X) - BarWidth / 2 - _cameraX, py = y - 8 - _cameraY;
        _spriteBatch.Draw(_pixel, new Rectangle(px, py, BarWidth, 5), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(px + 1, py + 1, Math.Max(1, (int)((BarWidth - 2) * (float)m.Hp / m.MaxHp)), 3), new Color(80, 220, 80));
    }

    // One NPC: stand sprite on its origin-anchored feet, name tag above,
    // gold "!" when it has quest work (an offer or a ready turn-in).
    private void DrawNpc(NpcSpawn n)
    {
        var data = _npcs[n.NpcId];
        var tex = _textures[$"npc/{n.NpcId}.png"];
        int feet = n.X;
        // Raw art faces left: flipped feet mirror around the origin (avatar parts do the same).
        int x = n.FacingRight ? SceneMath.FlipDrawX(feet, -data.OriginX, tex.Width) : feet - data.OriginX;
        int y = n.Cy - data.OriginY;
        DrawTexture($"npc/{n.NpcId}.png", x, y, n.FacingRight);
        var tag = _ui.GetSmall(data.Name);
        int feetX = feet - _cameraX, tagY = y - 20 - _cameraY;
        _spriteBatch.Draw(_pixel,
            new Rectangle(feetX - tag.Width / 2 - 3, tagY, tag.Width + 6, 16),
            new Color(0, 0, 0, 120));
        _spriteBatch.Draw(tag, new Vector2(feetX - tag.Width / 2, tagY), Color.White);
        if (NpcHasWork(n.NpcId))
        {
            var mark = _ui.GetSmall("!");
            _spriteBatch.Draw(mark, new Vector2(feetX - mark.Width / 2, tagY - 16),
                new Color(255, 220, 0));
        }
    }

    // Click an NPC in talk reach: the talk panel opens (too far: nothing).
    private void NpcClick(int gx, int gy)
    {
        foreach (var n in _map.Npcs.OrderByDescending(n => n.Cy)) // draw order: high feet on top, clicks match
        {
            var data = _npcs[n.NpcId];
            var tex = _textures[$"npc/{n.NpcId}.png"];
            int feet = n.X;
            int x = n.FacingRight ? SceneMath.FlipDrawX(feet, -data.OriginX, tex.Width) : feet - data.OriginX;
            var rect = new Rectangle(x - _cameraX, n.Cy - data.OriginY - _cameraY,
                tex.Width, tex.Height);
            if (!HitRect(rect, gx, gy)) continue;
            if (Math.Abs(n.X - _mover.X) > QuestLog.TalkDx || Math.Abs(n.Cy - _mover.Y) > QuestLog.TalkDy) continue; // an overlapped NPC behind may be in reach
            _talkNpc = n.NpcId;
            _talkQuest = 0;
            _talkPage = 0;
            _submenu.Enabled = false;
            PlaySfx("click");
            return;
        }
    }

    // One ground drop: the sprite sits on its feet (coin or potion icon).
    private void DrawDrop(DropView d)
    {
        string image = d.Kind == DropKind.Mesos ? "ui/hud_coin.png" : _items[d.Id].Image;
        var tex = _textures[image];
        int x = (int)Math.Floor(d.X) - tex.Width / 2;
        int y = (int)Math.Floor(d.Y) - tex.Height;
        DrawTexture(image, x, y, false);
    }

    // Skill effects: bare frames centered on their anchor, flipped like
    // avatars (raw art faces left). Cast effects ride the caster, hit sparks
    // the mob's body, buff loops the player; all were centered when spawned.
    private void DrawFx()
    {
        foreach (var f in _fx)
        {
            var frames = f.Loop ? _skills[f.Skill].Affected : f.Hit ? _skills[f.Skill].Hit : _skills[f.Skill].Effect;
            var tex = _textures[frames[f.Frame]];
            var o = new Vector2(tex.Width / 2f, tex.Height / 2f);
            _spriteBatch.Draw(tex, new Vector2(f.X - _cameraX, f.Y - _cameraY), null, Color.White,
                0f, o, 1f, f.FacingRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }

    // Damage numbers: basic skin digits, rising and fading over NumberMs.
    private void DrawNumbers()
    {
        if (_numbers.Count == 0) return;
        var dmg = LoadDmg();
        foreach (var n in _numbers)
        {
            float k = n.Age / NumberMs;
            int alpha = k < 0.7f ? 255 : (int)(255 * (1 - (k - 0.7f) / 0.3f));
            var tint = new Color(255, 255, 255, alpha);
            int totalW = 0;
            foreach (char c in n.Text) totalW += _textures[dmg.Digits[c - '0'].Image].Width;
            float x = n.X - totalW / 2f - _cameraX;
            float baseY = n.Y - NumberRise * k - _cameraY;
            foreach (char c in n.Text)
            {
                var d = dmg.Digits[c - '0'];
                var tex = _textures[d.Image];
                _spriteBatch.Draw(tex, new Vector2(x, baseY - d.Oy), null, tint,
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                x += tex.Width;
            }
        }
    }

    // Basic damage skin: ten digits plus their baseline offsets, loaded once.
    private DmgData LoadDmg()
    {
        if (_dmg == null)
        {
            var data = LoadJson<DmgData>(Path.Combine(_assetsDir, "effect", "dmg.json"));
            if (data.Digits.Count != 10)
                throw new InvalidDataException($"damage skin has {data.Digits.Count} digits, want 10");
            foreach (var d in data.Digits) LoadTexture(d.Image);
            _dmg = data;
        }
        return _dmg;
    }

    // One mob's art: json plus textures, loaded once. The idle is stand, or
    // move when the mob has no stand (the blue snail crawls everywhere).
    private string LoadMob(int mob)
    {
        if (!_mobData.TryGetValue(mob, out var data))
        {
            data = LoadJson<MobData>(Path.Combine(_assetsDir, "mob", $"{mob}.json"));
            if (!data.Actions.Any(a => a.Name is "stand" or "move"))
                throw new InvalidDataException($"mob {mob} has no idle action");
            foreach (string need in new[] { "hit1", "die1" })
                if (!data.Actions.Any(a => a.Name == need))
                    throw new InvalidDataException($"mob {mob} has no {need} action");
            foreach (var a in data.Actions)
            {
                if (a.Frames.Count == 0)
                    throw new InvalidDataException($"mob {mob} {a.Name} has no frames");
                if (a.Frames.Any(f => f.Delay <= 0))
                    throw new InvalidDataException($"mob {mob} {a.Name} has a bad delay");
            }
            foreach (var a in data.Actions)
                foreach (var f in a.Frames)
                    LoadTexture(f.Image);
            _mobData[mob] = data;
        }
        return MobIdle(mob);
    }

    private string MobIdle(int mob) =>
        _mobData[mob].Actions.Any(a => a.Name == "stand") ? "stand" : "move";

    private List<MobFrame> MobFrames(int mob, string state) =>
        _mobData[mob].Actions.FirstOrDefault(a => a.Name == state)?.Frames
            ?? throw new InvalidDataException($"mob {mob} has no {state} action");

    // A spawn the server pushed (or the local world listed): the view starts
    // at the live HP, so late joiners see wounded mobs as wounded.
    private void ApplySpawn(int uid, int mob, float x, float y, int maxHp, int hp)
    {
        string idle = LoadMob(mob);
        _mobs[uid] = new MobView { Mob = mob, X = x, Y = y, Hp = hp, MaxHp = maxHp, Action = idle };
    }

    // A hit the server judged (or the local world struck): flinch or die, plus
    // a number. A hit for a view we never saw is dropped: a kill in flight
    // across a remap, or a join racing a swing on another thread.
    // mine + dead means we landed the killing blow: claim the EXP.
    // A skill hit also plants the skill's spark on the mob's body (skills
    // without spark art, like Scarring Sword, skip it). Our own hits feed
    // the combo orb while Combo Attack lives (its bonus hits don't recurse).
    private void ApplyHit(int uid, int damage, int hpLeft, bool mine, int exp, int skill)
    {
        if (!_mobs.TryGetValue(uid, out var m)) return;
        m.Hp = hpLeft;
        m.Action = hpLeft == 0 ? "die1" : "hit1";
        m.Frame = 0;
        m.Timer = 0;
        PlaySfx(hpLeft == 0 ? SoundKey.MobDie(m.Mob, _mobData[m.Mob].Boss) : "hit");
        float bodyH = _textures[MobFrames(m.Mob, m.Action)[0].Image].Height;
        float headY = m.Y - bodyH;
        _numbers.Add(new DamageNumber { Text = damage.ToString(), X = m.X, Y = headY });
        if (skill != 0 && _skills[skill].Hit.Count > 0)
            _fx.Add(new SkillFx { Skill = skill, Hit = true, X = m.X, Y = m.Y - bodyH / 2, FacingRight = true });
        if (mine && skill != SkillData.FinalAttack && _fighter.Buffs.ContainsKey(SkillData.ComboAttack))
        {
            int chance = _skills[SkillData.ComboAttack].OrbChance * SkillLv(SkillData.ComboAttack)
                + _skills[SkillData.ComboSynergy].SynChance * SkillLv(SkillData.ComboSynergy);
            if (Random.Shared.NextDouble() * 100 < chance) // visible count: rolled client-side, shown in the window
                _fighter.Orbs = Math.Min(_fighter.Orbs + 1, _skills[SkillData.ComboAttack].OrbMax);
        }
        if (hpLeft == 0 && mine) { GainExp(exp); BumpQuestKills(m.Mob); }
    }

    // One AI step: snap the pose, face along. hit1/die1 own the pose until
    // they finish (a move never steals a flinch or mirrors a corpse), but
    // the feet still follow.
    // A move for a view we never saw is dropped, like a hit. A changed
    // action restarts its frames (stand/move counts differ per mob).
    private void ApplyMove(int uid, float x, float y, bool facingRight)
    {
        if (!_mobs.TryGetValue(uid, out var m)) return;
        m.X = x;
        m.Y = y;
        if (m.Action == "hit1" || m.Action == "die1") return; // flinch/death own the pose
        m.FacingRight = facingRight;
        if (m.Action != "move")
        {
            m.Action = "move";
            m.Frame = 0;
            m.Timer = 0;
        }
    }

    // One touch that landed on us: DEF folds in, the number shows what got
    // through, death respawns at the map spawn with full pools (buffs stay).
    // Returns true on death: the caller drops the rest of the frame's touches
    // (judged at the corpse, not the spawn).
    private bool ApplyHurt(int damage)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return false; // CLI sessions have no one to hurt
        int dealt = Progression.ApplyHurt(chara, damage, OwnDef());
        if (dealt == 0) return false; // harmless touch: no number, no death
        _numbers.Add(new DamageNumber { Text = dealt.ToString(), X = _mover.X, Y = _mover.Y - 90 });
        if (chara.Hp != 0) return false;
        Die(chara);
        return true;
    }

    // Death: full pools, feet back on the spawn ground, motion cleared.
    private void Die(CharaSlot chara)
    {
        chara.Hp = Progression.BuffedMaxHp(chara.Level, OwnHpPct());
        chara.Mp = Progression.MaxMp(chara.Level);
        _mover.X = _map.Info.SpawnX;
        _mover.Ground = SceneMath.GroundBelow(_map.Footholds, _map.Info.SpawnX, _map.Info.SpawnY)
            ?? throw new InvalidDataException($"map {_mapId} has no ground under spawn");
        _mover.Y = SceneMath.FootholdY(_mover.Ground, _map.Info.SpawnX);
        ResetMotion();
        SnapCamera();
        _talkNpc = 0; _talkQuest = 0; _talkPage = 0; // death teleports: no reload on the same map, so end talk here
        GameLog("you died");
    }

    // DEF% from every source: worn gear, Iron Body while live, Guardian
    // Armor and Endure always. Mob touches are the only thing it blocks.
    private int OwnDef() =>
        ItemData.EquipBonus(_save?.Slots[_slot]?.Equips, _items).def
        + (_fighter.Buffs.ContainsKey(SkillData.IronBody) ? _skills[SkillData.IronBody].DefPct * SkillLv(SkillData.IronBody) : 0)
        + _skills[SkillData.GuardianArmor].DefPct * SkillLv(SkillData.GuardianArmor)
        + _skills[SkillData.Endure].DefPct * SkillLv(SkillData.Endure);

    // One drop landed: mesos draw the HUD coin, potions their own icon.
    // Fresh kill drops land in view: every local judge drains right after
    // striking (net play gets them pushed instead).
    private void DrainDrops(MobWorld world)
    {
        foreach (var d in world.TakeDrops())
            ApplyDropSpawn(d.Uid, d.X, d.Y, d.Kind, d.Id, d.Count);
    }

    // Respawn arrivals for this view (other maps' graves tick too, but their
    // spawns are not ours to show).
    private void ApplySpawns(IEnumerable<MobWorld.Spawn> spawns)
    {
        foreach (var s in spawns)
        {
            if (s.Map != _mapId) continue;
            ApplySpawn(s.Uid, s.Mob, s.X, s.Y, s.MaxHp, s.Hp);
        }
    }

    private void ApplyDropSpawn(int uid, float x, float y, int kind, int id, int count) =>
        _drops[uid] = new DropView { X = x, Y = y, Kind = kind, Id = id, Count = count };

    // One drop gone: everyone clears the view, the taker claims the loot.
    // Mesos pile up; known drops land in a bag (equips in EQUIP, potions in USE).
    private void ApplyDropTake(int uid, bool mine, int kind, int id, int count)
    {
        _drops.Remove(uid);
        var chara = _save?.Slots[_slot];
        if (!mine || chara == null) return;
        PlaySfx("pickup");
        if (kind == DropKind.Mesos)
        {
            chara.Mesos += id;
            GameLog($"picked up {id} mesos");
        }
        else // the server only drops known ids
        {
            var bag = _items[id].Slot != null
                ? chara.EquipBag ??= new Dictionary<int, int>()
                : chara.Use ??= new Dictionary<int, int>();
            bag.TryGetValue(id, out int have);
            bag[id] = have + count;
            GameLog($"picked up {_items[id].Name} x{count}");
        }
    }

    // Swings and damaging casts need a judge: net play sends, the local
    // world strikes right here. The disconnect frame has neither yet.
    private bool HasJudge() => (_net != null && _net.Connected) || _localWorld != null;

    // One cast's client-side checks: learned, level, MP, cooldown. CLI
    // sessions carry no character (no pools), so they cannot cast, like potions.
    private bool TryPaySkill(int id) =>
        _skillCost.Pay(_save?.Slots[_slot], id, SkillLv(id), _fighter.Cooldowns);

    private int SkillLv(int id)
    {
        if (_save?.Slots[_slot] is not CharaSlot chara) return 0;
        return chara.SkillLv.TryGetValue(id, out int lv) ? lv : 0;
    }

    // One fresh edge per skill key (twelve now: a helper, not twelve blocks).
    private static bool FreshKey(KeyboardState kb, Keys key, ref bool was)
    {
        bool down = kb.IsKeyDown(key);
        bool fresh = down && !was;
        was = down;
        return fresh;
    }

    // Menu trips don't track game keys: seed every skill edge from live state.
    private void SeedSkillKeys(KeyboardState kb)
    {
        _qWasDown = kb.IsKeyDown(Keys.Q);
        _wWasDown = kb.IsKeyDown(Keys.W);
        _eWasDown = kb.IsKeyDown(Keys.E);
        _rWasDown = kb.IsKeyDown(Keys.R);
        _tWasDown = kb.IsKeyDown(Keys.T);
        _yWasDown = kb.IsKeyDown(Keys.Y);
        _uWasDown = kb.IsKeyDown(Keys.U);
        _fWasDown = kb.IsKeyDown(Keys.F);
        _gWasDown = kb.IsKeyDown(Keys.G);
        _hWasDown = kb.IsKeyDown(Keys.H);
        _jWasDown = kb.IsKeyDown(Keys.J);
        _lWasDown = kb.IsKeyDown(Keys.L);
    }

    // A paid damaging cast: lock the pose, play the effect, judge (local) or
    // send (net). The cast-time pct folds skill level and combo orbs.
    private void CastActive(int id)
    {
        _fighter.AttackLock = _skills[id].CastLockMs;
        PlaySfx(SwingKey()); // v1 skills whoosh like swings (no per-skill sounds yet)
        SpawnCastFx(id, _mover.X, _mover.Y, _mover.FacingRight);
        int pct = CastPct(id);
        if (_net != null && _net.Connected) _net.SendSkillCast(id, pct);
        else if (_localWorld != null)
        {
            foreach (var h in _localWorld.SkillStrike(_mapId, _mover.X, _mover.Y, _mover.FacingRight, Random.Shared, OwnBuild(), id, pct))
                ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp, h.Skill);
            DrainDrops(_localWorld);
        }
    }

    // Cast-time damage pct: the skill's level curve plus combo orbs (each orb
    // pays Combo Attack's rate plus Combo Synergy's, both at their levels).
    private int CastPct(int id) =>
        _skillDmg.Pct(id, SkillLv(id), _fighter.Orbs, SkillLv(SkillData.ComboAttack), SkillLv(SkillData.ComboSynergy));

    // A paid buff cast: start the timer (recast refreshes), loop its art,
    // relay the effect, push the new build (buff stats feed damage).
    private void CastBuff(int id)
    {
        _fighter.Buffs[id] = _skills[id].BuffMs;
        PlaySfx("buff");
        StripLoopFx(id); // recasts re-hang one loop, never stack two
        SpawnLoopFx(id);
        SpawnCastFx(id, _mover.X, _mover.Y, _mover.FacingRight);
        if (_net != null && _net.Connected) _net.SendSkillCast(id, 0);
        PushBuild();
    }

    // Buff loops are singletons per buff (cast and end both strip first).
    private void StripLoopFx(int id)
    {
        for (int i = _fx.Count - 1; i >= 0; i--)
            if (_fx[i].Loop && _fx[i].Skill == id) _fx.RemoveAt(i);
    }

    // A buff ends (expiry, toggle, session): timer, loop, and orbs all go,
    // and the build drops back (pools clamp next frame, never heal).
    private void EndBuff(int id)
    {
        _fighter.Buffs.Remove(id);
        StripLoopFx(id);
        if (id == SkillData.ComboAttack) _fighter.Orbs = 0;
        PushBuild();
    }

    // Map switches clear one-shot effects but keep buff timers: re-hang loops.
    private void RespawnBuffLoops()
    {
        foreach (int id in _fighter.Buffs.Keys) SpawnLoopFx(id);
    }

    // Buff loop art, riding the player (buffs without art skip silently).
    private void SpawnLoopFx(int id)
    {
        if (_skills[id].Affected.Count == 0) return;
        _fx.Add(new SkillFx { Skill = id, Loop = true, X = _mover.X, Y = _mover.Y - FxCastDy, FacingRight = _mover.FacingRight });
    }

    // Rush/Flash Blade carry: shift along facing (staying inside the map),
    // then re-ground below (over a gap the dash ends airborne and physics
    // takes it from there).
    private void Dash()
    {
        _mover.X = Math.Clamp(_mover.X + (_mover.FacingRight ? DashPx : -DashPx), _map.Info.VrLeft, _map.Info.VrRight);
        _mover.Ground = _mover.Ground == null ? null
            : SceneMath.ArrivalGround(_map.Footholds, (int)_mover.X, (int)_mover.Y + 2);
        if (_mover.Ground != null) _mover.Y = SceneMath.FootholdY(_mover.Ground, (int)_mover.X);
    }

    // Leap Attack's floor check: a platform within range below the feet.
    private bool DiveGround()
    {
        var land = SceneMath.GroundBelow(_map.Footholds, (int)Math.Floor(_mover.X), (int)Math.Ceiling(_mover.Y));
        return land != null && SceneMath.FootholdY(land, (int)_mover.X) - _mover.Y <= LeapRange;
    }

    // Self Recovery pays out on its tick (both pools, capped at buffed max).
    private void PayoutRegen()
    {
        var chara = _save?.Slots[_slot];
        int lv = SkillLv(SkillData.SelfRecovery);
        if (chara == null || lv <= 0) return;
        chara.Hp = Math.Min(chara.Hp + _skills[SkillData.SelfRecovery].RegenHp * lv,
            Progression.BuffedMaxHp(chara.Level, OwnHpPct()));
        chara.Mp = Math.Min(chara.Mp + _skills[SkillData.SelfRecovery].RegenMp * lv,
            Progression.MaxMp(chara.Level));
    }

    // Our own cast or a peer's echo: the effect centers on the torso.
    // Casts without art (Iron Body, Combo Attack) skip silently.
    private void SpawnCastFx(int skill, float x, float y, bool facingRight)
    {
        if (_skills[skill].Effect.Count == 0) return;
        _fx.Add(new SkillFx { Skill = skill, X = x, Y = y - FxCastDy, FacingRight = facingRight });
    }

    // Our combat stats for the server (Build) and the local judge. CLI
    // sessions have no character, so they swing as beginners. Passives and
    // worn gear fold in by level; Spirit Blade multiplies the weapon live.
    private Build OwnBuild()
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return Progression.Beginner;
        var worn = ItemData.EquipBonus(chara.Equips, _items);
        int str = chara.Str + worn.str
            + _skills[SkillData.PhysicalTraining].Str * SkillLv(SkillData.PhysicalTraining)
            + _skills[SkillData.AgileArms].Str * SkillLv(SkillData.AgileArms);
        int dex = chara.Dex + worn.dex
            + _skills[SkillData.PhysicalTraining].Dex * SkillLv(SkillData.PhysicalTraining);
        int atk = (worn.weapon > 0 ? worn.weapon : Progression.WeaponAtk)
            + _skills[SkillData.GuardianArmor].Atk * SkillLv(SkillData.GuardianArmor);
        if (_fighter.Buffs.ContainsKey(SkillData.SpiritBlade))
            atk = atk * (100 + _skills[SkillData.SpiritBlade].AtkPct * SkillLv(SkillData.SpiritBlade)) / 100;
        int dmg = _skills[SkillData.WeaponMastery].DmgPct * SkillLv(SkillData.WeaponMastery);
        int crit = _skills[SkillData.WeaponMastery].CritPct * SkillLv(SkillData.WeaponMastery)
            + _skills[SkillData.ChanceAttack].CritPct * SkillLv(SkillData.ChanceAttack);
        int final = _skills[SkillData.FinalAttack].ProcPct * SkillLv(SkillData.FinalAttack);
        return new Build(str, dex, atk, dmg, crit, final);
    }

    // +MaxHP% from every source: Iron Body while live, Warrior Mastery and
    // Endure always. Pools, potions, and refills all cap at the buffed max.
    private int OwnHpPct() =>
        (_fighter.Buffs.ContainsKey(SkillData.IronBody) ? _skills[SkillData.IronBody].HpPct * SkillLv(SkillData.IronBody) : 0)
        + _skills[SkillData.WarriorMastery].HpPct * SkillLv(SkillData.WarriorMastery)
        + _skills[SkillData.Endure].HpPct * SkillLv(SkillData.Endure);

    // Warrior Mastery speeds run and jump together (one column, both axes).
    private float OwnWalk() =>
        PlayerMover.WalkSpeed * (100 + _skills[SkillData.WarriorMastery].MovePct * SkillLv(SkillData.WarriorMastery)) / 100;
    private float OwnJump() =>
        PlayerMover.JumpV0 * (100 + _skills[SkillData.WarriorMastery].MovePct * SkillLv(SkillData.WarriorMastery)) / 100;

    // Builds change on level-up, learn, and buff edges: push them all.
    private void PushBuild() => _net?.SendBuild(OwnBuild());

    // Our name + plate, so peers draw our tag (CLI sessions have no chara: skip).
    private void PushName()
    {
        var chara = _save?.Slots[_slot];
        if (chara != null) _net?.SendName(chara.Name, PlateFor(chara.Equips));
    }

    // A kill we landed (net or local): claim the EXP. A level-up pushes our
    // new build to the server (the local judge reads it fresh every swing).
    private void GainExp(int exp)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return; // CLI sessions have no one to level
        int ups = Progression.ApplyExp(chara, exp, OwnHpPct());
        if (ups > 0)
        {
            GameLog($"level up! now Lv.{chara.Level} (STR {chara.Str} DEX {chara.Dex})");
            PlaySfx("levelup");
            PushBuild();
        }
    }

    // Local mobs (shots and dropped links): a fresh world once per session,
    // then re-list the view (map switches re-list; kills re-plant on timers).
    private void StartLocalWorld()
    {
        _localWorld = new MobWorld(_assetsDir);
        RefreshLocalMobs();
    }

    private void RefreshLocalMobs()
    {
        _mobs.Clear();
        _drops.Clear();
        _numbers.Clear();
        _fx.Clear();
        RespawnBuffLoops(); // buff timers survive the switch, so their loops re-hang
        _localWorld!.EnterMap(_mapId);
        foreach (var s in _localWorld.MobsOn(_mapId))
            ApplySpawn(s.Uid, s.Mob, s.X, s.Y, s.MaxHp, s.Hp);
        foreach (var d in _localWorld.DropsOn(_mapId))
            ApplyDropSpawn(d.Uid, d.X, d.Y, d.Kind, d.Id, d.Count);
    }

    // The map without any characters: the chars screen reuses this as its backdrop.
    private void DrawMapBack()
    {
        foreach (var item in _map.Backs) { if (!item.Front) DrawBack(item); }

        // layers are drawn 0 first; inside one layer objects come before tiles
        foreach (int layer in _layers)
        {
            foreach (var item in _map.Objs) { if (item.Layer == layer) DrawTexture(item.Image, item.X, item.Y, item.Flip); }
            foreach (var item in _map.Tiles) { if (item.Layer == layer) DrawTexture(item.Image, item.X, item.Y, false); }
        }

        // live portals shimmer over the tiles, behind the characters
        for (int i = 0; i < _map.Portals.Count; i++)
        {
            var portal = _map.Portals[i];
            if (portal.Frames.Count == 0) continue;
            var f = portal.Frames[_portalAnims[i].frame];
            DrawTexture(f.Image, portal.X + f.Dx, portal.Y + f.Dy, false);
        }
    }

    // Front backgrounds cover the scene (and the chars-screen avatars too).
    private void DrawMapFront()
    {
        foreach (var item in _map.Backs) { if (item.Front) DrawBack(item); }
    }

    // Worn-or-beginner parts for one frame: beginner parts minus each worn
    // slot's beginner img, plus the worn gear files, re-sorted back-to-front
    // (same stable order the exporter bakes). Unknown ids draw nothing.
    private List<CharaPart> MergeParts(string action, int frame, List<int> worn)
    {
        var hide = new HashSet<string>();
        var gearParts = new List<CharaPart>();
        foreach (int id in worn)
        {
            if (!_items.TryGetValue(id, out var d) || d.Slot == null) continue; // hand-made saves may wear anything
            var key = (id.ToString("D8"), action);
            if (!_gear.TryGetValue(key, out var gear)) continue; // art-less gear (rings): nothing to draw or hide
            hide.Add(ItemData.BeginnerImgs[d.Slot]); // boot load rejects art slots outside the table
            gearParts.AddRange(gear.Frames[frame].Parts); // boot load guarantees art for art gear
        }
        var parts = new List<CharaPart>();
        foreach (var p in _charas[action].Frames[frame].Parts)
            if (!hide.Contains(p.Img)) parts.Add(p);
        parts.AddRange(gearParts);
        return parts.OrderByDescending(p => p.Z).ToList();
    }

    private void DrawAvatar(int feetX, int feetY, string action, int frame, bool facingRight, List<int>? worn = null)
    {
        var parts = worn == null || worn.Count == 0
            ? _charas[action].Frames[frame].Parts
            : MergeParts(action, frame, worn);
        foreach (var part in parts)
        {
            if (facingRight)
            {
                var tex = _textures[part.Image];
                DrawTexture(part.Image, SceneMath.FlipDrawX(feetX, part.X, tex.Width), feetY + part.Y, true);
            }
            else
            {
                DrawTexture(part.Image, feetX + part.X, feetY + part.Y, false);
            }
        }
    }

    // One background: parallax on the axes that do not scroll, then tile it over the
    // camera rect.
    private void DrawBack(BackItem b)
    {
        var tex = _textures[b.Image];
        var (x, y, cx, cy, left, right, top, bottom) =
            SceneMath.BackLayout(b, tex.Width, tex.Height, _cameraX, _cameraY, GameWidth, GameHeight, _timeMs);

        for (int j = top; j < bottom; j++)
            for (int i = left; i < right; i++)
                DrawTexture(b.Image, x + i * cx, y + j * cy, b.Flip);
    }

    private void DrawTexture(string image, int worldX, int worldY, bool flip)
    {
        _spriteBatch.Draw(_textures[image], new Vector2(worldX - _cameraX, worldY - _cameraY), null, Color.White,
            0f, Vector2.Zero, 1f, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    private void LoadTexture(string image)
    {
        if (_textures.ContainsKey(image)) return;

        string path = Path.Combine(_assetsDir, image);
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing exported png: {Path.GetFullPath(path)}", path);

        using var stream = File.OpenRead(path);
        _textures[image] = Texture2D.FromStream(GraphicsDevice, stream);
    }

    private static T LoadJson<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"missing exported json: {Path.GetFullPath(path)} (run: dotnet run --project MushTown.Exporter -- export)", path);

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"cannot parse json: {path}");
    }

    // A missing/bad file keeps the current layout and logs one line
    // (startup: built-in defaults; hot-reload: old values). A mid-write
    // read fails once; saving again fixes it.
    private void TryReloadLayout()
    {
        try { ReloadLayout(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            GameLog($"layout: {ex.Message}");
        }
    }

    // File wins: reload positions and snap dragged panels back to them.
    private void ReloadLayout()
    {
        Layout.Load(_layoutPath);
        MenuLayout.Load(_menuLayoutPath);
        CharLayout.Load(_charLayoutPath);
        if (_ui != null) _ui.RefreshListFonts(); // null only on the ctor reload, before content loads
        _invPos = new Point(Layout.Inv.X, Layout.Inv.Y);
        _statPos = new Point(Layout.Stat.X, Layout.Stat.Y);
        _skillPos = new Point(Layout.Skill.X, Layout.Skill.Y);
        _questPos = new Point(Layout.Quest.X, Layout.Quest.Y);
        _settingsPos = new Point(Layout.Settings.X, Layout.Settings.Y);
        _shopPos = new Point(Layout.Shop.X, Layout.Shop.Y);
        _dragInv = null; _dragStat = null; _dragSkill = null; _dragQuest = null; _dragSettings = null; _dragShop = null; // a reload mid-drag drops the grab
        if (_contentLoaded) BuildScreenScene(); // ctor reload skips: no textures yet
    }

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
