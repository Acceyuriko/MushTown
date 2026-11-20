using Microsoft.Xna.Framework;
using MushTown.Shared;
using Microsoft.Xna.Framework.Graphics;

namespace MushTown.Client;

// The time left on a skill icon: what is left and the whole time. The whole time is 0 for a skill with no countdown.
public readonly record struct SkillTimer(float LeftMs, float TotalMs)
{
    public bool Active => TotalMs > 0 && LeftMs > 0;
}

// The countdown look of a skill icon, drawn the same way in the buff bar, the key window, the quick slots and the skill window.
// A grey mask covers the icon from the bottom up as the time runs out; under 60 s the whole seconds left (rounded down) are yellow in the middle,
// from 60 s on the whole minutes left (rounded down) are white at the bottom left. A number of 0 is not drawn. No countdown draws nothing. The layer count of a layered buff uses the same text code. Every number is in layouts/panel-timer.json.
public static class TimerOverlay
{
    internal static readonly PanelLayout Lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-timer.json"));

    // icon = where the icon is drawn on the screen.
    public static void Draw(UiGfx gfx, Rectangle icon, SkillTimer timer)
    {
        if (!timer.Active) return;
        var inner = icon;
        inner.Inflate(-Lay.Get("maskInset", 1), -Lay.Get("maskInset", 1)); // the mask stays inside the outline of the icon
        int covered = (int)Math.Round(inner.Height * (1 - Math.Min(1f, timer.LeftMs / timer.TotalMs)));
        gfx.Sb.Draw(gfx.Pixel, new Rectangle(inner.X, inner.Bottom - covered, inner.Width, covered),
            new Color(Lay.Get("maskR", 128), Lay.Get("maskG", 128), Lay.Get("maskB", 128), Lay.Get("maskA", 170)));
        var (number, minutes) = Label(timer.LeftMs);
        if (number == 0) return; // under 1 s nothing is written (Teleport's 0.48 s cooldown); the mask still shows
        if (!minutes)
        {
            var t = gfx.Ui.GetSized(number.ToString(), Lay.Get("secondsPx", 14));
            Outlined(gfx, t, icon.X + (icon.Width - t.Width) / 2 + Lay.Get("secondsDx", 0), icon.Y + (icon.Height - t.Height) / 2 + Lay.Get("secondsDy", 0),
                new Color(Lay.Get("secondsR", 255), Lay.Get("secondsG", 221), Lay.Get("secondsB", 0)));
            return;
        }
        var m = gfx.Ui.GetSized(number.ToString(), Lay.Get("minutesPx", 12));
        Outlined(gfx, m, icon.X + Lay.Get("minutesX", 1), icon.Bottom - m.Height - Lay.Get("minutesBottom", 1),
            new Color(Lay.Get("minutesR", 255), Lay.Get("minutesG", 255), Lay.Get("minutesB", 255)));
    }

    // The number on the icon, rounded down: whole seconds under 60 s, whole minutes from 60 s on. A number of 0 is not drawn.
    internal static (int Number, bool Minutes) Label(float leftMs) =>
        leftMs < 60000 ? ((int)(leftMs / 1000), false) : ((int)(leftMs / 60000), true);

    // The layer count of a layered buff (Elemental Drain, Elemental Adaptation), gold, in the top right or the bottom right corner of the icon.
    public static void DrawCount(UiGfx gfx, Rectangle icon, int layers, bool top)
    {
        var t = gfx.Ui.GetSized(layers.ToString(), Lay.Get("countPx", 14));
        int x = icon.Right - t.Width - Lay.Get("countRight", 1);
        int y = top ? icon.Y + Lay.Get("countTop", 1) : icon.Bottom - t.Height - Lay.Get("countBottom", 1);
        Outlined(gfx, t, x, y, new Color(Lay.Get("countR", 255), Lay.Get("countG", 208), Lay.Get("countB", 40)));
    }

    private static void Outlined(UiGfx gfx, Texture2D text, int x, int y, Color ink)
    {
        int sh = Lay.Get("shadow", 1);
        var edge = new Color(0, 0, 0, 200);
        gfx.Sb.Draw(text, new Vector2(x - sh, y), edge);
        gfx.Sb.Draw(text, new Vector2(x + sh, y), edge);
        gfx.Sb.Draw(text, new Vector2(x, y - sh), edge);
        gfx.Sb.Draw(text, new Vector2(x, y + sh), edge);
        gfx.Sb.Draw(text, new Vector2(x, y), ink);
    }
}
