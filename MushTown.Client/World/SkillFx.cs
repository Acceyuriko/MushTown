using MushTown.Shared;

namespace MushTown.Client;

// One skill effect in flight: a cast effect (centered on the caster), a hit
// spark (centered on the mob's body), or a buff loop (riding the player until
// the buff ends). A one-shot plays once and ends its own life; a loop wraps
// forever and the buff end takes it away.
public sealed class SkillFx : Component
{
    private const int Delay = 100; // skill effect frames share one delay (WZ ships no delays for them)
    public const int CastDy = 40; // cast effects center on the torso (bare frames: no WZ origins)

    public int Skill; // which buff a loop belongs to; the spawn site's id
    public bool Loop;
    public bool FacingRight;
    public List<string> Frames = null!; // the frame list this effect plays
    public int Frame;
    public float Timer;
    public float X;
    public float Y;
    public PlayerMover? Rider; // a buff loop rides the player; one-shots leave it null

    // The buff ended: drop the loop of that skill, and only that one. A cast
    // effect of the same skill keeps playing. The node must leave the tree, or
    // the next spawn adds it a second time and it ticks twice per frame.
    public static void StripLoops(List<SkillFx> live, GameObject world, Stack<SkillFx> pool, int id)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (!live[i].Loop || live[i].Skill != id) continue;
            world.RemoveChild(live[i].Owner);
            pool.Push(live[i]);
            live.RemoveAt(i);
        }
    }

    // A pooled effect comes back clean: the spawn site sets the skill, the
    // frames, the anchor and the facing.
    public void Reset()
    {
        Loop = false;
        Rider = null;
        Frames = null!;
        Frame = 0;
        Timer = 0;
        Owner.Dead = false;
    }

    public override void Update(float dt)
    {
        if (Rider != null) // a buff loop is pinned to the player's torso
        {
            X = Rider.X;
            Y = Rider.Y - CastDy;
        }
        Timer += dt * 1000f;
        if (Timer < Delay) return;
        Timer -= Delay; // keep the overflow (single step: 100ms delays, 16ms ticks)
        if (Loop)
        {
            Frame = (Frame + 1) % Frames.Count;
            return;
        }
        if (++Frame < Frames.Count) return;
        Owner.Dead = true;
    }
}
