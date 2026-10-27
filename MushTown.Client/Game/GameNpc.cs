using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// NPC talk: the action (nearest in reach) and the click (the clicked NPC).
public partial class MushTownGame
{
    // Is this NPC close enough to talk?
    private bool InTalkReach(NpcArt a) =>
        Math.Abs(a.Spawn.X - _mover.X) <= QuestLog.TalkDx && Math.Abs(a.Spawn.Cy - _mover.Y) <= QuestLog.TalkDy;

    // The Talk action talks to the nearest NPC in reach.
    private void TalkNearest()
    {
        NpcArt? best = null;
        float bestD = float.MaxValue;
        foreach (var a in _npcArts)
        {
            if (!InTalkReach(a)) continue;
            float dx = a.Spawn.X - _mover.X, dy = a.Spawn.Cy - _mover.Y;
            float d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = a; }
        }
        if (best == null) return;
        OpenTalk(best.Spawn.NpcId);
    }

    // The talk panel opens on this NPC.
    private void OpenTalk(int npcId)
    {
        _talkNpc = npcId;
        _talkQuest = 0;
        _talkPage = 0;
        PlaySfx("click");
    }

    // Click an NPC in talk reach: the talk panel opens (too far: nothing).
    private void NpcClick(int gx, int gy)
    {
        foreach (var a in _npcArts.OrderByDescending(a => a.Spawn.Cy)) // draw order: high feet on top, clicks match
        {
            var n = a.Spawn;
            var data = _npcs[n.NpcId];
            var tex = _textures[$"npc/{n.NpcId}.png"];
            int feet = n.X;
            int x = n.FacingRight ? SceneMath.FlipDrawX(feet, -data.OriginX, tex.Width) : feet - data.OriginX;
            var rect = new Rectangle(x - _camera.X, n.Cy - data.OriginY - _camera.Y,
                tex.Width, tex.Height);
            if (!HitRect(rect, gx, gy)) continue;
            if (!InTalkReach(a)) continue; // an overlapped NPC behind may be in reach
            OpenTalk(n.NpcId);
            return;
        }
    }
}
