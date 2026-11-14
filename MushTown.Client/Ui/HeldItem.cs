using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The piece on the cursor while a click holds one: the item in the inventory
// hand, or the action or skill a click picked up (take-1: no count to show).
// It reads the mouse every frame, so it follows the pointer without a rebuild.
// A hair or face coupon shows its bust instead of the item icon.
public sealed class HeldItem : Component, IDrawable
{
    private readonly Func<string> _art; // the art of the piece, "" when the cursor carries nothing. Every pickable source draws its art first, so the texture is always loaded.
    private readonly Func<int> _bust; // the cosmetic a coupon shows, 0 for every other piece
    private readonly int _bustSize; // the square the bust is drawn in: the bag cell
    private readonly Action<UiGfx, Rectangle, int> _drawBust;
    public HeldItem(Func<string> art, Func<int> bust, int bustSize, Action<UiGfx, Rectangle, int> drawBust)
    {
        _art = art;
        _bust = bust;
        _bustSize = bustSize;
        _drawBust = drawBust;
    }

    public void Draw(UiGfx gfx)
    {
        int bust = _bust();
        if (bust != 0)
        {
            _drawBust(gfx, new Rectangle(gfx.Mouse.X - _bustSize / 2, gfx.Mouse.Y - _bustSize / 2, _bustSize, _bustSize), bust);
            return;
        }
        string art = _art();
        if (art == "") return;
        var icon = gfx.Tex(art);
        gfx.Sb.Draw(icon, new Vector2(gfx.Mouse.X - icon.Width / 2, gfx.Mouse.Y - icon.Height / 2), Color.White);
    }
}
