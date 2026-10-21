using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The frosted glass behind a window: the screen at 1/4 size, drawn back
// up over the window rect. The upscale is the blur, and the official
// window art (40% white) over it reads as the official frosted panel.
public sealed class BlurBackdrop : Component, IDrawable
{
    // The blur texture is the game screen at 1/4 size.
    public const int Scale = 4;

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        var src = new Rectangle(r.X / Scale, r.Y / Scale, r.Width / Scale, r.Height / Scale);
        gfx.Sb.Draw(gfx.Blur, r, src, Color.White); // destination, then source
    }
}
