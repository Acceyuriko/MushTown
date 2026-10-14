namespace MushTown.Shared;

// Exported map file: assets/maps/{id}.json
public sealed class MapData
{
    public int MapId { get; set; }
    public MapInfo Info { get; set; } = new();
    public List<BackItem> Backs { get; set; } = new();
    public List<TileItem> Tiles { get; set; } = new();
    public List<ObjItem> Objs { get; set; } = new();
    public List<FootholdItem> Footholds { get; set; } = new();
    public List<PortalItem> Portals { get; set; } = new();
    public List<LadderItem> Ladders { get; set; } = new();
    public List<LifeItem> Life { get; set; } = new();
    public List<NpcSpawn> Npcs { get; set; } = new();
    public MiniMapData MiniMap { get; set; } = new();
}

public sealed class MapInfo
{
    // Camera clamp rect.
    public int VrLeft { get; set; }
    public int VrTop { get; set; }
    public int VrRight { get; set; }
    public int VrBottom { get; set; }

    // Where the player appears. SpawnY is the ground under the spawn portal.
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }

    // Raw WZ info/bgm ("Bgm58/The Beginnig of The Adventure"): the client
    // turns it into a file key with SoundKey.FromBgm.
    public string Bgm { get; set; } = "";
}

// WZ miniMap node: the canvas plus the world rect it covers. The rect's
// top-left is (-CenterX, -CenterY) (the names lie), size Width x Height.
// Same mapping as WzComparerR2's minimap renderer (UIMiniMap.cs).
public sealed class MiniMapData
{
    public string Image { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int CenterX { get; set; }
    public int CenterY { get; set; }

    // World point to a dot inside a drawn box of drawW x drawH.
    public (int X, int Y) ToDot(float wx, float wy, int drawW, int drawH) =>
        ((int)((wx + CenterX) / Width * drawW), (int)((wy + CenterY) / Height * drawH));
}

public sealed class BackItem
{
    // Draw position of the image top-left corner: the WZ origin is already taken off.
    // It is a parallax anchor, not a world position (see Type).
    public int X { get; set; }
    public int Y { get; set; }
    public string Image { get; set; } = "";

    // True when the background is drawn in front of the map and characters.
    public bool Front { get; set; }

    // Mirrored horizontally (WZ "f").
    public bool Flip { get; set; }

    // WZ "type": 0 none, 1 tile horizontal, 2 tile vertical, 3 both,
    // 4/6 = horizontal axis drifts with time, 5/7 = vertical axis does.
    public int Type { get; set; }

    // Tile step in pixels (WZ "cx"/"cy"); 0 means "the image's own size".
    public int Cx { get; set; }
    public int Cy { get; set; }

    // Parallax rate in percent (WZ "rx"/"ry"): 0 = pinned to the screen, -100 = pinned to the map.
    public int Rx { get; set; }
    public int Ry { get; set; }
}

public sealed class TileItem
{
    // Draw position of the image top-left corner.
    public int X { get; set; }
    public int Y { get; set; }
    public string Image { get; set; } = "";

    // Map layer 0-7 (WZ layer group). Layers are drawn in order, 0 first.
    public int Layer { get; set; }

    // Image layer inside the map layer (WZ "z"); bigger Z is drawn later.
    public int Z { get; set; }
}

// One map object: same Layer/Z rules as TileItem.
public sealed class ObjItem
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Image { get; set; } = "";

    // Mirrored horizontally (WZ "f").
    public bool Flip { get; set; }

    public int Layer { get; set; }
    public int Z { get; set; }
}

// One ground line segment. Layer groups footholds (higher layer = drawn in front).
public sealed class FootholdItem
{
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public int Layer { get; set; }
}

// Portal number rules shared by exporter proof and client use (the two must
// agree, so they live here, not as magic numbers at each site).
public static class PortalRules
{
    public const int NoTarget = 999999999; // tm with no target (spawns, one-way entrances)
    public const int ArrivalLookUp = 10; // arrival search starts this far above the portal point
}

// One portal: ↑ walks into (Tm, Tn), unless Tm is NoTarget (a dead end).
// Live portals also carry art frames; dead ones have no Frames.
public sealed class PortalItem
{
    public string Pn { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Tm { get; set; }
    public string Tn { get; set; } = "";
    public List<PortalFrame> Frames { get; set; } = new();
}

public sealed class PortalFrame
{
    public string Image { get; set; } = "";
    public int Dx { get; set; }
    public int Dy { get; set; }
    public int Delay { get; set; }
}

// One climbable ladder or rope: a vertical bar from Y1 to Y2 at X.
// Ladder is true for ladders (climb pose), false for ropes (hang pose).
public sealed class LadderItem
{
    public int X { get; set; }
    public int Y1 { get; set; }
    public int Y2 { get; set; }
    public bool Ladder { get; set; }
}

// One mob spawn point: the WZ life node with type "m" (NPCs live in MapData.Npcs).
// X/Cy is the feet point (the simulator places life at x,cy: FrmMapRender.cs:1612).
public sealed class LifeItem
{
    public int MobId { get; set; }
    public int X { get; set; }
    public int Cy { get; set; }
}

// One NPC placed on a map (life type "n", hide=1 skipped at export).
public sealed class NpcSpawn
{
    public int NpcId { get; set; }
    public int X { get; set; }
    public int Cy { get; set; }
    public bool FacingRight { get; set; }
}

// Exported mob file: assets/mob/{id}.json. Stats are v1 feel numbers, not WZ:
// the modern client keeps them server-side (no maxHP node anywhere in the data).
public sealed class MobData
{
    public int MobId { get; set; }
    public string Name { get; set; } = "";
    public int MaxHp { get; set; }
    public double Def { get; set; } // fraction of damage blocked, 0-1 (bwiki 防御; v1: nothing ignores it)
    public int Exp { get; set; } // kill award (v1 feel number, like MaxHp: the modern client keeps it server-side)
    public int Touch { get; set; } // contact damage (flat: mobs roll no variance in v1)
    public int Speed { get; set; } // walk px/s (wander and chase share it)
    public int Aggro { get; set; } // chase range in x px (0 = never chases)
    public bool Boss { get; set; } // v1 boss (Mushmom only): long grave, fat purse, wide touch, HP bar
    public List<MobAction> Actions { get; set; } = new();
}

// Drop kinds on the DropSpawn wire shape (mesos pay an amount, items an id+count).
public static class DropKind
{
    public const int Mesos = 0;
    public const int Item = 1;
}

public sealed class MobAction
{
    public string Name { get; set; } = ""; // stand, move, hit1, die1
    public List<MobFrame> Frames { get; set; } = new();
}

public sealed class MobFrame
{
    public string Image { get; set; } = "";

