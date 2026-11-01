using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The balloon one NPC wears when it has quest work: 0 to take, 1 under way,
// 2 to hand in, -1 none (UIWindow2.img/QuestIcon). It hangs over the head, and
// the NPC keeps quiet while it shows: the two never overlap.
public sealed class NpcQuestMark : Component, IDrawable
{
    public required Func<int> Icon; // the quest state of this NPC
    public required Func<Point> Head; // the NPC's head on the screen
    public required PanelLayout Lay;  // layouts/panel-balloon.json
    public required Func<int, int> OriginX; // the anchor column of icon n (its WZ origin)

    public bool Showing => Icon() >= 0;

    public void Draw(UiGfx gfx)
    {
        int icon = Icon();
        if (icon < 0) return;
        var art = gfx.Tex($"ui/npc_quest_{icon}.png");
        var head = Head();
        int x = head.X - OriginX(icon);
        int y = head.Y - art.Height - Lay.Get("npcMarkAbove", 4); // the whole balloon rides above the head
        gfx.Sb.Draw(art, new Vector2(x, y), Color.White);
    }
}
