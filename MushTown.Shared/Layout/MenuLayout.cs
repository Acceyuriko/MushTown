using System.Text.Json;
using static MushTown.Shared.LayoutJson;

namespace MushTown.Shared;

// layouts/menu-layout.json: wooden sign board geometry in 1366x768
// space (saves + server screens). Missing keys keep their current values,
// so the file may list only what you tweak.
public static class MenuLayout
{
    public static int Slice = 48; // body end caps: keeps the corner brackets whole
    public static int OverlapTop = 44; // header sinks this deep into the body
    public static int BotUp = 126; // base rises this high above the body bottom
    public static int RowGap = 12; // gap between pill rows
    public static int BodyPadTop = 21; // cream above the first row
    public static int BodyPadBottom = 21; // cream below the last row
    public static int MiddleExtraH = 0; // extra middle height (rows stay centered)
    public static int ServerRowW = 448;
    public static int ServerRowH = 105;
    public static int TextMargin = 60; // server row text clears the rivets
    public static int TextDown = 3; // text sits this far below row center (matches baked buttons)
    public static int ListSize = 16; // server row text size in display px (not scaled)
    public static int TextGap = 24; // gap between the name and the IP (menu px, scaled)
    public static int CancelSize = 16; // Cancel row text size in display px
    public static int CancelDown = 3; // Cancel text sits this far below row center
    public static float Scale = 0.7f; // whole board shrinks toward the panel center

    public static void Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        // All or nothing: a bad value throws before any field changes.
        var slice = GetInt(root, "slice", Slice);
        var overlapTop = GetInt(root, "overlapTop", OverlapTop);
        var botUp = GetInt(root, "botUp", BotUp);
        var rowGap = GetInt(root, "rowGap", RowGap);
        var bodyPadTop = GetInt(root, "bodyPadTop", BodyPadTop);
        var bodyPadBottom = GetInt(root, "bodyPadBottom", BodyPadBottom);
        var middleExtraH = GetInt(root, "middleExtraH", MiddleExtraH);
        var serverRowW = GetInt(root, "serverRowW", ServerRowW);
        var serverRowH = GetInt(root, "serverRowH", ServerRowH);
        var textMargin = GetInt(root, "textMargin", TextMargin);
        var textDown = GetInt(root, "textDown", TextDown);
        var listSize = GetInt(root, "listSize", ListSize);
        var textGap = GetInt(root, "textGap", TextGap);
        var cancelSize = GetInt(root, "cancelSize", CancelSize);
        var cancelDown = GetInt(root, "cancelDown", CancelDown);
        var scale = GetFloat(root, "scale", Scale);
        if (listSize <= 0) throw new JsonException("bad listSize"); // GDI+ throws for 0/negative sizes
        if (cancelSize <= 0) throw new JsonException("bad cancelSize");
        Slice = slice; OverlapTop = overlapTop; BotUp = botUp; RowGap = rowGap; BodyPadTop = bodyPadTop;
        BodyPadBottom = bodyPadBottom; MiddleExtraH = middleExtraH; ServerRowW = serverRowW;
        ServerRowH = serverRowH; TextMargin = textMargin; TextDown = textDown; ListSize = listSize;
        TextGap = textGap; CancelSize = cancelSize; CancelDown = cancelDown; Scale = scale;
    }
}
