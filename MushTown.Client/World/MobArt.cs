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
    public int Hp;
    public int MaxHp;
    public string Action { get; private set; } = ""; // stand, move, hit1, die1
    public List<MobFrame> Frames { get; private set; } = null!; // the frames of the current action: SetAction fills it
    public int Frame;
    public float Timer;

    // The idle is stand, or move when the mob has no stand (the blue snail
    // crawls everywhere).
    public string Idle => Data.Actions.Any(a => a.Name == "stand") ? "stand" : "move";

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

    // The WZ art carries no delays, so every frame is 100ms. The last frame
    // loops: a flinch goes back to the idle, the idles start over. Only the
    // death ends the life: Frame lands on Frames.Count there, so a corpse
    // must not be ticked or drawn again — CollectDead drops it first.
    public override void Update(float dt)
    {
        Timer += dt * 1000f;
        if (Timer < Frames[Frame].Delay) return;
        Timer -= Frames[Frame].Delay; // keep the overflow (single step: delays run >= 100ms, ticks 16ms)
        if (++Frame < Frames.Count) return;
        if (Action == "die1") { Owner.Dead = true; return; }
        if (Action == "hit1") { Action = Idle; Frames = FramesOf(Idle); }
        Frame = 0;
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

// One ground drop: a meso pile (Id = amount) or a potion (Id + Count).
public sealed class DropArt : Component
{
    public float X;
    public float Y;
    public int Kind;
    public int Id;
    public int Count;
}
