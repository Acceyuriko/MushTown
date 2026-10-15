using System.Text.Json;
using static MushTown.Shared.LayoutJson;

namespace MushTown.Shared;

// layouts/charselect-layout.json: character select geometry in 1366x768
// space. Missing keys keep their current values, so the file may list
// only what you tweak.
public static class CharLayout
{
    public static int[] SlotTopX = { 306, 457, 609, 761, 913, 1064 };
    public static int[] SlotBotX = { 258, 413, 567, 722, 877, 1032 };
    public static int SlotTopY = 348; // top shelf feet line
    public static int SlotBotY = 580; // bottom shelf feet line
    public static int SlotW = 120; // click rect size (feet-anchored)
    public static int SlotH = 170;
    public static int TagUp = 140; // name tag floats this high above the feet
    public static (int X, int Y) Pillar = (-66, -164); // totem offset from feet (WZ origin 66,164)
    public static (int X, int Y) Pill = (1135, 33); // server pill top-left
    public static (int X, int Y) Board = (1135, 100); // slots board top-left
    public static (int X, int Y) BoardCount = (172, 9); // count text center offset inside the board
    public static (int X, int Y) Pager = (589, 640); // pager bar top-left (black glass centers on the white bar)
    public static int PagerGlass = 110; // black backing alpha 0-255 (the blur itself is a WZ shader we do not run)
    public static (int X, int Y) PageL = (553, 640); // left arrow top-left (art-sized, sits in the black bar end)
    public static (int X, int Y) PageR = (787, 640); // right arrow top-left
    public static (int X, int Y) New = (598, 680); // create button top-left (art-sized)
    public static (int X, int Y) Delete = (418, 684); // delete button top-left (art-sized)
    public static (int X, int Y) Select = (1150, 608); // start button top-left
    public static (int X, int Y) Back = (30, 700); // back button top-left
    public static (int X, int Y) DelPanel = (483, 284); // delete box top-left
    public static (int X, int Y) DelSize = (400, 200); // delete box size
    public static int DelTextY = 320; // "Delete {name}?" line
    public static (int X, int Y) DelYes = (493, 410);
    public static (int X, int Y) DelNo = (693, 410);
    public static int ErrorY = 732;

    public static void Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        // All or nothing: a bad value throws before any field changes.
        var slotTopX = GetInts(root, "slotTopX", SlotTopX);
        var slotBotX = GetInts(root, "slotBotX", SlotBotX);
        var slotTopY = GetInt(root, "slotTopY", SlotTopY);
        var slotBotY = GetInt(root, "slotBotY", SlotBotY);
        var slotW = GetInt(root, "slotW", SlotW);
        var slotH = GetInt(root, "slotH", SlotH);
        var tagUp = GetInt(root, "tagUp", TagUp);
        var pillar = GetPoint(root, "pillar", Pillar);
        var pill = GetPoint(root, "pill", Pill);
        var board = GetPoint(root, "board", Board);
        var boardCount = GetPoint(root, "boardCount", BoardCount);
        var pager = GetPoint(root, "pager", Pager);
        var pagerGlass = GetInt(root, "pagerGlass", PagerGlass);
        var pageL = GetPoint(root, "pageL", PageL);
        var pageR = GetPoint(root, "pageR", PageR);
        var @new = GetPoint(root, "new", New);
        var delete = GetPoint(root, "delete", Delete);
        var select = GetPoint(root, "select", Select);
        var back = GetPoint(root, "back", Back);
        var delPanel = GetPoint(root, "delPanel", DelPanel);
        var delSize = GetPoint(root, "delSize", DelSize);
        var delTextY = GetInt(root, "delTextY", DelTextY);
        var delYes = GetPoint(root, "delYes", DelYes);
        var delNo = GetPoint(root, "delNo", DelNo);
        var errorY = GetInt(root, "errorY", ErrorY);
        SlotTopX = slotTopX; SlotBotX = slotBotX; SlotTopY = slotTopY; SlotBotY = slotBotY;
        SlotW = slotW; SlotH = slotH; TagUp = tagUp; Pillar = pillar; Pill = pill; Board = board;
        BoardCount = boardCount; Pager = pager; PagerGlass = pagerGlass; PageL = pageL; PageR = pageR;
        New = @new; Delete = delete;
        Select = select; Back = back; DelPanel = delPanel; DelSize = delSize;
        DelTextY = delTextY; DelYes = delYes; DelNo = delNo; ErrorY = errorY;
    }
}
