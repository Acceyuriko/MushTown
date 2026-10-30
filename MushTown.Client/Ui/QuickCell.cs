using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// One cell of the bottom strip: the icon of the target its key binds to, then
// the official cap. The target is read every frame, so a rebind shows without
// a rebuild. All sixteen cells draw the same way: the cap in the top-left
// corner, the icon centered.
public sealed class QuickCell : Component, IDrawable
{
    private readonly string _cap;                   // the official cap art of the key
    private readonly Func<string> _iconArt;         // the bound target's art name, "" when nothing is bound
    private readonly Func<string, Texture2D> _load; // load the art on demand: the component cannot add to the game's cache

    public QuickCell(string cap, Func<string> iconArt, Func<string, Texture2D> load)
    {
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
            gfx.Sb.Draw(icon, new Vector2(
                s.X + (s.Width - icon.Width) / 2,
                s.Y + (s.Height - icon.Height) / 2), Color.White);
        }
        gfx.Sb.Draw(_load(_cap), new Vector2(s.X + Layout.QslotCap.X, s.Y + Layout.QslotCap.Y), Color.White);
    }
}