    // Frame time in ms. The WZ art carries no delays, so every frame is 100.
    public int Delay { get; set; }
}

// Item def: assets/item/{id}.json. Potions heal (Hp/Mp from the WZ spec
// node); equips wear (Slot + bonuses from the WZ info node). v1 gear rules:
// a worn weapon's Atk replaces WeaponAtk 32, Def is defense points 1:1,
// Str/Dex add; level/job reqs ignored (official Lv10+ fits no v1 char).
// Icons are the WZ info/icon canvas.
public sealed class ItemData
{
    public const int RedPotion = 2000000; // +50 HP (spec hp)
    public const int BluePotion = 2000003; // +100 MP (spec mp)
    public const int Mop = 1442004; // Janitor's Mop (Wp, PAD 47): Mushmom purse
    public const int Sergeant = 1040039; // Red Sergeant (Ma, DEX 1/PDD 25): orange rare

    // The potion ids v1 knows: the client loads and slots them, quests may
    // grant them. One list on purpose: a grantable potion the client never
    // loaded would miss its art.
    public static readonly int[] PotionIds = new[] { RedPotion, BluePotion };

    // The equip ids v1 knows: the client loads defs/icons/art, bags and
    // wears them, mobs drop them. (1040038 skipped: its official name is
    // unfit; cap-class gear skipped: hats re-hang the hair.)
    public static readonly int[] EquipIds = new[] { Mop, Sergeant };

    // Every loadable item def + icon (potions, then equips).
    public static readonly int[] AllIds = PotionIds.Concat(EquipIds).ToArray();

    // Beginner img per worn slot: the doll merge draws these when the slot
    // sits empty (only slots with v1 equips need entries).
    public static readonly Dictionary<string, string> BeginnerImgs = new()
    {
        ["Wp"] = "01442000",
        ["Ma"] = "01040036",
    };

    // Worn bonuses: weapon PAD (0 when none worn: the caller keeps the
    // beginner 32), summed STR/DEX/DEF. Unknown ids grant nothing (a
    // hand-made save may list what v1 never shipped).
    public static (int weapon, int str, int dex, int def) EquipBonus(List<int>? worn, IReadOnlyDictionary<int, ItemData> defs)
    {
        int weapon = 0, str = 0, dex = 0, def = 0;
        if (worn == null) return (weapon, str, dex, def);
        foreach (int id in worn)
        {
            if (!defs.TryGetValue(id, out var d) || d.Slot == null) continue;
            if (d.Slot == "Wp") weapon = Math.Max(weapon, d.Atk);
            str += d.Str;
            dex += d.Dex;
            def += d.Def;
        }
        return (weapon, str, dex, def);
    }

    public static string Name(int id) => id switch
    {
        RedPotion => "Red Potion",
        BluePotion => "Blue Potion",
        Mop => "Janitor's Mop",
        Sergeant => "Red Sergeant",
        _ => $"item {id}",
    };

    public int ItemId { get; set; }
    public string Image { get; set; } = "";
    public int Hp { get; set; }
    public int Mp { get; set; }
    public int Price { get; set; } // WZ info/price: what the shop charges (sell pays half, min 1)
    public string? Slot { get; set; } // equip slot (WZ islot); null = consumable
    public int Atk { get; set; } // WZ incPAD
    public int Def { get; set; } // WZ incPDD
    public int Str { get; set; } // WZ incSTR
    public int Dex { get; set; } // WZ incDEX
}

// Basic damage skin digits: assets/effect/dmg.json (BasicEff.img NoRed0,
// the set that also carries Miss/guard words, so it is the combat font).
// Digits[d] is the art for digit d; Oy aligns baselines at draw time.
public sealed class DmgData
{
    public List<DmgDigit> Digits { get; set; } = new();
}

public sealed class DmgDigit
{
    public string Image { get; set; } = "";
    public int Oy { get; set; }
}
// Exported character action: assets/chara/body/{action}.json (beginner full
// merge) or assets/chara/gear/{imgId}/{action}.json (one gear's parts).
public sealed class CharaData
{
    public List<CharaFrame> Frames { get; set; } = new();
}

public sealed class CharaFrame
{
    public int Delay { get; set; }
    public List<CharaPart> Parts { get; set; } = new();
}

public sealed class CharaPart
{
    public string Image { get; set; } = "";

    // Source img id (00002000, 01040036, ...): the doll merge keeps base
    // parts and swaps each worn slot's beginner img for the worn gear.
    public string Img { get; set; } = "";

    // Image top-left corner in character space. Anchor (0,0) is between the feet.
    public int X { get; set; }
    public int Y { get; set; }

    // Draw order from zmap.img; smaller draws later (in front). List is sorted back-to-front.
    public int Z { get; set; }
}
