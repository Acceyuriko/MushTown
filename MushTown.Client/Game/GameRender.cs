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
            draws.Add((a.Spawn.Cy, () => DrawNpc(a)));
        foreach ((int _, Action draw) in draws.OrderBy(d => d.y))
            draw();

        DrawMapFront();
        DrawFx();
        DrawNumbers();

        _spriteBatch.End();
    }

    // One NPC: its stand frame on its origin-anchored feet, name tag under the
    // feet. The quest balloon and the speech bubble are components (NpcQuestMark,
    // NpcBalloon), not drawn here.
    private void DrawNpc(NpcArt a)
    {
        var n = a.Spawn;
        var top = NpcTopLeft(n);
        // The frame comes from the same component the world map tip uses.
        string frame = a.Owner.Get<LifeStand>()!.ImageAt((float)_timeMs);
        DrawTexture(frame, top.X, top.Y, n.FacingRight);
        var tag = _ui.GetSmall(_npcs[n.NpcId].Name);
        // The tag hangs under the middle of the art, not under the spawn point:
        // a flipped draw mirrors the picture around the origin, so the two are
        // not the same spot.
        int cx = top.X + _textures[frame].Width / 2 - _camera.X;
        int tagY = n.Cy - _camera.Y + _balloonLay.Get("npcNameDown", 2); // the name sits under the feet
        int padX = _balloonLay.Get("npcNamePadX", 3);
        DrawTag(new Rectangle(cx - tag.Width / 2 - padX, tagY, tag.Width + padX * 2, _balloonLay.Get("npcNameH", 16)), tag);
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

    // Worn-or-beginner parts and the doll drawing live in Ui/AvatarArt.cs:
    // the world and the menus share them.

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

    internal void LoadTexture(string image)
    {
        if (_textures.ContainsKey(image)) return;

        string path = Path.Combine(_assetsDir, image);
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing exported png: {Path.GetFullPath(path)}", path);

        using var stream = File.OpenRead(path);
        _textures[image] = Texture2D.FromStream(GraphicsDevice, stream);
    }
}
