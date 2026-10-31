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

    // The NPC balloon: the nearest NPC in reach says its line when the player
    // walks up (the talk panel has the full talk). One at a time, a few
    // seconds each, and never the one the player is already talking to.
    private int _npcBalloonNpc;      // the NPC the bubble belongs to (0 = none)
    private double _npcBalloonUntil; // when the bubble goes away
    private int _npcBalloonDone;     // the NPC that already said its line (0 = none)

    private void UpdateNpcBalloon()
    {
        if (_talkNpc != 0 || _showShop) { _npcBalloonDone = _npcBalloonNpc; _npcBalloonNpc = 0; return; } // the talk panel and the shop have the NPC
        NpcArt? best = null;
        float bestD = float.MaxValue;
        foreach (var a in _npcArts)
        {
            if (!InTalkReach(a) || _npcs[a.Spawn.NpcId].Greet == "") continue;
            float dx = a.Spawn.X - _mover.X, dy = a.Spawn.Cy - _mover.Y;
            float d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = a; }
        }
        int id = best?.Spawn.NpcId ?? 0;
        if (id == 0) { _npcBalloonNpc = 0; _npcBalloonDone = 0; return; } // walked away: the next approach talks again
        if (id != _npcBalloonNpc)
        {
            if (id == _npcBalloonDone) { _npcBalloonNpc = 0; return; } // it already said its line
            _npcBalloonNpc = id;
            _npcBalloonUntil = _timeMs + _balloonLay.Get("npcLifeMs", 3000);
        }
        else if (_timeMs > _npcBalloonUntil) { _npcBalloonDone = id; _npcBalloonNpc = 0; }
    }

    // The line the bubble shows and the head it hangs over, on the screen.
    private (string text, Point head) NpcSpeech()
    {
        if (_npcBalloonNpc == 0) return ("", default);
        foreach (var a in _npcArts)
        {
            var n = a.Spawn;
            if (n.NpcId != _npcBalloonNpc) continue;
            var data = _npcs[n.NpcId];
            var tex = _textures[$"npc/{n.NpcId}.png"];
            int x = n.FacingRight ? SceneMath.FlipDrawX(n.X, -data.OriginX, tex.Width) : n.X - data.OriginX;
            return (QText(_npcs[n.NpcId].Greet), new Point(x + tex.Width / 2 - _camera.X, n.Cy - data.OriginY - _camera.Y));
        }
        return ("", default);
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
