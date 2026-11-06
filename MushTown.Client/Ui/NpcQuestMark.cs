using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The balloon one NPC wears when it has quest work: 0 to take, 1 under way,
// 2 to hand in, -1 none (UIWindow2.img/QuestIcon). It hangs over the head by
// its tail, and the NPC keeps quiet while it shows: the two never overlap.
public sealed class NpcQuestMark : Component, IDrawable
{
    public required Func<int> Icon; // the quest state of this NPC
    public required Func<Point> Head; // the NPC's head on the screen
    public required PanelLayout Lay;  // layouts/panel-balloon.json
    public required Func<int, QuestMarkData> Data; // the animation of icon n

    public bool Showing => Icon() >= 0;

    public void Draw(UiGfx gfx)
    {
        int icon = Icon();
        if (icon < 0) return;
        var data = Data(icon);
        int scale = Math.Max(1, Lay.Get("npcMarkDelayScale", 1));
        int frame = Idle.At(data.Delays, gfx.Time / scale); // the delays add up and loop
        var art = gfx.Tex($"ui/npc_quest_{icon}_{frame}.png");
        var head = Head();
        int x = head.X - data.TailX[frame]; // the tail points at the middle of the head
        int y = head.Y - data.TailY[frame] - Lay.Get("npcMarkAbove", 2);
        gfx.Sb.Draw(art, new Vector2(x, y), Color.White);
    }
}
