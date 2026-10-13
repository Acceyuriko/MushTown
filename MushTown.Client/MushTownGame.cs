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
    private const int GameWidth = 1366;
    private const int GameHeight = 768;

    // Walk/jump feel, tuned by hand (approximate MapleStory values).
    private const float WalkSpeed = 140f; // px per second
    private const float JumpV0 = -520f;   // take-off velocity, px per second
    private const float Gravity = 1800f;  // pull-down, px per second squared
    private const float MaxFall = 700f;   // terminal velocity: long drops stop speeding up here
    private const float AirAccel = 100f;  // weak air drift: a full jump shifts vx by ~60
    private const float ClimbSpeed = 120f; // ladder/rope slide, px per second
    private const int DropFrames = 3; // down+jump ignores landings for ~3 frames: just enough to clear the dropped line
    private const int LoadBeat = 10;      // black frames shown on map switch (~170ms)
    private const int PortalCooldown = 30; // frames after travel during which ↑ cannot travel again (~0.5s)
    private const int GrabCooldown = 20; // frames after leaving a ladder during which Up/Down cannot re-grab (~0.33s)
    private const float NumberMs = 800; // damage numbers rise and fade over this long
    private const int NumberRise = 60; // px a damage number floats up
    private const int BarWidth = 40; // mob HP bar width, px

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
    private readonly bool _shotHit;

    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _scene = null!;

    private MapData _map = null!;
    private int _mapId;
    private readonly Dictionary<string, CharaData> _charas = new();
    private readonly Dictionary<string, List<int>> _delays = new();
    private List<int> _layers = null!;
    private readonly Dictionary<string, Texture2D> _textures = new();
    private int _cameraX, _cameraY;
    private float _camX, _camY, _camVX, _camVY;
    private double _timeMs; // scrolling backs drift with this (0 in shot mode: still image)
    private int _loadBeat;
    private int _portalCooldown; // travel cooldown, ticks down alongside the beat
    private int _grabCooldown; // ladder re-grab block after jumping off or mounting the top
    private List<(int frame, float timer)> _portalAnims = new(); // one anim state per portal, aligned with _map.Portals
    private List<List<int>> _portalDelays = new();

    // Player: feet position, horizontal/vertical speed, and the foothold stood on
    // (null means airborne, so there is no separate on-ground flag).
    private float _px, _py, _vx, _vy;
    private FootholdItem? _ground;
    private LadderItem? _climb; // set while on a ladder or rope
    private bool _climbMoving;  // climb pose only animates while sliding
    private int _dropFrames;    // down+jump: skip landings until this hits 0
    private FootholdItem _spawnGround = null!;
    private string _action = "stand2";
    private int _frame;
    private float _timerMs;
    private bool _facingRight, _altWasDown, _upWasDown, _wasInPortal, _ctrlWasDown;
    private float _attackLock; // ms of swing left: movement frozen until it hits 0

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
    private DmgData? _dmg; // basic damage skin digits, loaded once

    private sealed class MobView
    {
        public int Mob;
        public float X;
        public float Y;
        public int Hp;
        public int MaxHp;
        public string Action = ""; // idle, hit1, die1
        public int Frame;
        public float Timer;
    }

    private sealed class DamageNumber
    {
        public string Text = "";
        public float X; // head position when the hit landed
        public float Y;
        public float Age;
    }

    private readonly string _layoutPath;
#if DEBUG
    private FileSystemWatcher? _layoutWatcher;
    private volatile bool _layoutDirty; // set by the watcher thread, consumed on the game thread
