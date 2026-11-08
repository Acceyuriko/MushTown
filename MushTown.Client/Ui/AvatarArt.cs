using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The one avatar drawing: plain body parts plus worn gear, sorted back to
// front, then drawn flipped or not. The world (the player, the peers) and the
// menus (the chars shelf, the create page) call it, so a part fix lands here once.
public partial class MushTownGame
{
    // The parts of one frame: the plain body parts plus the worn gear files,
    // re-sorted back-to-front (same stable order the exporter bakes). Unknown
    // ids draw nothing.
    private List<CharaPart> MergeParts(string action, int frame, List<int> worn)
    {
        var gearParts = new List<CharaPart>();
        foreach (int id in worn)
        {
            if (!_items.TryGetValue(id, out var d) || d.Slot == null) continue; // hand-made saves may wear anything
            var key = (id.ToString("D8"), action);
            if (!_gear.TryGetValue(key, out var gear)) continue; // art-less gear (rings): nothing to draw
            gearParts.AddRange(gear.Frames[frame].Parts); // boot load guarantees art for art gear
        }
        var parts = new List<CharaPart>(_charas[action].Frames[frame].Parts);
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

    // The horizontal center of the drawn body. The parts are not symmetric
    // around the feet, so a balloon hung on the feet leans when the character
    // turns; the body part is the widest one in the frame.
    private int BodyCenterX(int feetX, string action, int frame, bool facingRight, List<int>? worn)
    {
        var parts = worn == null || worn.Count == 0
            ? _charas[action].Frames[frame].Parts
            : MergeParts(action, frame, worn);
        CharaPart? body = null;
        int area = 0;
        foreach (var p in parts)
        {
            var tex = _textures[p.Image];
            if (tex.Width * tex.Height <= area) continue;
            area = tex.Width * tex.Height;
            body = p;
        }
        if (body == null) return feetX;
        int w = _textures[body.Image].Width;
        return facingRight ? SceneMath.FlipDrawX(feetX, body.X, w) + w / 2 : feetX + body.X + w / 2;
    }
    // Does this hair or face coupon have its parts baked into the doll yet?
    internal bool HasLookBust(int cosmetic) =>
        cosmetic != 0 && _charas["stand1"].Frames[0].Parts.Any(p => p.Img == cosmetic.ToString("D8"));

    // The bust of one hair or face coupon: the doll's upper body, shrunk to
    // the box and cropped at the chest, with the head in the middle of the
    // box. A hair coupon drops the face, a face coupon drops the hair (each
    // coupon previews its own part). False when the look has no art yet, so
    // the caller can fall back to the coupon icon.
    internal bool DrawLookBust(UiGfx gfx, Rectangle box, int cosmetic)
    {
        var stand = _charas["stand1"].Frames[0].Parts;
        string img = cosmetic.ToString("D8");
        var parts = stand.Where(p => p.Img == img).ToList(); // the look itself
        if (parts.Count == 0) return false;
        foreach (var p in stand)
            if (p.Img is "00002000" or "00012000")
                parts.Add(p);
        parts = parts.OrderByDescending(p => p.Z).ToList(); // back to front, like the doll
        foreach (var p in parts) LoadTexture(p.Image);
        // The head keeps 65% of the box and sits in the middle; the chest
        // shows under it and the box crops whatever hangs past the bottom.
        var head = parts.Single(p => p.Image.EndsWith("_head.png"));
        var headTex = _textures[head.Image];
        float headH = headTex.Height;
        float scale = Math.Min(1f, box.Height * 0.65f / headH);
        float ox = box.X + box.Width / 2f - (head.X + headTex.Width / 2f) * scale;
        float oy = box.Y + (box.Height - headH * scale) / 2f - head.Y * scale;
        foreach (var p in parts)
        {
            var tex = _textures[p.Image];
            float dx = ox + p.X * scale, dy = oy + p.Y * scale;
            float cut = Math.Max(0, dy + tex.Height * scale - box.Bottom); // the box cuts the bust
            if (cut >= tex.Height * scale) continue;
            var src = new Rectangle(0, 0, tex.Width, (int)MathF.Round(tex.Height - cut / scale));
            gfx.Sb.Draw(tex, new Vector2(dx, dy), src, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
        return true;
    }

    // The same drawing on a UI spot: the world draw adds the camera, so park
    // it for the call (the menus draw at screen pixels).
    internal void DrawAvatarOnScreen(int feetX, int feetY, string action, int frame, bool facingRight, List<int>? worn = null)
    {
        int camX = _camera.X, camY = _camera.Y;
        _camera.X = 0;
        _camera.Y = 0;
        DrawAvatar(feetX, feetY, action, frame, facingRight, worn);
        _camera.X = camX;
        _camera.Y = camY;
    }
}

// One doll in a UI tree: the owner rect's top-left is its feet. The frame is
// read every draw, so a walk cycle shows without a rebuild. The worn list is
// optional: the character info portrait and the shop preview show the real
// gear; without a worn list the doll draws the plain body.
public sealed class AvatarPreview : Component, IDrawable
{
    private readonly MushTownGame _game;
    private readonly string _action;
    private readonly Func<int> _frame;
    private readonly bool _faceRight;
    private readonly Func<List<int>?>? _worn;

    public AvatarPreview(MushTownGame game, string action, Func<int> frame, bool faceRight, Func<List<int>?>? worn = null)
    {
        _game = game; _action = action; _frame = frame; _faceRight = faceRight; _worn = worn;
    }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        _game.DrawAvatarOnScreen(r.X, r.Y, _action, _frame(), _faceRight, _worn?.Invoke());
    }
}
