using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The red party HP gauge. The panel uses the old UIWindow.img/UserList/Party/
// PartyHP set; the bar over a member's head uses UIParty.img/partyHP. The two
// graduations differ in size and corners; both use a 63x7 bar and the same
// 1px red gauge.
internal static class PartyGauge
{
    // Draw the gauge with its top-left at x/y; pct is 0..100.
    public static void Draw(SpriteBatch sb, Texture2D graduation, Texture2D bar, Texture2D gauge, int x, int y, int pct)
    {
        int bx = x + (graduation.Width - bar.Width) / 2;
        int by = y + (graduation.Height - bar.Height) / 2;
        sb.Draw(bar, new Vector2(bx, by), Color.White);
        int fill = SceneMath.BarWidth(bar.Width, pct, 100);
        if (fill > 0)
            sb.Draw(gauge, new Rectangle(bx, by, fill, bar.Height), new Rectangle(0, 0, 1, gauge.Height), Color.White);
        sb.Draw(graduation, new Vector2(x, y), Color.White); // the frame sits over the bar
    }
}