#endif

    public MushTownGame(string? shotPath, int shotMap, string shotAction, int shotFrame, bool shotFacingRight, bool host, string? joinHost, string? shotScreen = null, bool shotHit = false)
    {
        _shotPath = shotPath;
        _shotMap = shotMap;
        _shotAction = shotAction;
        _shotFrame = shotFrame;
        _shotFacingRight = shotFacingRight;
        _host = host;
        _joinHost = joinHost;
        _shotScreen = shotScreen;
        _shotHit = shotHit;
        _assetsDir = FindAssetsDir();
        string dir = Path.GetDirectoryName(_assetsDir)!;
        _layoutPath = Path.Combine(dir, "hud-layout.json");
        TryReloadLayout();
#if DEBUG
        _layoutWatcher = new FileSystemWatcher(dir, Path.GetFileName(_layoutPath));
        _layoutWatcher.Changed += (_, _) => _layoutDirty = true;
        _layoutWatcher.Renamed += (_, _) => _layoutDirty = true; // some editors save by rename
        _layoutWatcher.Created += (_, _) => _layoutDirty = true; // ...or delete-then-create
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

        Exiting += (_, _) => { SaveChara(); _net?.Dispose(); _server?.Dispose(); };
    }

    protected override void Initialize()
    {
        Window.ClientSizeChanged += OnClientSizeChanged;

        _mapId = _shotPath != null ? _shotMap : SpawnMapId;
        LoadMapData(_mapId);
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
            _action = _shotAction;
            _frame = _shotFrame;
            _facingRight = _shotFacingRight;
        }

        InitializeMenu(_shotScreen);
        base.Initialize();
    }

    // Screen size changed (resolution switch, moving to another monitor):
    // follow the new size so the upscaling stays correct.
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
        // all checks passed: commit everything at once, so a bad map leaves
        // the old map untouched and the menu can retry cleanly
        _mapId = mapId;
        _map = map;
        _layers = layers;
        _spawnGround = spawnGround;
        _portalAnims = portalAnims;
        _portalDelays = portalDelays;
    }

    // Stand on this map's spawn ground.
    private void Spawn()
    {
        _px = _map.Info.SpawnX;
        _ground = _spawnGround;
        _py = SceneMath.FootholdY(_ground, _map.Info.SpawnX);
        ResetMotion();
    }

    // Stop all motion: still speeds, no ladder, no drop-through, no swing.
    private void ResetMotion()
    {
        _vx = 0;
        _vy = 0;
        _climb = null;
        _climbMoving = false;
        _dropFrames = 0;
        _attackLock = 0;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _scene = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);

        LoadMapTextures();
        foreach (var chara in _charas.Values)
            foreach (var frame in chara.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
        LoadMenuContent();
        if (_shotPath != null && _shotScreen is null or "inv" or "use" or "stat" or "hud") // game shots show mobs; menu shots don't
            SetupShotMobs();
    }

    // Shot mode: a frozen local world. --hit plants the player mid-swing next
    // to the first mob and lands one real strike (numbers and all).
    private void SetupShotMobs()
    {
        StartLocalWorld();
        if (!_shotHit) return;
        var world = _localWorld!; // StartLocalWorld just built it
        var first = world.MobsOn(_mapId).FirstOrDefault()
            ?? throw new InvalidDataException($"--hit needs mobs, map {_mapId} has none");
        _px = first.X - 50;
        _ground = SceneMath.ArrivalGround(_map.Footholds, (int)_px, (int)first.Y)
            ?? throw new InvalidDataException($"--hit found no ground near mob {first.Mob}");
        _py = SceneMath.FootholdY(_ground, (int)_px);
        ResetMotion();
        _facingRight = true;
        _action = "swingP1";
        _frame = 1;
        // fixed seed: the evidence shot shows the same number every run
        foreach (var h in world.Strike(_mapId, _px, _py, true, new Random(0), Progression.Beginner))
            ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp);
        SnapCamera();
    }

    private void LoadMapTextures()
    {
        // textures stay cached across maps (6 maps, bounded); nothing unloads
        foreach (var item in _map.Backs) LoadTexture(item.Image);
        foreach (var item in _map.Tiles) LoadTexture(item.Image);
        foreach (var item in _map.Objs) LoadTexture(item.Image);
        foreach (var item in _map.Portals) foreach (var f in item.Frames) LoadTexture(f.Image);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_shotPath != null)
        {
            base.Update(gameTime);
            return;
        }
#if DEBUG
        if (_layoutDirty)
        {
            _layoutDirty = false;
            TryReloadLayout();
        }
