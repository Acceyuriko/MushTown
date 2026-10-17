using System.Text.Json;
using static MushTown.Shared.LayoutJson;

namespace MushTown.Shared;

// layouts/jobselect-layout.json: job (class select) geometry in 1366x768
// space. Positions are the WZ ones (Login.img/ClassSelect/desc/pos) except
// the list offset, the panel padding, and the pager, which the client sets.
// The class art is not here: each class carries its own WZ origin.
// Missing keys keep their current values, so the file may list only what you
// tweak.
public static class JobLayout
{
    public static (int X, int Y) List = (1040, 57); // card list panel top-left (57 leaves room for the pager above Create)
    public static int ListGlass = 110; // dark backing alpha 0-255 (the WZ glass blur is not run)
    public static int ListPad = 8; // backing overhang: the WZ card grid sits flush with its art
    public static int PageGap = 4; // gap between the list backing and the pager bar
    public static (int X, int Y) Name = (48, 44); // big class name art (desc/pos className)
    public static (int X, int Y) Mark = (50, 169); // branch mark art (desc/pos jobMark)
    public static (int X, int Y) Sub = (48, 193); // sub name line (desc/pos subNameLT)
    public static (int X, int Y) Desc = (50, 233); // first description line (desc/pos descLT)
    public static int DescLine = 14; // description line advance (WZ descWH 84 for 6 lines)
    public static int CardPad = 14; // info card padding past the text
    public static (int X, int Y) Stat = (64, 356); // first stats row (desc/pos race)
    public static int StatValueX = 174; // value column x
    public static int StatLine = 32; // stats row advance (desc/pos queue: 356, 388, 420)
    public static (int X, int Y) Make = (1095, 701); // Create button top-left (centers the 200-wide button under the narrow list)
    public static (int X, int Y) Back = (0, 720); // Back button top-left (flush left, like chars)
    public static int ErrorY = 40; // centered error line

    public static void Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        // All or nothing: a bad value throws before any field changes.
        var list = GetPoint(root, "list", List);
        var listGlass = GetInt(root, "listGlass", ListGlass);
        var listPad = GetInt(root, "listPad", ListPad);
        var pageGap = GetInt(root, "pageGap", PageGap);
        var name = GetPoint(root, "name", Name);
        var mark = GetPoint(root, "mark", Mark);
        var sub = GetPoint(root, "sub", Sub);
        var desc = GetPoint(root, "desc", Desc);
        var descLine = GetInt(root, "descLine", DescLine);
        var cardPad = GetInt(root, "cardPad", CardPad);
        var stat = GetPoint(root, "stat", Stat);
        var statValueX = GetInt(root, "statValueX", StatValueX);
        var statLine = GetInt(root, "statLine", StatLine);
        var make = GetPoint(root, "make", Make);
        var back = GetPoint(root, "back", Back);
        var errorY = GetInt(root, "errorY", ErrorY);
        List = list; ListGlass = listGlass; ListPad = listPad; PageGap = pageGap; Name = name; Mark = mark;
        Sub = sub; Desc = desc; DescLine = descLine; CardPad = cardPad; Stat = stat;
        StatValueX = statValueX; StatLine = statLine; Make = make; Back = back; ErrorY = errorY;
    }
}
