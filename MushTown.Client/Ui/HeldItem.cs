using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The piece on the cursor while a click holds one: the item in the inventory
// hand, or the action or skill a click picked up (take-1: no count to show).
// It reads the mouse every frame, so it follows the pointer without a rebuild.
public sealed class HeldItem : Component, IDrawable
{
    private readonly Func<string> _art; // the art of the piece, "" when the cursor carries nothing. Every pickable source draws its art first, so the texture is always loaded.
    public HeldItem(Func<string> art) => _art = art;

    public void Draw(UiGfx gfx)
    {
        string art = _art();
        if (art == "") return;
        var icon = gfx.Tex(art);
        gfx.Sb.Draw(icon, new Vector2(gfx.Mouse.X - icon.Width / 2, gfx.Mouse.Y - icon.Height / 2), Color.White);
    }
}
