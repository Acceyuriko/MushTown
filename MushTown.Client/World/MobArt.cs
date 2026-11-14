using MushTown.Shared;

namespace MushTown.Client;

// Mob and drop art state, one per live view (moved to tree children in
// S6). The mob plays its own anim on Update: a flinch returns to the
// idle, the death anim marks the Owner dead and the game collects it
// after the tree step. Drops never tick, and both still draw through the
// y-sorted list (the tree never y-sorts; S8 verdict: the list stays).
public sealed class MobArt : Component
{
    public int Mob;
    public MobData Data = null!; // the exported file: frames and the idle name come from here
    public float X;
    public float Y;
    public bool FacingRight; // AI poses face along (raw art faces left)
    public long Hp;
    public long MaxHp;
    public string Action { get; private set; } = ""; // stand, move, hit1, die1
    public List<MobFrame> Frames { get; private set; } = null!; // the frames of the current action: SetAction fills it
    public int Frame;
    public float Timer;

    // The idle is stand, and move when the mob has none or its stand is a single
    // picture: a still stand would freeze the mob and its tip (the snail only
    // has one stand frame, its crawl is the move action).
    public string Idle => Data.Idle;

    // One action at a time: the tick and the draw read the same list.
    public void SetAction(string action)
    {
        Action = action;
        Frames = FramesOf(action);
        Frame = 0;
        Timer = 0;
    }

    private List<MobFrame> FramesOf(string action) =>
        Data.Actions.FirstOrDefault(a => a.Name == action)?.Frames
            ?? throw new InvalidDataException($"mob {Mob} has no {action} action");

    // Every frame carries its own delay from the WZ (the exporter reads
    // Mob/<id>.img/<action>/<n>/delay). The last frame loops: a flinch goes back
    // to the idle, the idles start over. Only the death ends the life: Frame
    // lands on Frames.Count there, so a corpse must not be ticked or drawn
    // again — CollectDead drops it first. A long tick (a lag spike) walks
    // several frames, or the 50 ms frames would play too slow.
    public override void Update(float dt)
    {
        Timer += dt * 1000f;
        while (Timer >= Frames[Frame].Delay)
        {
            Timer -= Frames[Frame].Delay;
            if (++Frame < Frames.Count) continue;
            if (Action == "die1") { Owner.Dead = true; return; }
            if (Action == "hit1") { Action = Idle; Frames = FramesOf(Idle); }
            Frame = 0;
        }
    }

    // The mobs that finished their death anim: the tree skipped them
    // already, so this only drops the body and the dict entry.
    public static void CollectDead(Dictionary<int, MobArt> mobs, GameObject world)
    {
        List<int>? gone = null;
        foreach (var (uid, m) in mobs)
            if (m.Owner.Dead) (gone ??= new()).Add(uid);
        if (gone == null) return;
        foreach (int uid in gone)
        {
            world.RemoveChild(mobs[uid].Owner);
            mobs.Remove(uid);
        }
    }
}

// The frame an idle animation shows at a given time: the delays of the frames
// add up and the time runs through them again and again. A live mob plays its
// frames on Update; a world map tip plays an NPC or a monster with this.
// Only character dolls use Frame; NPCs and monsters stay on At.
public static class Idle
{
    public static int At(IReadOnlyList<int> delays, float timeMs)
    {
        int total = 0;
        foreach (int delay in delays) total += delay;
        int t = (int)(timeMs % total);
        for (int i = 0; i < delays.Count; i++)
        {
            t -= delays[i];
            if (t < 0) return i;
        }
        return delays.Count - 1;
    }

    // The standing doll walks 1-2-3-2-1, not 1-2-3-1: the return leg plays
    // the middle frames backwards. One and two frames have nothing to mirror.
    public static int PingPongAt(IReadOnlyList<int> delays, float timeMs)
    {
        int count = delays.Count;
        if (count <= 2) return At(delays, timeMs);
        int forward = 0;
        foreach (int delay in delays) forward += delay;
        int back = forward - delays[0] - delays[count - 1];
        int t = (int)(timeMs % (forward + back));
        if (t < forward) return At(delays, t);
        t -= forward;
        for (int i = count - 2; i >= 1; i--)
        {
            t -= delays[i];
            if (t < 0) return i;
        }
        return 1;
    }

    // A character doll's frame: stand1 uses PingPongAt, every other action At.
    public static int Frame(string action, IReadOnlyList<int> delays, float timeMs) =>
        action == "stand1" ? PingPongAt(delays, timeMs) : At(delays, timeMs);
}

// One ground drop: a meso pile (Amount) or a potion (Id + Count).
public sealed class DropArt : Component
{
    public float X;
    public float Y;
    public int Kind;
    public int Id;
    public int Count;
    public long Amount;
}
