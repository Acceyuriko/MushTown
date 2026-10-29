using MushTown.Shared;

namespace MushTown.Client;

// The one avatar drawing: worn-or-beginner parts, sorted back to front, then
// drawn flipped or not. The world (the player, the peers) and the menus (the
// chars shelf, the create page) call it, so a part fix lands here once.
public partial class MushTownGame
{
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

    // The same drawing on a UI spot: the world draw adds the camera, so park
    // it for the call (the menus draw at screen pixels).
    internal void DrawAvatarOnScreen(int feetX, int feetY, string action, int frame, bool facingRight)
    {
        int camX = _camera.X, camY = _camera.Y;
        _camera.X = 0;
        _camera.Y = 0;
        DrawAvatar(feetX, feetY, action, frame, facingRight);
        _camera.X = camX;
        _camera.Y = camY;
    }
}

// One doll in a UI tree: the owner rect's top-left is its feet. The frame is
// read every draw, so a walk cycle shows without a rebuild.
public sealed class AvatarPreview : Component, IDrawable
{
    private readonly MushTownGame _game;
    private readonly string _action;
    private readonly Func<int> _frame;
    private readonly bool _faceRight;

    public AvatarPreview(MushTownGame game, string action, Func<int> frame, bool faceRight)
    {
        _game = game; _action = action; _frame = frame; _faceRight = faceRight;
    }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        _game.DrawAvatarOnScreen(r.X, r.Y, _action, _frame(), _faceRight);
    }
}
