using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// Renders the exported map + standing character. Weekly acceptance view.
public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private MapData? _map;
    private CharaData? _chara;
    private readonly Dictionary<string, Texture2D> _textures = new();
    private int _cameraX, _cameraY;

    // Command line: --assets <dir> --shot <file.png>
    public static string? AssetsDir;
    public static string? ShotPath;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.ApplyChanges();

        string assets = AssetsDir ?? FindAssetsDir();
        _map = LoadJson<MapData>(Path.Combine(assets, "maps", "100000000.json"));
        _chara = LoadJson<CharaData>(Path.Combine(assets, "chara", "body", "stand2.json"));

        // camera: character around 70% down the screen, clamped to map bounds
        int w = _graphics.PreferredBackBufferWidth, h = _graphics.PreferredBackBufferHeight;
        if (_map != null)
        {
            _cameraX = Math.Clamp(_map.Info.SpawnX - w / 2, _map.Info.VrLeft, Math.Max(_map.Info.VrLeft, _map.Info.VrRight - w));
            _cameraY = Math.Clamp(_map.Info.SpawnY - (int)(h * 0.7), _map.Info.VrTop, Math.Max(_map.Info.VrTop, _map.Info.VrBottom - h));
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        if (_map != null)
        {
            foreach (var item in _map.Backs) LoadTexture(item.Image);
            foreach (var item in _map.Tiles) LoadTexture(item.Image);
            foreach (var item in _map.Objs) LoadTexture(item.Image);
        }
        if (_chara != null)
            foreach (var frame in _chara.Frames)
                foreach (var part in frame.Parts) LoadTexture(part.Image);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(120, 170, 210));

        _spriteBatch.Begin();

        if (_map != null)
        {
            foreach (var item in _map.Backs) { if (!item.Front) DrawTexture(item.Image, item.X, item.Y); }
            foreach (var item in _map.Tiles) DrawTexture(item.Image, item.X, item.Y);
            foreach (var item in _map.Objs) DrawTexture(item.Image, item.X, item.Y);
        }

        // standing still on the spawn ground (frame 0 only until W3 animation)
        if (_chara?.Frames.Count > 0)
        {
            var spawnX = _map?.Info.SpawnX ?? 0;
            var spawnY = _map?.Info.SpawnY ?? 0;
            foreach (var part in _chara.Frames[0].Parts)
                DrawTexture(part.Image, spawnX + part.X, spawnY + part.Y);
        }

        // front backgrounds cover the scene
        if (_map != null)
            foreach (var item in _map.Backs) { if (item.Front) DrawTexture(item.Image, item.X, item.Y); }

        _spriteBatch.End();

        base.Draw(gameTime);

        if (ShotPath != null)
        {
            SaveShot(ShotPath);
            Exit();
        }
    }

    private void DrawTexture(string image, int worldX, int worldY)
    {
        if (!_textures.TryGetValue(image, out var tex)) return;
        _spriteBatch.Draw(tex, new Vector2(worldX - _cameraX, worldY - _cameraY), Color.White);
    }

    private void LoadTexture(string image)
    {
        if (_textures.ContainsKey(image)) return;

        string assets = AssetsDir ?? FindAssetsDir();
        string path = Path.Combine(assets, image);
        if (!File.Exists(path)) return;

        using var stream = File.OpenRead(path);
        _textures[image] = Texture2D.FromStream(GraphicsDevice, stream);
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
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
        return "assets";
    }

    private void SaveShot(string path)
    {
        int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int h = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var colors = new Color[w * h];
        GraphicsDevice.GetBackBufferData(colors);

        using var tex = new Texture2D(GraphicsDevice, w, h);
        tex.SetData(colors);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var fs = File.Create(path);
        tex.SaveAsPng(fs, w, h);
    }
}
