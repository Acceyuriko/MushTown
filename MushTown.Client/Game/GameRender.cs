using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// Scene rendering: map layers, actors by feet height, NPCs, the avatar.
public partial class MushTownGame
{
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
                DrawName(feetX - _camera.X, feetY + NameDown - _camera.Y, _heroPlate.Name, _heroPlate.Plate);
        }
        ));
        foreach (var r in _remotes.Values.OrderBy(r => r.Id)) // peers show beginner art (only the name plate crosses, via Name)
        {
            int rx = (int)Math.Floor(r.X), ry = (int)Math.Floor(r.Y);
            var plate = r.Owner.Get<PlateArt>()!; // spawn always hangs one
            draws.Add((ry, () =>
            {
                DrawAvatar(rx, ry, Net.ActionName(r.Action), r.Frame, r.FacingRight);
                if (plate.HasName)
                    DrawName(rx - _camera.X, ry + NameDown - _camera.Y, plate.Name, plate.Plate);
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
        foreach (var a in _npcArts)
            draws.Add((a.Spawn.Cy, () => DrawNpc(a.Spawn)));
        foreach ((int _, Action draw) in draws.OrderBy(d => d.y))
            draw();

        DrawMapFront();
        DrawFx();
        DrawNumbers();

        _spriteBatch.End();
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
        int feetX = feet - _camera.X, tagY = y - 20 - _camera.Y;
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
        foreach (var p in _portals)
        {
            if (p.Portal.Frames.Count == 0) continue;
            var f = p.Portal.Frames[p.Frame];
            DrawTexture(f.Image, p.Portal.X + f.Dx, p.Portal.Y + f.Dy, false);
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
            SceneMath.BackLayout(b, tex.Width, tex.Height, _camera.X, _camera.Y, GameWidth, GameHeight, _timeMs);

        for (int j = top; j < bottom; j++)
            for (int i = left; i < right; i++)
                DrawTexture(b.Image, x + i * cx, y + j * cy, b.Flip);
    }

    private void DrawTexture(string image, int worldX, int worldY, bool flip)
    {
        _spriteBatch.Draw(_textures[image], new Vector2(worldX - _camera.X, worldY - _camera.Y), null, Color.White,
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
}