#endif
        if (_screen != Screen.Game)
        {
            UpdateMenu();
            base.Update(gameTime);
            return;
        }

        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape))
        {
            if (_account != null) { ToChars(); base.Update(gameTime); return; } // menu session: Esc saves and steps out
            else Exit(); // CLI session has no menu to return to
        }
        var ms = Mouse.GetState(); // one click edge for the log button and the HUD
        bool click = ClickEdge(ms);
        var (mx, my) = MouseGamePos(ms);
        if (!UpdateLogButton(click, mx, my))
            UpdateHud(kb, click, ms.LeftButton == ButtonState.Pressed, mx, my);
        if (_loadBeat > 0) _loadBeat--;
        if (_portalCooldown > 0) _portalCooldown--;
        if (_grabCooldown > 0) _grabCooldown--;

        _timeMs = gameTime.TotalGameTime.TotalMilliseconds;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float dtMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
        int dir = (kb.IsKeyDown(Keys.Right) ? 1 : 0) - (kb.IsKeyDown(Keys.Left) ? 1 : 0);
        if (_attackLock > 0)
        {
            _attackLock = Math.Max(0, _attackLock - dtMs);
            dir = 0; // mid-swing: feet planted, facing kept
        }
        else if (dir != 0 && _climb == null) _facingRight = dir > 0; // raw sprites face left; mirror when facing right

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

        // Ctrl swings the polearm (not on a ladder): the swing locks movement
        // for one playthrough; net play sends it, the local world judges it right here.
        // No swing without a judge (the disconnect frame has neither yet).
        if (freshCtrl && _climb == null && _attackLock <= 0 &&
            ((_net != null && _net.Connected) || _localWorld != null))
        {
            _attackLock = _delays["swingP1"].Sum();
            if (_net != null && _net.Connected) _net.SendAttack();
            else if (_localWorld != null)
                foreach (var h in _localWorld.Strike(_mapId, _px, _py, _facingRight, Random.Shared, OwnBuild()))
                    ApplyHit(h.Uid, h.Damage, h.HpLeft, true, h.Exp);
        }

        // Up at a portal travels: tap anywhere in range, or hold Up and walk
        // in (portal wins over ladder). Travel cools down for ~0.5s, and
        // standing in range with Up held needs a fresh tap, so arrivals never
        // ping-pong back.
        var portal = SceneMath.FindPortal(_map.Portals, _px, _py);
        bool wantPortal = freshUp || (up && !_wasInPortal);
        bool switched = false;
        if (_climb == null && portal != null && wantPortal && _portalCooldown == 0)
        {
            SwitchMap(portal.Tm, portal.Tn);
            switched = true;
        }
        if (_climb == null && !switched && _grabCooldown == 0 && (up || down) && _attackLock <= 0) GrabLadder(up, down);
        _wasInPortal = switched
            ? SceneMath.FindPortal(_map.Portals, _px, _py) != null // arrival spot, not the old one
            : portal != null;

        // Alt+side leaps off faster than a run-up with half a jump of up
        // (rope and ladder feel the same); Alt alone holds on
        if (_climb != null && freshAlt && dir != 0)
        {
            _climb = null;
            _climbMoving = false;
            _vy = JumpV0 / 2;
            _vx = dir * WalkSpeed * 1.5f;
            _facingRight = dir > 0;
            _ground = null;
            _grabCooldown = GrabCooldown;
        }
        if (_climb != null) ClimbMove(up, down, dt);

        // held Alt jumps (landings chain into the next jump); down+jump instead
        // drops through the platform, one floor per tap, and only over a floor
        bool wantJump = alt && !down && _ground != null && _climb == null && _attackLock <= 0;
        bool wantDrop = freshAlt && down && _ground != null && _climb == null && _attackLock <= 0
            && SceneMath.GroundBelow(_map.Footholds, (int)Math.Floor(_px), (int)Math.Ceiling(_py) + 1) != null;

        // horizontal: full speed on ground, keep momentum plus weak drift in the air
        bool moved = false; // false when still or already at the wall: feet stay, so stand, not walk
        float sx = _px, nx = _px; // stride start and end (the ground follow walks between them)
        if (_climb == null)
        {
            if (_ground != null)
                _vx = down ? 0 : dir * WalkSpeed; // held Down pins the feet: prone
            else
                _vx = Math.Clamp(_vx + dir * AirAccel * dt, -WalkSpeed, WalkSpeed);
            float rawX = _px + _vx * dt;
            nx = Math.Clamp(rawX, _map.Info.VrLeft, _map.Info.VrRight);
            if (nx != rawX) _vx = 0; // hit a wall: momentum dies here
            moved = nx != _px;
            _px = nx;
        }
        if (_ground != null && _climb == null)
        {
            // follow the line pixel by pixel: one 2.3px stride would skip the
            // joint pixel on slopes and read a 2px gap in a continuous line
            int x0 = (int)Math.Floor(sx), x1 = (int)Math.Floor(nx);
            int stepX = Math.Sign(x1 - x0);
            int gx = x0;
            while (_ground != null)
            {
                if (gx < Math.Min(_ground.X1, _ground.X2) || gx > Math.Max(_ground.X1, _ground.X2))
                {
                    var step = SceneMath.StepTo(_map.Footholds, gx, (int)Math.Floor(_py));
                    if (step == null)
                    {
                        _ground = null; // walked off the edge: fall from here
                        break;
                    }
                    _ground = step;
                }
                _py = SceneMath.FootholdY(_ground, gx);
                if (gx == x1) break;
                gx += stepX;
            }
        }

        if (wantJump)
        {
            _vy = JumpV0;
            _ground = null;
        }
        if (wantDrop)
        {
            _ground = null; // fall through; the landing check skips for a few frames
            _dropFrames = DropFrames;
        }

        // vertical: one-way platforms (pass through going up, land coming down)
        if (_ground == null && _climb == null)
        {
            _vy += Gravity * dt;
            _vy = Math.Min(_vy, MaxFall);
            float ny = _py + _vy * dt;
            if (_vy > 0 && _dropFrames == 0)
            {
                int ix = (int)Math.Floor(_px), iy = (int)Math.Ceiling(_py); // ceiling: never snap up through a line
                var g = SceneMath.GroundBelow(_map.Footholds, ix, iy);
                if (g != null && ny >= SceneMath.FootholdY(g, ix))
                {
                    _py = SceneMath.FootholdY(g, ix);
                    _vy = 0;
                    _ground = g;
                }
                else
                {
                    _py = ny;
                }
            }
            else
            {
                _py = ny;
            }
            if (_dropFrames > 0) _dropFrames--;
            if (_ground == null && _py >= _map.Info.VrBottom) // fell out of the world: respawn
            {
                Console.WriteLine($"fell out of the world at ({_px:F0},{_py:F0}), respawned at spawn");
                Spawn();
                SnapCamera(); // teleport: snap, don't swoosh across the map
            }
        }

        string want = _attackLock > 0 ? "swingP1"
            : _climb != null ? (_climb.Ladder ? "ladder" : "rope")
            : _ground == null ? "jump" : down ? "prone" : dir != 0 && moved ? "walk2" : "stand2";
        if (want != _action)
        {
            _action = want;
            _frame = 0;
            _timerMs = 0;
        }
        if (_climb == null || _climbMoving) // still on a ladder: hold the pose
            (_frame, _timerMs) = SceneMath.AdvanceFrame(_frame, _timerMs, dtMs, _delays[_action]);
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
                _net.SendState(_mapId, _px, _py, Net.ActionCode(_action), (byte)_frame, _facingRight);
            while (_net.TryTake(out var e))
                if (e.Gone)
                    _remotes.Remove(e.Id);
                else if (e.Map != _mapId)
                    _remotes.Remove(e.Id); // peer on another map: not shown here
                else if (e.Frame < _charas[Net.ActionName(e.Action)].Frames.Count)
                    _remotes[e.Id] = e; // bad frame: not ours, drop it
            while (_net.TryTakeMob(out var m))
            {
                if (m.Map != _mapId) continue; // another map's fight: not shown here
                if (m.Spawn) ApplySpawn(m.Uid, m.Mob, m.X, m.Y, m.MaxHp, m.HpLeft);
                else ApplyHit(m.Uid, m.Damage, m.HpLeft, m.Attacker == _net.PlayerId, m.Exp);
            }
            if (!_net.Connected && _remotes.Count > 0)
                _remotes.Clear(); // link lost: peers vanish, the local world takes over
            if (!_net.Connected && _localWorld == null)
                StartLocalWorld(); // link lost: a fresh local world keeps the mobs working (kills come back: the drop resets the session)
        }

        base.Update(gameTime);
    }

    // character around 70% down the screen, clamped to map bounds
    private (int x, int y) CameraTarget() => (
        SceneMath.ClampCamera((int)Math.Floor(_px) - GameWidth / 2, _map.Info.VrLeft, _map.Info.VrRight, GameWidth),
        SceneMath.ClampCamera((int)Math.Floor(_py) - (int)(GameHeight * 0.7), _map.Info.VrTop, _map.Info.VrBottom, GameHeight));

    // Held Up/Down near a ladder or rope grabs on, centered on its x.
    // Down-only at or below the foot does nothing (else it grabs and lets
    // go in the same frame: a visible hop).
    private void GrabLadder(bool up, bool down)
    {
        var l = SceneMath.FindLadder(_map.Ladders, _px, _py);
        if (l == null) return;
        if (!up && down && _py >= l.Y2) return;
        ResetMotion();
        _climb = l;
        _px = l.X;
        _ground = null;
    }

    // Slide along the grabbed ladder or rope. Up at the top steps onto the
    // platform above when one is there; Down at the bottom lets go.
    private void ClimbMove(bool up, bool down, float dt)
    {
        var l = _climb!;
        int dirY = (down ? 1 : 0) - (up ? 1 : 0);
        float ny = Math.Clamp(_py + dirY * ClimbSpeed * dt, l.Y1, l.Y2);
        _climbMoving = ny != _py;
        if (up && ny <= l.Y1)
        {
            var g = SceneMath.MountTop(_map.Footholds, l.X, l.Y1);
            if (g != null)
            {
                _ground = g;
                _py = SceneMath.FootholdY(g, l.X);
                _climb = null;
                _climbMoving = false;
                _grabCooldown = GrabCooldown; // just mounted: walk off before Up can re-grab
                return;
            }
        }
        if (down && !up && ny >= l.Y2)
        {
            _climb = null;
            _climbMoving = false; // let go, gravity takes over
        }
        _py = ny;
    }

    // Land on the ground under the target portal (same map or another one).
    // The exporter already proved the portal and its ground exist, so a miss
    // here means hand-edited assets.
    private void SwitchMap(int tm, string tn)
    {
        if (tm != _mapId)
        {
            LoadMapData(tm);
            LoadMapTextures();
            _remotes.Clear(); // peers stay on the old map; their live states refill this one
            if (_localWorld != null) RefreshLocalMobs(); // the local world re-lists the new map
            else { _mobs.Clear(); _numbers.Clear(); } // net: the server re-pushes them
            _loadBeat = LoadBeat; // real map change: black beat (same-map jumps stay instant)
        }
        var land = _map.Portals.FirstOrDefault(p => p.Pn == tn)
            ?? throw new InvalidDataException($"map {tm} has no portal '{tn}'");
        _px = land.X;
        _ground = SceneMath.ArrivalGround(_map.Footholds, land.X, land.Y)
            ?? throw new InvalidDataException($"map {tm} portal '{tn}' has no ground below");
        _py = SceneMath.FootholdY(_ground, land.X);
        ResetMotion();
        SnapCamera();
        _portalCooldown = PortalCooldown;
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
                DrawLogOverlay(gx, gy);
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
        int feetX = (int)Math.Floor(_px), feetY = (int)Math.Floor(_py);
        var draws = new List<(int y, Action draw)>();
        draws.Add((feetY, () => DrawAvatar(feetX, feetY, _action, _frame, _facingRight)));
        foreach (var r in _remotes.Values.OrderBy(r => r.Id))
            draws.Add(((int)Math.Floor(r.Y), () => DrawAvatar((int)Math.Floor(r.X), (int)Math.Floor(r.Y), Net.ActionName(r.Action), r.Frame, r.FacingRight)));
        foreach (int uid in _mobs.Keys.OrderBy(u => u))
        {
            var m = _mobs[uid];
            draws.Add(((int)Math.Floor(m.Y), () => DrawMob(m)));
        }
        foreach ((int _, Action draw) in draws.OrderBy(d => d.y))
            draw();

        DrawMapFront();
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
        DrawTexture(frame.Image, x, y, false); // mobs face left (raw); W12 AI flips them
        if (m.Hp >= m.MaxHp || m.Hp <= 0) return; // fresh and dying mobs hide their bar
        int px = (int)Math.Floor(m.X) - BarWidth / 2 - _cameraX, py = y - 8 - _cameraY;
        _spriteBatch.Draw(_pixel, new Rectangle(px, py, BarWidth, 5), Color.Black);
        _spriteBatch.Draw(_pixel, new Rectangle(px + 1, py + 1, Math.Max(1, (int)((BarWidth - 2) * (float)m.Hp / m.MaxHp)), 3), new Color(80, 220, 80));
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
    private void ApplyHit(int uid, int damage, int hpLeft, bool mine, int exp)
    {
        if (!_mobs.TryGetValue(uid, out var m)) return;
        m.Hp = hpLeft;
        m.Action = hpLeft == 0 ? "die1" : "hit1";
        m.Frame = 0;
        m.Timer = 0;
        float headY = m.Y - _textures[MobFrames(m.Mob, m.Action)[0].Image].Height;
        _numbers.Add(new DamageNumber { Text = damage.ToString(), X = m.X, Y = headY });
        if (hpLeft == 0 && mine) GainExp(exp);
    }

    // Our combat stats for the server (Build) and the local judge. CLI
    // sessions have no character, so they swing as beginners.
    private Build OwnBuild()
    {
        var chara = _account?.Slots[_slot];
        return chara == null ? Progression.Beginner : new Build(chara.Str, chara.Dex, Progression.WeaponAtk);
    }

    // A kill we landed (net or local): claim the EXP. A level-up pushes our
    // new build to the server (the local judge reads it fresh every swing).
    private void GainExp(int exp)
    {
        var chara = _account?.Slots[_slot];
        if (chara == null) return; // CLI sessions have no one to level
        int ups = Progression.ApplyExp(chara, exp);
        if (ups > 0)
        {
            GameLog($"level up! now Lv.{chara.Level} (STR {chara.Str} DEX {chara.Dex})");
            _net?.SendBuild(chara.Str, chara.Dex, Progression.WeaponAtk);
        }
    }

    // Local mobs (shots and dropped links): a fresh world once per session,
    // then re-list the view (map switches re-list; kills stay dead until
    // the session ends).
    private void StartLocalWorld()
    {
        _localWorld = new MobWorld(_assetsDir);
        RefreshLocalMobs();
    }

    private void RefreshLocalMobs()
    {
        _mobs.Clear();
        _numbers.Clear();
        _localWorld!.EnterMap(_mapId);
        foreach (var s in _localWorld.MobsOn(_mapId))
            ApplySpawn(s.Uid, s.Mob, s.X, s.Y, s.MaxHp, s.Hp);
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

    private void DrawAvatar(int feetX, int feetY, string action, int frame, bool facingRight)
    {
        foreach (var part in _charas[action].Frames[frame].Parts)
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
        _invPos = new Point(Layout.Inv.X, Layout.Inv.Y);
        _statPos = new Point(Layout.Stat.X, Layout.Stat.Y);
        _dragInv = null; _dragStat = null; // a reload mid-drag drops the grab
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
