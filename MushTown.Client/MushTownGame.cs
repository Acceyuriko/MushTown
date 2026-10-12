using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// Renders the exported map + playable character. Weekly acceptance view.
public class MushTownGame : Game
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
    private bool _facingRight, _altWasDown, _upWasDown, _wasInPortal;

    // Net peers, keyed by server id. Empty in solo play and shot mode.
    private GameServer? _server;
    private NetClient? _net;
    private readonly Dictionary<int, RemoteEvent> _remotes = new();

    public MushTownGame(string? shotPath, int shotMap, string shotAction, int shotFrame, bool shotFacingRight, bool host, string? joinHost)
    {
        _shotPath = shotPath;
        _shotMap = shotMap;
        _shotAction = shotAction;
        _shotFrame = shotFrame;
        _shotFacingRight = shotFacingRight;
        _host = host;
        _joinHost = joinHost;
        _assetsDir = FindAssetsDir();
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
        // net play often runs two instances on one PC; keep the unfocused one at full speed
        if (host || joinHost != null) InactiveSleepTime = TimeSpan.Zero;

        // borderless fullscreen at desktop resolution; the game image itself
        // keeps its own size and gets scaled up to fill the screen
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        _graphics.PreferredBackBufferWidth = mode.Width;
        _graphics.PreferredBackBufferHeight = mode.Height;

        Exiting += (_, _) => { _net?.Dispose(); _server?.Dispose(); };
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
        {
            var log = new RollLog(Path.Combine("logs", $"net-{Environment.ProcessId}.log"));
            if (_host)
            {
                _server = new GameServer(log.Write);
                try
                {
                    _server.Start();
                }
                catch (Exception ex)
                {
                    throw new IOException($"cannot host: port {Net.Port} busy? ({ex.Message})", ex);
                }
            }
            _net = new NetClient(log.Write);
            _net.Connect(_host ? "127.0.0.1" : _joinHost!);
        }

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
        _mapId = mapId;
        _map = LoadJson<MapData>(Path.Combine(_assetsDir, "maps", $"{mapId}.json"));
        // inside a map layer objects come before tiles, then by z, then by file order
        // (OrderBy is stable, so the file index stays the tiebreak) (FrmMapRender.cs:1353-1412)
        _map.Objs = _map.Objs.OrderBy(o => o.Layer).ThenBy(o => o.Z).ToList();
        _map.Tiles = _map.Tiles.OrderBy(t => t.Layer).ThenBy(t => t.Z).ToList();
        _layers = _map.Tiles.Select(t => t.Layer).Concat(_map.Objs.Select(o => o.Layer)).Distinct().OrderBy(l => l).ToList();
        _spawnGround = SceneMath.GroundBelow(_map.Footholds, _map.Info.SpawnX, _map.Info.SpawnY)
            ?? throw new InvalidDataException($"map {mapId}: no foothold under spawn ({_map.Info.SpawnX},{_map.Info.SpawnY})");
        _portalAnims = _map.Portals.Select(_ => (0, 0f)).ToList();
        _portalDelays = _map.Portals.Select(p => p.Frames.Select(f => f.Delay).ToList()).ToList();
        for (int i = 0; i < _portalDelays.Count; i++)
            if (_portalDelays[i].Any(x => x <= 0))
                throw new InvalidDataException($"map {mapId} portal {i} has a bad delay");
    }

    // Stand on this map's spawn ground.
    private void Spawn()
    {
        _px = _map.Info.SpawnX;
        _ground = _spawnGround;
        _py = SceneMath.FootholdY(_ground, _map.Info.SpawnX);
        ResetMotion();
    }

    // Stop all motion: still speeds, no ladder, no drop-through.
    private void ResetMotion()
    {
        _vx = 0;
        _vy = 0;
        _climb = null;
        _climbMoving = false;
        _dropFrames = 0;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _scene = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);

        LoadMapTextures();
        foreach (var chara in _charas.Values)
            foreach (var frame in chara.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
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

        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape))
            Exit();
        if (_loadBeat > 0) _loadBeat--;
        if (_portalCooldown > 0) _portalCooldown--;
        if (_grabCooldown > 0) _grabCooldown--;

        _timeMs = gameTime.TotalGameTime.TotalMilliseconds;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float dtMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
        int dir = (kb.IsKeyDown(Keys.Right) ? 1 : 0) - (kb.IsKeyDown(Keys.Left) ? 1 : 0);
        if (dir != 0 && _climb == null) _facingRight = dir > 0; // raw sprites face left; mirror when facing right

        // fresh edges: portal tap, drop and ladder leap fire once per tap
        // (portal also fires on walking in with Up held; grab and jump use held keys)
        bool alt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        bool freshAlt = alt && !_altWasDown;
        _altWasDown = alt;
        bool up = kb.IsKeyDown(Keys.Up);
        bool freshUp = up && !_upWasDown;
        _upWasDown = up;
        bool down = kb.IsKeyDown(Keys.Down);

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
        if (_climb == null && !switched && _grabCooldown == 0 && (up || down)) GrabLadder(up, down);
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
        bool wantJump = alt && !down && _ground != null && _climb == null;
        bool wantDrop = freshAlt && down && _ground != null && _climb == null
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

        string want = _climb != null ? (_climb.Ladder ? "ladder" : "rope")
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
            if (!_net.Connected && _remotes.Count > 0)
                _remotes.Clear(); // link lost: peers vanish, we keep playing solo
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
        else
            DrawScene();
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

        // the player, then each peer: current action frame, mirrored around the
        // feet when facing right (peers by id, so overlap order never flickers)
        int feetX = (int)Math.Floor(_px), feetY = (int)Math.Floor(_py);
        DrawAvatar(feetX, feetY, _action, _frame, _facingRight);
        foreach (var r in _remotes.Values.OrderBy(r => r.Id))
            DrawAvatar((int)Math.Floor(r.X), (int)Math.Floor(r.Y), Net.ActionName(r.Action), r.Frame, r.FacingRight);

        // front backgrounds cover the scene
        foreach (var item in _map.Backs) { if (item.Front) DrawBack(item); }

        _spriteBatch.End();
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
