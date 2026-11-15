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
    // Spawn from the look table: the look may have arrived before the State.
    public PeerArt(int id, IReadOnlyDictionary<int, AppearanceEvent> looks)
    {
        Id = id;
        if (looks.TryGetValue(id, out var look)) SetAppearance(look);
    }

    public int Id;
    public float X;
    public float Y;
    public byte Action;
    public byte Frame;
    public bool FacingRight;
    public int Skin;
    public int Hair = ItemData.DefaultHair;
    public int Face = ItemData.DefaultFace;
    public List<int>? CashWorn;
    public List<int>? Equips;

    // A look can arrive before the first State: the table entry is applied to
    // this component as soon as either arrives.
    public void SetAppearance(AppearanceEvent a)
    {
        Skin = Math.Clamp(a.Skin, 0, CharaSlot.SkinCount - 1);
        Hair = a.Hair;
        Face = a.Face;
        CashWorn = a.CashWorn.ToList();
        Equips = a.Equips.ToList();
    }
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
