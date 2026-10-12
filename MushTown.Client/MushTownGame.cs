using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    private const float AirAccel = 100f;  // weak air drift: a full jump shifts vx by ~60

    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetsDir;
    private readonly string? _shotPath;
    private readonly string _shotAction;
    private readonly int _shotFrame;
    private readonly bool _shotFacingRight;

    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _scene = null!;

    private MapData _map = null!;
    private readonly Dictionary<string, CharaData> _charas = new();
    private readonly Dictionary<string, List<int>> _delays = new();
    private List<int> _layers = null!;
    private readonly Dictionary<string, Texture2D> _textures = new();
    private int _cameraX, _cameraY;
    private float _camX, _camY, _camVX, _camVY;

    // Player: feet position, horizontal/vertical speed, and the foothold stood on
    // (null means airborne, so there is no separate on-ground flag).
    private float _px, _py, _vx, _vy;
    private FootholdItem? _ground;
    private FootholdItem _spawnGround = null!;
    private string _action = "stand2";
    private int _frame;
    private float _timerMs;
    private bool _facingRight, _jumpWasDown;

    public MushTownGame(string? shotPath, string shotAction, int shotFrame, bool shotFacingRight)
    {
        _shotPath = shotPath;
        _shotAction = shotAction;
        _shotFrame = shotFrame;
        _shotFacingRight = shotFacingRight;
        _assetsDir = FindAssetsDir();
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;

        // borderless fullscreen at desktop resolution; the game image itself
        // keeps its own size and gets scaled up to fill the screen
        var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        _graphics.PreferredBackBufferWidth = mode.Width;
        _graphics.PreferredBackBufferHeight = mode.Height;
    }

    protected override void Initialize()
    {
        Window.ClientSizeChanged += OnClientSizeChanged;

        _map = LoadJson<MapData>(Path.Combine(_assetsDir, "maps", "100000000.json"));
        foreach (string action in new[] { "stand2", "walk2", "jump" })
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

        // inside a map layer objects come before tiles, then by z, then by file order
        // (OrderBy is stable, so the file index stays the tiebreak) (FrmMapRender.cs:1353-1412)
        _map.Objs = _map.Objs.OrderBy(o => o.Layer).ThenBy(o => o.Z).ToList();
        _map.Tiles = _map.Tiles.OrderBy(t => t.Layer).ThenBy(t => t.Z).ToList();
        _layers = _map.Tiles.Select(t => t.Layer).Concat(_map.Objs.Select(o => o.Layer)).Distinct().OrderBy(l => l).ToList();

        // player starts on the ground under the spawn point
        _px = _map.Info.SpawnX;
        _spawnGround = SceneMath.GroundBelow(_map.Footholds, _map.Info.SpawnX, _map.Info.SpawnY)
            ?? throw new InvalidDataException($"no foothold under spawn ({_map.Info.SpawnX},{_map.Info.SpawnY})");
        _ground = _spawnGround;
        _py = SceneMath.FootholdY(_ground, _map.Info.SpawnX);

        // camera starts snapped on target (no opening swoop); Update eases it after moves
        SnapCamera();

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

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _scene = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);

        foreach (var item in _map.Backs) LoadTexture(item.Image);
        foreach (var item in _map.Tiles) LoadTexture(item.Image);
        foreach (var item in _map.Objs) LoadTexture(item.Image);
        foreach (var chara in _charas.Values)
            foreach (var frame in chara.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
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

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float dtMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
        int dir = (kb.IsKeyDown(Keys.Right) ? 1 : 0) - (kb.IsKeyDown(Keys.Left) ? 1 : 0);
        if (dir != 0) _facingRight = dir > 0; // raw sprites face left; mirror when facing right

        // jump on a fresh Alt press (read before walking can leave the ground)
        bool alt = kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        bool wantJump = alt && !_jumpWasDown && _ground != null;
        _jumpWasDown = alt;

        // horizontal: full speed on ground, keep momentum plus weak drift in the air
        if (_ground != null)
            _vx = dir * WalkSpeed;
        else
            _vx = Math.Clamp(_vx + dir * AirAccel * dt, -WalkSpeed, WalkSpeed);
        float rawX = _px + _vx * dt;
        float nx = Math.Clamp(rawX, _map.Info.VrLeft, _map.Info.VrRight);
        if (nx != rawX) _vx = 0; // hit a wall: momentum dies here
        bool moved = nx != _px; // false when still or already at the wall: feet stay, so stand, not walk
        _px = nx;
        if (_ground != null)
        {
            int gx = (int)Math.Floor(nx);
            if (gx < Math.Min(_ground.X1, _ground.X2) || gx > Math.Max(_ground.X1, _ground.X2))
            {
                var step = SceneMath.StepTo(_map.Footholds, gx, (int)Math.Floor(_py));
                if (step == null)
                    _ground = null; // walked off the edge: fall from here
                else
                {
                    _ground = step;
                    _py = SceneMath.FootholdY(step, gx);
                }
            }
            else
            {
                _py = SceneMath.FootholdY(_ground, gx);
            }
        }

        if (wantJump)
        {
            _vy = JumpV0;
            _ground = null;
        }

        // vertical: one-way platforms (pass through going up, land coming down)
        if (_ground == null)
        {
            _vy += Gravity * dt;
            float ny = _py + _vy * dt;
            if (_vy > 0)
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
            if (_ground == null && _py >= _map.Info.VrBottom) // fell out of the world: respawn
            {
                Console.WriteLine($"fell out of the world at ({_px:F0},{_py:F0}), respawned at spawn");
                _px = _map.Info.SpawnX;
                _py = SceneMath.FootholdY(_spawnGround, _map.Info.SpawnX);
                _vy = 0;
                _ground = _spawnGround;
                SnapCamera(); // teleport: snap, don't swoosh across the map
            }
        }

        string want = _ground == null ? "jump" : dir != 0 && moved ? "walk2" : "stand2";
        if (want != _action)
        {
            _action = want;
            _frame = 0;
            _timerMs = 0;
        }
        (_frame, _timerMs) = SceneMath.AdvanceFrame(_frame, _timerMs, dtMs, _delays[_action]);

        var (targetX, targetY) = CameraTarget();
        (_camX, _camVX) = SceneMath.SpringStep(_camX, _camVX, targetX, dt);
        (_camY, _camVY) = SceneMath.SpringStep(_camY, _camVY, targetY, dt);
        _cameraX = (int)Math.Round(_camX);
        _cameraY = (int)Math.Round(_camY);

        base.Update(gameTime);
    }

    // character around 70% down the screen, clamped to map bounds
    private (int x, int y) CameraTarget() => (
        SceneMath.ClampCamera((int)Math.Floor(_px) - GameWidth / 2, _map.Info.VrLeft, _map.Info.VrRight, GameWidth),
        SceneMath.ClampCamera((int)Math.Floor(_py) - (int)(GameHeight * 0.7), _map.Info.VrTop, _map.Info.VrBottom, GameHeight));

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

        // the player: current action frame, mirrored around the feet when facing right
        var frame = _charas[_action].Frames[_frame];
        int feetX = (int)Math.Floor(_px), feetY = (int)Math.Floor(_py);
        foreach (var part in frame.Parts)
        {
            if (_facingRight)
            {
                var tex = _textures[part.Image];
                DrawTexture(part.Image, SceneMath.FlipDrawX(feetX, part.X, tex.Width), feetY + part.Y, true);
            }
            else
            {
                DrawTexture(part.Image, feetX + part.X, feetY + part.Y, false);
            }
        }

        // front backgrounds cover the scene
        foreach (var item in _map.Backs) { if (item.Front) DrawBack(item); }

        _spriteBatch.End();
    }

    // One background: parallax on the axes that do not scroll, then tile it over the
    // camera rect.
    private void DrawBack(BackItem b)
    {
        var tex = _textures[b.Image];
        var (x, y, cx, cy, left, right, top, bottom) =
            SceneMath.BackLayout(b, tex.Width, tex.Height, _cameraX, _cameraY, GameWidth, GameHeight);

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
