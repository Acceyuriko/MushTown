using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The last layer of the HUD: the text tip of the hovered widget and the item
// tooltip of the hovered cell. Both read the hover the scene resolved this
// frame. A held piece covers the cursor, so its tooltip waits.
public sealed class Tips : Component, IDrawable
{
    private readonly Func<bool> _handEmpty;
    private readonly Dictionary<int, ItemData> _items;
    private readonly PanelLayout _invLay;  // the tooltip box: its offset, edge and wrap width
    private readonly PanelLayout _statLay; // panel-stat.json: the only source of the detail offsets (the stat window's box reads the same keys)

    public Tips(Func<bool> handEmpty, Dictionary<int, ItemData> items, PanelLayout invLay, PanelLayout statLay)
    {
        _handEmpty = handEmpty;
        _items = items;
        _invLay = invLay;
        _statLay = statLay;
    }

    public void Draw(UiGfx gfx)
    {
        if (gfx.Hovered?.Get<HoverTip>() is HoverTip text) text.DrawAt(gfx, gfx.Mouse); // Scene.Draw fills gfx.Hovered first
        if (_handEmpty() && gfx.Hovered?.Get<ItemTip>() is ItemTip item) DrawItem(gfx, item.Id());
    }

    // The item tooltip: the icon, the name, the bonuses and the description of
    // the cell whose ItemTip is hovered. It reuses the official box and the
    // detail pane of the stat window, sized to the text.
    private void DrawItem(UiGfx gfx, int itemId)
    {
        if (itemId == 0) return; // an empty cell has nothing to show
        int gx = gfx.Mouse.X, gy = gfx.Mouse.Y;
        var item = _items[itemId];
        var name = gfx.Ui.GetSmall(item.Name);
        var lines = new List<Texture2D>();
        foreach (string text in ItemLines.Detail(item)) lines.Add(gfx.Ui.GetSmall(text));
        // The description String.wz carries (potions have one, gear has none).
        var desc = new List<Texture2D>();
        if (item.Desc.Length != 0)
            foreach (string text in gfx.Ui.Wrap(item.Desc, _invLay.Get("tipTextW", 180))) desc.Add(gfx.Ui.GetSmall(text));

        int iconX = _statLay.Get("detailIconX", 8), iconY = _statLay.Get("detailIconY", 8);
        int iconSize = _statLay.Get("detailIconSize", 32);
        int nameX = _statLay.Get("detailNameX", 48), nameY = _statLay.Get("detailNameY", 6);
        int lineX = _statLay.Get("detailLineX", 48), lineY = _statLay.Get("detailLineY", 28);
        int lineGap = _statLay.Get("detailLineGap", 16);
        int textW = name.Width;
        foreach (var line in lines) textW = Math.Max(textW, line.Width);
        foreach (var line in desc) textW = Math.Max(textW, line.Width);
        int w = nameX + textW + iconX;
        int rows = lines.Count + desc.Count; // the blank row between the two blocks
        if (lines.Count > 0 && desc.Count > 0) rows++;
        int h = Math.Max(lineY + rows * lineGap, iconY + iconSize) + iconX;
        int edge = _invLay.Get("tipEdge", 4); // keeps the box off the screen edge
        var box = new Rectangle(Math.Min(gx + _invLay.Get("tipDX", 18), MushTownGame.GameWidth - w - edge),
            Math.Min(gy + _invLay.Get("tipDY", 12), MushTownGame.GameHeight - h - edge), w, h);

        NineSlice.Draw(gfx, "tip", box);
        var icon = gfx.Tex(item.Image); // native size, like every other item icon
        gfx.Sb.Draw(icon, new Vector2(box.X + iconX + (iconSize - icon.Width) / 2, box.Y + iconY + (iconSize - icon.Height) / 2), Color.White);
        gfx.Sb.Draw(name, new Vector2(box.X + nameX, box.Y + nameY), new Color(255, 220, 0));
        int y = box.Y + lineY;
        foreach (var line in lines)
        {
            gfx.Sb.Draw(line, new Vector2(box.X + lineX, y), Color.White);
            y += lineGap;
        }
        if (lines.Count > 0) y += lineGap; // the gap before the description
        foreach (var line in desc)
        {
            gfx.Sb.Draw(line, new Vector2(box.X + lineX, y), new Color(214, 216, 222));
            y += lineGap;
        }
    }
}
