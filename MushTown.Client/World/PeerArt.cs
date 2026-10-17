using MushTown.Shared;

namespace MushTown.Client;

// Peer pose + name plates, one per live peer (moved to tree children in
// S7; the hero's plate rides the player object). Plates stay sibling
// components, not child objects: the tree has no transforms, so the
// anchor math lives in Draw. No Update overrides: peers snap on net
// events, plates fill on Name events, the hero plate syncs once per
// frame (live-derived from worn gear). The game removes the Owner
// alongside the dict.
public sealed class PeerArt : Component
{
    public int Id;
    public float X;
    public float Y;
    public byte Action;
    public byte Frame;
    public bool FacingRight;
}

// One name plate: a peer's Name event, or the hero's synced tag. HasName
// gates the draw (a peer shows no plate until its Name arrives, even if
// a foreign client sends an empty one).
public sealed class PlateArt : Component
{
    public bool HasName;
    public string Name = "";
    public int Plate;
}
