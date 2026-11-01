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

    // The top left of an NPC's stand art, in world coordinates. The raw art
    // faces left, so the flipped draw mirrors around the origin (avatar parts
    // do the same). Every NPC anchor reads this, never the arithmetic twice.
    private Point NpcTopLeft(NpcSpawn n)
    {
        var data = _npcs[n.NpcId];
        var tex = _textures[$"npc/{n.NpcId}.png"];
        int x = n.FacingRight ? SceneMath.FlipDrawX(n.X, -data.OriginX, tex.Width) : n.X - data.OriginX;
        return new Point(x, n.Cy - data.OriginY);
    }

    // Where an NPC's head is on the screen: the middle of its stand art, its top edge.
    private Point NpcHead(NpcSpawn n)
    {
        var top = NpcTopLeft(n);
        return new Point(top.X + _textures[$"npc/{n.NpcId}.png"].Width / 2 - _camera.X, top.Y - _camera.Y);
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
            var tex = _textures[$"npc/{n.NpcId}.png"];
            var top = NpcTopLeft(n);
            var rect = new Rectangle(top.X - _camera.X, top.Y - _camera.Y, tex.Width, tex.Height);
            if (!HitRect(rect, gx, gy)) continue;
            if (!InTalkReach(a)) continue; // an overlapped NPC behind may be in reach
            OpenTalk(n.NpcId);
            return;
        }
    }
}
