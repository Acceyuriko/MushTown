using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// One cell of the bottom strip: the icon of the target its key binds to, then
// the official cap. The target is read every frame, so a rebind shows without
// a rebuild. All 32 cells draw the same way: the cap in the top-left
// corner, the icon centered.
public sealed class QuickCell : Component, IDrawable
{
    private readonly Func<string> _key;             // the key name (it follows the preset)
    private readonly Func<string> _iconArt;         // the bound target's art name, "" when nothing is bound
    private readonly Func<string, Texture2D> _load; // load the art on demand: the component cannot add to the game's cache
    private readonly Func<SkillTimer> _timer;       // the countdown of the bound skill, none for anything else

    public QuickCell(Func<string> key, Func<string> iconArt, Func<string, Texture2D> load, Func<SkillTimer> timer)
    {
        _key = key;
        _iconArt = iconArt;
        _load = load;
        _timer = timer;
    }

    public void Draw(UiGfx gfx)
    {
        var s = Owner.Get<Rect>()!.Screen;
        string art = _iconArt();
        if (art != "") // an action with no official icon (Settings, the sub-windows) draws nothing
        {
            var icon = _load(art);
            var at = new Point(s.X + (s.Width - icon.Width) / 2, s.Y + (s.Height - icon.Height) / 2);
            gfx.Sb.Draw(icon, at.ToVector2(), Color.White);
            TimerOverlay.Draw(gfx, new Rectangle(at.X, at.Y, icon.Width, icon.Height), _timer());
        }
        string key = _key();
        string cap = KeyConfigWindow.CapArt(key);
        var pos = new Vector2(s.X + Layout.QslotCap.X, s.Y + Layout.QslotCap.Y);
        if (cap != "")
        {
            gfx.Sb.Draw(_load(cap), pos, Color.White);
            return;
        }
        // No official cap for this key (Backspace, CapsLock, RightShift, numpad ...): the key name
        // in small text, shrunk to fit inside the cell.
        var name = gfx.Ui.GetSmall(key);
        float scale = Math.Min(1f, (s.Width - 2 * Layout.QslotCap.X) / (float)name.Width);
        gfx.Sb.Draw(name, pos, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}

// The blue frame of the cell the key window picked for a new key. One object over the
// whole strip, after every cell, so a neighbour never paints over the frame.
public sealed class QuickSelect : Component, IDrawable
{
    private readonly Func<Rectangle?> _picked; // the screen-space rect of the picked cell, null when no cell is picked
    private readonly Func<string, Texture2D> _load;

    public QuickSelect(Func<Rectangle?> picked, Func<string, Texture2D> load)
    {
        _picked = picked;
        _load = load;
    }

    public void Draw(UiGfx gfx)
    {
        if (_picked() is not { } cell) return;
        gfx.Sb.Draw(_load("ui/hud_qslot_sel.png"), new Vector2(cell.X + Layout.QslotSelect.X, cell.Y + Layout.QslotSelect.Y), Color.White);
    }
}
