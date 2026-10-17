using MushTown.Shared;

namespace MushTown.Client;

// Mob and drop art state, one per live view (moved to tree children in
// S6). No Update overrides on purpose: the mob anim tick stays at its
// exact spot in Update (it runs after the frame's casts but before the
// drains; riding TreeUpdate would move that sampling point), drops never
// tick, and both still draw through the y-sorted list (the tree never
// y-sorts; S8 verdict: the list stays). The game removes the Owner
// alongside the dict.
public sealed class MobArt : Component
{
    public int Mob;
    public float X;
    public float Y;
    public bool FacingRight; // AI poses face along (raw art faces left)
    public int Hp;
    public int MaxHp;
    public string Action = ""; // idle, hit1, die1
    public int Frame;
    public float Timer;
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
