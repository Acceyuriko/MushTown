using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The one avatar drawing: plain body parts plus worn gear and the current
// hair / face, sorted back to front, then drawn flipped or not. The world
// (the player, the peers) and the menus (the chars shelf, the create page)
// call it, so a part fix lands here once.
public partial class MushTownGame
{
    private CharaData Body(string action, bool female) => (female ? _charasFemale : _charasMale)[action];

    internal List<int> Delays(string action, bool female) => (female ? _delaysFemale : _delaysMale)[action];

    private static string BodyImgId(bool female) => female ? "00002001" : "00002000";
    private static string HeadImgId(bool female) => female ? "00012001" : "00012000";
    private static string DefaultHairImg(bool female) =>
        (female ? ItemData.DefaultHairFemale : ItemData.DefaultHairMale).ToString("D8");
    private static string DefaultFaceImg(bool female) =>
        (female ? ItemData.DefaultFaceFemale : ItemData.DefaultFaceMale).ToString("D8");

    // The parts of one frame: the plain body, the worn gear, then the cash
    // appearance of a body part instead of its gear, and finally the current
    // hair and face. An unknown id draws nothing.
    private List<CharaPart> MergeParts(string action, int frame, bool female, List<int>? worn, List<int>? cashWorn,
        int hair, int face)
    {
        var gearParts = new List<CharaPart>();
        // A cash item replaces the gear of the same body part (Ma and MaPn
        // both cover the longcoat, Pn the boxers, Wp the sword, So the shoes).
        var cashBodyParts = new HashSet<string>();
        if (cashWorn != null)
            foreach (int id in cashWorn)
                if (_items.TryGetValue(id, out var d) && d.Slot != null &&
                    ItemData.BeginnerImgs.TryGetValue(d.Slot, out string? part))
                    cashBodyParts.Add(part);
        if (worn != null)
            foreach (int id in worn)
            {
                if (!_items.TryGetValue(id, out var d) || d.Slot == null) continue;
                if (cashBodyParts.Contains(ItemData.BeginnerImgs.GetValueOrDefault(d.Slot, ""))) continue;
                AppendGear(gearParts, id, action, frame);
            }
        // Cash: one piece per body part, even when the list holds two overalls.
        var cashDrawn = new HashSet<string>();
        if (cashWorn != null)
            foreach (int id in cashWorn)
            {
                if (!_items.TryGetValue(id, out var d) || d.Slot == null) continue;
                if (ItemData.BeginnerImgs.TryGetValue(d.Slot, out string? part) && !cashDrawn.Add(part)) continue;
                AppendGear(gearParts, id, action, frame);
            }
        var hairData = _hairs.GetValueOrDefault((hair.ToString("D8"), action));
        var faceData = _faces.GetValueOrDefault((face.ToString("D8"), action));
        return AvatarMerge.Parts(Body(action, female).Frames[frame].Parts, gearParts,
            hairData?.Frames[frame].Parts, faceData?.Frames[frame].Parts,
            DefaultHairImg(female), DefaultFaceImg(female));
    }

    private void AppendGear(List<CharaPart> parts, int id, string action, int frame)
    {
        if (!_gear.TryGetValue((id.ToString("D8"), action), out var gear)) return; // art-less gear (rings): nothing to draw
        parts.AddRange(gear.Frames[frame].Parts); // boot load guarantees art for art gear
    }

    private void DrawAvatar(int feetX, int feetY, string action, int frame, bool facingRight, CharaSlot? chara = null)
    {
        var parts = MergeParts(action, frame, chara?.Female ?? false, chara?.Equips, chara?.CashWorn,
            chara?.Hair ?? ItemData.DefaultHairMale, chara?.Face ?? ItemData.DefaultFaceMale);
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
    // turns. Hair may be wider than the body, so pick the body by its img id.
    private int BodyCenterX(int feetX, string action, int frame, bool facingRight, CharaSlot? chara)
    {
        var parts = MergeParts(action, frame, chara?.Female ?? false, chara?.Equips, chara?.CashWorn,
            chara?.Hair ?? ItemData.DefaultHairMale, chara?.Face ?? ItemData.DefaultFaceMale);
        string bodyImg = BodyImgId(chara?.Female ?? false);
        CharaPart? body = parts.FirstOrDefault(p => p.Img == bodyImg);
        if (body == null) return feetX;
        int w = _textures[body.Image].Width;
        return facingRight ? SceneMath.FlipDrawX(feetX, body.X, w) + w / 2 : feetX + body.X + w / 2;
    }

    // Does this hair or face coupon have exported doll parts?
    internal bool HasLookBust(int cosmetic)
    {
        if (cosmetic == 0) return false;
        string id = cosmetic.ToString("D8");
        return (_hairs.TryGetValue((id, "stand1"), out var hair) && hair.Frames[0].Parts.Count > 0)
            || (_faces.TryGetValue((id, "stand1"), out var face) && face.Frames[0].Parts.Count > 0);
    }

    // The bust of one hair or face coupon: the current body and head, shrunk
    // to the box and cropped at the chest, with the head in the middle of the
    // box. A hair coupon drops the face, a face coupon drops the hair (each
    // coupon previews its own part). False when the look has no art yet, so
    // the caller can fall back to the coupon icon.
    internal bool DrawLookBust(UiGfx gfx, Rectangle box, int cosmetic)
    {
        if (cosmetic == 0) return false;
        string id = cosmetic.ToString("D8");
        var look = _hairs.GetValueOrDefault((id, "stand1")) ?? _faces.GetValueOrDefault((id, "stand1"));
        if (look == null || look.Frames[0].Parts.Count == 0) return false;
        bool female = Hero?.Female ?? false;
        string bodyImg = BodyImgId(female), headImg = HeadImgId(female);
        var stand = Body("stand1", female).Frames[0].Parts;
        var parts = stand.Where(p => p.Img == bodyImg || p.Img == headImg).ToList();
        parts.AddRange(look.Frames[0].Parts);
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
    internal void DrawAvatarOnScreen(int feetX, int feetY, string action, int frame, bool facingRight, CharaSlot? chara = null)
    {
        int camX = _camera.X, camY = _camera.Y;
        _camera.X = 0;
        _camera.Y = 0;
        DrawAvatar(feetX, feetY, action, frame, facingRight, chara);
        _camera.X = camX;
        _camera.Y = camY;
    }
}

// One doll in a UI tree: the owner rect's top-left is its feet. The frame is
// read every draw, so a walk cycle shows without a rebuild. The character is
// optional: without one the doll draws the default male beginner.
public sealed class AvatarPreview : Component, IDrawable
{
    private readonly MushTownGame _game;
    private readonly string _action;
    private readonly Func<int> _frame;
    private readonly bool _faceRight;
    private readonly Func<CharaSlot?>? _chara;

    public AvatarPreview(MushTownGame game, string action, Func<int> frame, bool faceRight, Func<CharaSlot?>? chara = null)
    {
        _game = game; _action = action; _frame = frame; _faceRight = faceRight; _chara = chara;
    }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        _game.DrawAvatarOnScreen(r.X, r.Y, _action, _frame(), _faceRight, _chara?.Invoke());
    }
}
