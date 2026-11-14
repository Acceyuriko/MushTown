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
    private CharaData Body(string action, int skin) => _charas[(skin, action)];

    internal List<int> Delays(string action, int skin) => _delays[(skin, action)];

    // The live frame for an action and skin: AvatarPreview uses this so a
    // panel doll breathes like the world doll.
    internal int IdleFrame(string action, int skin) =>
        Idle.Frame(action, Delays(action, skin), TimeMs);

    private static string BodyImgId(int skin) => CharaSlot.BodyId(skin).ToString("D8");
    private static string HeadImgId(int skin) => CharaSlot.HeadId(skin).ToString("D8");
    private static string DefaultHairImg() => ItemData.DefaultHair.ToString("D8");
    private static string DefaultFaceImg() => ItemData.DefaultFace.ToString("D8");

    // The parts of one frame: the plain body, the worn gear, then the cash
    // appearance of a body part instead of its gear, and finally the current
    // hair and face. An unknown id draws nothing.
    private List<CharaPart> MergeParts(string action, int frame, int skin, List<int>? worn, List<int>? cashWorn,
        int hair, int face)
    {
        var gearParts = new List<CharaPart>();
        // A cash item replaces the gear of the same slot (Ma and MaPn share
        // the top area; every other slot is a plain name match).
        var cashSlots = new List<string>();
        if (cashWorn != null)
            foreach (int id in cashWorn)
                if (_items.TryGetValue(id, out var d) && d.Slot != null)
                    cashSlots.Add(d.Slot);
        if (worn != null)
            foreach (int id in worn)
            {
                if (!_items.TryGetValue(id, out var d) || d.Slot == null) continue;
                if (cashSlots.Any(slot => Progression.SameBodyArea(slot, d.Slot))) continue;
                AppendGear(gearParts, id, action, frame);
            }
        // Cash: one piece per slot, even when a broken save holds two.
        var cashDrawn = new List<string>();
        if (cashWorn != null)
            foreach (int id in cashWorn)
            {
                if (!_items.TryGetValue(id, out var d) || d.Slot == null) continue;
                if (cashDrawn.Any(slot => Progression.SameBodyArea(slot, d.Slot))) continue;
                cashDrawn.Add(d.Slot);
                AppendGear(gearParts, id, action, frame);
            }
        var hairData = _hairs.GetValueOrDefault((hair.ToString("D8"), action));
        var faceData = _faces.GetValueOrDefault((face.ToString("D8"), action));
        bool hasHat = HasSlot(worn, "Cp") || HasSlot(cashWorn, "Cp");
        return AvatarMerge.Parts(Body(action, skin).Frames[frame].Parts, gearParts,
            hairData?.Frames[frame].Parts, faceData?.Frames[frame].Parts,
            DefaultHairImg(), DefaultFaceImg(), hasHat);
    }

    private bool HasSlot(List<int>? ids, string slot) =>
        ids != null && ids.Any(id => _items.TryGetValue(id, out var data) && data.Slot == slot);

    private void AppendGear(List<CharaPart> parts, int id, string action, int frame)
    {
        // The back-facing ladder/rope frames must not draw face or eye gear on the skull.
        if (action is "ladder" or "rope" && _items.TryGetValue(id, out var data) && data.Slot is "Af" or "Ay") return;
        if (!_gear.TryGetValue((id.ToString("D8"), action), out var gear)) return; // art-less gear (rings): nothing to draw
        parts.AddRange(gear.Frames[frame].Parts); // boot load guarantees art for art gear
    }

    private int DrawAvatar(int feetX, int feetY, string action, int frame, bool facingRight, CharaSlot? chara = null,
        float scale = 1f)
    {
        var parts = MergeParts(action, frame, chara?.Skin ?? 0, chara?.Equips, chara?.CashWorn,
            chara?.Hair ?? ItemData.DefaultHair, chara?.Face ?? ItemData.DefaultFace);
        int centerX = AvatarCenterX(feetX, parts, facingRight, chara?.Skin ?? 0);
        foreach (var part in parts)
        {
            int dx = (int)MathF.Round(part.X * scale);
            int dy = (int)MathF.Round(part.Y * scale);
            if (facingRight)
            {
                var tex = _textures[part.Image];
                int x = (int)MathF.Round(feetX - (part.X + tex.Width) * scale);
                DrawTexture(part.Image, x, feetY + dy, true, scale);
            }
            else
            {
                DrawTexture(part.Image, feetX + dx, feetY + dy, false, scale);
            }
        }
        return centerX;
    }

    // The horizontal center of the drawn body, from parts the caller already
    // merged. The parts are not symmetric around the feet, so a balloon hung
    // on the feet leans when the character turns. Hair may be wider than the
    // body, so pick the body by its img id.
    private int AvatarCenterX(int feetX, List<CharaPart> parts, bool facingRight, int skin)
    {
        string bodyImg = BodyImgId(skin);
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
    // coupon previews its own part).
    internal void DrawLookBust(UiGfx gfx, Rectangle box, int cosmetic)
    {
        string id = cosmetic.ToString("D8");
        var look = _hairs.GetValueOrDefault((id, "stand1")) ?? _faces.GetValueOrDefault((id, "stand1"))!;
        int skin = Hero?.Skin ?? 0;
        string bodyImg = BodyImgId(skin), headImg = HeadImgId(skin);
        var stand = Body("stand1", skin).Frames[0].Parts;
        var bodyParts = stand.Where(p => p.Img == bodyImg || p.Img == headImg).ToList();
        bool hairLook = _hairs.ContainsKey((id, "stand1"));
        var parts = AvatarMerge.Parts(bodyParts, Array.Empty<CharaPart>(),
            hairLook ? look.Frames[0].Parts : null,
            hairLook ? null : look.Frames[0].Parts,
            DefaultHairImg(), DefaultFaceImg()); // no hat: drop the hair-under-cap set
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
    }

    // The same drawing on a UI spot: the world draw adds the camera, so park
    // it for the call (the menus draw at screen pixels).
    internal void DrawAvatarOnScreen(int feetX, int feetY, string action, int frame, bool facingRight, CharaSlot? chara = null,
        float scale = 1f)
    {
        int camX = _camera.X, camY = _camera.Y;
        _camera.X = 0;
        _camera.Y = 0;
        DrawAvatar(feetX, feetY, action, frame, facingRight, chara, scale);
        _camera.X = camX;
        _camera.Y = camY;
    }
}

// One doll in a UI tree: the owner rect's top-left is its feet. The frame is
// read every draw, so a walk cycle shows without a rebuild. The character is
// optional: without one the doll draws the default skin 0 beginner.
public sealed class AvatarPreview : Component, IDrawable
{
    private readonly MushTownGame _game;
    private readonly string _action;
    private readonly Func<int>? _frame;
    private readonly bool _faceRight;
    private readonly Func<CharaSlot?>? _chara;
    private readonly float _scale;

    public AvatarPreview(MushTownGame game, string action, bool faceRight, Func<int>? frame = null, Func<CharaSlot?>? chara = null,
        float scale = 1f)
    {
        _game = game; _action = action; _frame = frame; _faceRight = faceRight; _chara = chara; _scale = scale;
    }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        var chara = _chara?.Invoke();
        int frame = _frame?.Invoke() ?? _game.IdleFrame(_action, chara?.Skin ?? 0);
        _game.DrawAvatarOnScreen(r.X, r.Y, _action, frame, _faceRight, chara, _scale);
    }
}
