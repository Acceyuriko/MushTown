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

    // The frame playing now: where it lands in the world and which picture it
    // is. The raw art faces left, so the flipped draw mirrors around the origin
    // (avatar parts do the same). The feet stay put: the top left moves with
    // each frame's own origin.
    private (Point Top, string Image) NpcFrame(NpcArt a)
    {
        var stand = a.Owner.Get<LifeStand>()!;
        int frame = stand.IndexAt((float)_timeMs);
        var image = stand.Art.Images[frame];
        var origin = stand.Art.Origins[frame];
        var n = a.Spawn;
        int x = n.FacingRight
            ? SceneMath.FlipDrawX(n.X, -origin.X, _textures[image].Width)
            : n.X - origin.X;
        return (new Point(x, n.Cy - origin.Y), image);
    }

    // Where an NPC's head is on the screen: the middle of the playing frame, its top edge.
    private Point NpcHead(NpcArt a)
    {
        var (top, image) = NpcFrame(a);
        return new Point(top.X + _textures[image].Width / 2 - _camera.X, top.Y - _camera.Y);
    }

    // The talk panel opens on this NPC. A mouse-opened dialog ignores the
    // distance rule for as long as it stays open; the Space action still
    // picks only a near NPC.
    private void OpenTalk(int npcId, bool byMouse = false)
    {
        _talkNpc = npcId;
        _talkQuest = 0;
        _talkReward = 0;
        _talkPage = 0;
        _talkByMouse = byMouse;
        PlaySfx("click");
    }

    // The NPC under the mouse: the topmost sprite by draw order. The whole
    // frame is the target, and there is no distance rule.
    private NpcArt? NpcAt(int gx, int gy)
    {
        NpcArt? hit = null;
        int hitCy = int.MinValue;
        foreach (var a in _npcArts) // draw order: high feet on top, clicks match
        {
            var (top, image) = NpcFrame(a);
            var tex = _textures[image];
            var rect = new Rectangle(top.X - _camera.X, top.Y - _camera.Y, tex.Width, tex.Height);
            if (HitRect(rect, gx, gy) && a.Spawn.Cy > hitCy)
            {
                hit = a;
                hitCy = a.Spawn.Cy;
            }
        }
        return hit;
    }

    // Click an NPC anywhere on its picture: the dialog opens. No reach test.
    private void NpcClick(int gx, int gy)
    {
        var npc = NpcAt(gx, gy);
        if (npc != null) OpenTalk(npc.Spawn.NpcId, byMouse: true);
    }
}
