using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The last layer of the HUD: the tip of the hovered widget (a line of text, or
// the NPC and monster tip the world map rows carry) and the item tooltip of the
// hovered cell. All read the hover the scene resolved this frame. A piece on the
// cursor covers it, so the item tooltip waits.
public sealed class Tips : Component, IDrawable
{
    private readonly MushTownGame _game;
    private readonly Func<bool> _cursorFree;
    private readonly Dictionary<int, ItemData> _items;
    private readonly PanelLayout _invLay;  // panel-inventory.json: the item tooltip's wrap width
    private readonly PanelLayout _statLay; // panel-stat.json: the item tooltip's detail offsets and bust size

    public Tips(MushTownGame game, Func<bool> cursorFree, Dictionary<int, ItemData> items, PanelLayout invLay, PanelLayout statLay)
    {
        _game = game;
        _cursorFree = cursorFree;
        _items = items;
        _invLay = invLay;
        _statLay = statLay;
    }

    public void Draw(UiGfx gfx)
    {
        var anchor = Owner.Get<TipAnchor>()!; // the tip layer carries it: every tip lands the same way
        if (gfx.Hovered?.Get<LifeTip>() is LifeTip life) life.DrawAt(gfx, anchor);
        if (gfx.Hovered?.Get<HoverTip>() is HoverTip text) text.DrawAt(gfx, anchor); // Scene.Draw fills gfx.Hovered first
        if (_cursorFree() && gfx.Hovered?.Get<ItemTip>() is ItemTip item) DrawItem(gfx, anchor, item.Id());
    }

    // The item tooltip: the icon, the name, the bonuses and the description of
    // the cell whose ItemTip is hovered. It reuses the official box and the
    // detail pane of the stat window, sized to the text.
    private void DrawItem(UiGfx gfx, TipAnchor anchor, int itemId)
    {
        if (itemId == 0 || !_items.TryGetValue(itemId, out var item)) return; // an empty or unknown cell has nothing to show
        var name = gfx.Ui.GetSmall(item.Name);
        var lines = new List<Texture2D>();
        foreach (string text in ItemLines.Detail(item)) lines.Add(gfx.Ui.GetSmall(text));
        // The description String.wz carries (potions, cash coupons and the cash rings have one).
        var desc = new List<Texture2D>();
        if (item.Desc.Length != 0)
            foreach (string text in gfx.Ui.Wrap(item.Desc, _invLay.Get("tipTextW", 180))) desc.Add(gfx.Ui.GetSmall(text));

        int iconX = _statLay.Get("detailIconX", 8), iconY = _statLay.Get("detailIconY", 8);
        // a hair or face coupon shows the doll's bust instead of the coupon art
        int iconSize = _game.HasLookBust(item.Cosmetic) ? _statLay.Get("detailBustSize", 48) : _statLay.Get("detailIconSize", 32);
        // the text starts after the icon whatever its size (the bust is wider than a plain icon)
        int textX = iconX + iconSize + _statLay.Get("detailIconGap", 8);
        int nameX = Math.Max(_statLay.Get("detailNameX", 48), textX), nameY = _statLay.Get("detailNameY", 6);
        int lineX = Math.Max(_statLay.Get("detailLineX", 48), textX), lineY = _statLay.Get("detailLineY", 28);
        int lineGap = _statLay.Get("detailLineGap", 16);
        int textW = name.Width;
        foreach (var line in lines) textW = Math.Max(textW, line.Width);
        foreach (var line in desc) textW = Math.Max(textW, line.Width);
        int w = nameX + textW + iconX;
        int rows = lines.Count + desc.Count; // the blank row between the two blocks
        if (lines.Count > 0 && desc.Count > 0) rows++;
        int h = Math.Max(lineY + rows * lineGap, iconY + iconSize) + iconX;
        var box = anchor.At(gfx, w, h);

        NineSlice.Draw(gfx, "tip", box);
        var icon = new Rectangle(box.X + iconX, box.Y + iconY, iconSize, iconSize);
        if (!_game.DrawLookBust(gfx, icon, item.Cosmetic))
        {
            var art = gfx.Tex(item.Image); // native size, like every other item icon
            gfx.Sb.Draw(art, new Vector2(icon.X + (iconSize - art.Width) / 2, icon.Y + (iconSize - art.Height) / 2), Color.White);
        }
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
