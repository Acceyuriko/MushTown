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
}

public sealed class MapInfo
{
    // Camera clamp rect.
    public int VrLeft { get; set; }
    public int VrTop { get; set; }
    public int VrRight { get; set; }
    public int VrBottom { get; set; }
    public int Town { get; set; }

    // Where the player appears. SpawnY is the ground under the spawn portal.
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
}

public sealed class BackItem
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Image { get; set; } = "";

    // True when the background is drawn in front of the map and characters.
    public bool Front { get; set; }
}

public sealed class TileItem
{
    // Draw position of the image top-left corner.
    public int X { get; set; }
    public int Y { get; set; }
    public string Image { get; set; } = "";
}

public sealed class ObjItem
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Image { get; set; } = "";
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

// Exported character action: assets/chara/{body}/{action}.json
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

    // Image top-left corner in character space. Anchor (0,0) is between the feet.
    public int X { get; set; }
    public int Y { get; set; }

    // Draw order from zmap.img; smaller draws later (in front). List is sorted back-to-front.
    public int Z { get; set; }
}
