using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// One cell of the bottom strip: the icon of the target its key binds to, then
// the official cap. The target is read every frame, so a rebind shows without
// a rebuild. The four hint cells keep the icon and the cap at the layout
// offset; the twelve item cells center the icon and put the cap bottom right.
public sealed class QuickCell : Component, IDrawable
{
    private const int HintCells = 4; // the four key hints take the first four cells
    private const int CapPad = 1;    // the item cap keeps off the cell border
    private const int CapShadow = 1; // the dark offset that keeps the cap readable on an icon

    private readonly int _index;
    private readonly string _cap;                   // the official cap art of the key
    private readonly Func<string> _iconArt;         // the bound target's art name, "" when nothing is bound
    private readonly Func<string, Texture2D> _load; // load the art on demand: the component cannot add to the game's cache

    public QuickCell(int index, string cap, Func<string> iconArt, Func<string, Texture2D> load)
    {
        _index = index;
        _cap = cap;
        _iconArt = iconArt;
        _load = load;
    }

    public void Draw(UiGfx gfx)
    {
        var s = Owner.Get<Rect>()!.Screen;
        string art = _iconArt();
        if (art != "") // an action with no official icon (Settings, the sub-windows) draws nothing
        {
            var icon = _load(art);
            var nudge = _index < HintCells ? Layout.HintIcon : default;
            gfx.Sb.Draw(icon, new Vector2(
                s.X + (s.Width - icon.Width) / 2 + nudge.X,
                s.Y + (s.Height - icon.Height) / 2 + nudge.Y), Color.White);
        }
        var cap = _load(_cap);
        if (_index < HintCells)
        {
            gfx.Sb.Draw(cap, new Vector2(s.X + Layout.HintCap.X, s.Y + Layout.HintCap.Y), Color.White);
            return;
        }
        var at = new Vector2(s.Right - cap.Width - CapPad, s.Bottom - cap.Height);
        gfx.Sb.Draw(cap, at + new Vector2(CapShadow, CapShadow), Color.Black);
        gfx.Sb.Draw(cap, at, new Color(200, 200, 200));
    }
}
