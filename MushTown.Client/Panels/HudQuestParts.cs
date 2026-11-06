using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// One quest row: the official rounded selection bar, also used as the hover
// fill. The WZ has no separate row mouseOver art (only the group bars do);
// drawing the selected bar instead of a square keeps both states rounded.
public sealed class QuestRowArt : Component, IDrawable
{
    private readonly Func<bool> _selected;
    private readonly int _selX, _selY;
    public QuestRowArt(Func<bool> selected, int selX, int selY)
    { _selected = selected; _selX = selX; _selY = selY; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        if (!_selected() && gfx.Hovered != Owner) return;
        var art = gfx.Tex("ui/quest_row_sel.png");
        gfx.Sb.Draw(art, new Vector2(r.X + _selX, r.Y + _selY), Color.White);
    }
}

// One quest row's name and level. The name is cut to the row; the level is
// right aligned. Colors answer to the live state (ready, done).
public sealed class QuestRowText : Component, IDrawable
{
    private readonly Func<(string Name, string Level, Color Ink)> _text;
    private readonly int _nameX, _textPad, _levelRight, _levelDy;
    public QuestRowText(Func<(string Name, string Level, Color Ink)> text, int nameX, int textPad, int levelRight, int levelDy)
    { _text = text; _nameX = nameX; _textPad = textPad; _levelRight = levelRight; _levelDy = levelDy; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        var (name, level, ink) = _text();
        var t = Text.Fit(gfx, name, r.Width - _nameX - _textPad);
        Text.Draw(gfx.Sb, gfx.Ui, t, new Rectangle(r.X + _nameX, r.Y, r.Width - _nameX - _textPad, r.Height), ink, TextLine.Middle);
        var lv = gfx.Ui.GetSmall(level);
        gfx.Sb.Draw(lv, new Vector2(r.Right - _levelRight - lv.Width, r.Y + _levelDy), new Color(120, 130, 145));
    }
}

// The animated NPC of the quest info panel: the same LifeStand the map NPC
// and the world map tip carry. The feet stay on (x, y); every frame's own
// origin moves the top left.
public sealed class QuestNpcPortrait : Component, IDrawable
{
    private readonly LifeStand _stand;
    private readonly int _x, _y;
    public QuestNpcPortrait(LifeStand stand, int x, int y) { _stand = stand; _x = x; _y = y; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Screen;
        var idle = _stand.Art;
        int frame = _stand.IndexAt(gfx.Time);
        var origin = idle.Origins[frame];
        gfx.Sb.Draw(gfx.Tex(idle.Images[frame]), new Vector2(r.X + _x - origin.X, r.Y + _y - origin.Y), Color.White);
    }
}

// One reward icon and its count text. Dy lifts art whose png has empty rows
// under the visible part (the official EXP label has 11 such rows).
public sealed record QuestRewardIcon(string Image, string Text, int Dy = 0);

// Everything the right panel shows for the selected quest.
public sealed record QuestInfoData(
    string Name, string Level, int NpcId,
    List<string> InfoLines, List<(string Text, bool Done)> Objectives,
    string Footer, List<QuestRewardIcon> Rewards);

// The body of the quest info panel: the Info and Objective bars, the wrapped
// text, and the reward box. It reads the live detail every frame (kill counts
// change under an open panel), so selecting or earning never rebuilds it.
public sealed class QuestInfoBody : Component, IDrawable
{
    private static readonly Color InfoInk = new(0xAB, 0xB5, 0xBB);
    private static readonly Color FooterInk = new(0x8E, 0xB2, 0xBE);
    private static readonly Color DoneInk = new(0x66, 0xFF, 0x66);
    private readonly Func<QuestInfoData?> _data;
    private readonly PanelLayout _lay;
    public QuestInfoBody(Func<QuestInfoData?> data, PanelLayout lay) { _data = data; _lay = lay; }

    public void Draw(UiGfx gfx)
    {
        var d = _data();
        if (d == null) return;
        var r = Owner.Get<Rect>()!.Screen;
        int lineH = _lay.Get("lineH", 16);

        int x = r.X + _lay.Get("infoBarX", 16);
        int y = r.Y + _lay.Get("infoBarY", 128);
        gfx.Sb.Draw(gfx.Tex("ui/quest_sec_info.png"), new Vector2(x, y), Color.White);
        int tx = r.X + _lay.Get("infoTextX", 24);
        y = r.Y + _lay.Get("infoTextY", 152);
        int max = _lay.Get("infoMaxLines", 8);
        int lines = Math.Min(d.InfoLines.Count, max);
        for (int i = 0; i < lines; i++)
        {
            gfx.Sb.Draw(gfx.Ui.GetSmall(d.InfoLines[i]), new Vector2(tx, y), InfoInk);
            y += lineH;
        }

        // The Objective bar follows the text; objBarY is the lowest it may go.
        y = Math.Min(r.Y + _lay.Get("objBarY", 302), y + _lay.Get("secGap", 14));
        gfx.Sb.Draw(gfx.Tex("ui/quest_sec_obj.png"), new Vector2(x, y), Color.White);
        y += _lay.Get("secBarH", 22) + _lay.Get("secBarGap", 2);
        max = _lay.Get("objMaxLines", 4);
        int drawn = 0;
        int textW = _lay.Get("infoTextW", 250);
        foreach (var (text, done) in d.Objectives)
        {
            if (drawn++ >= max) break;
            gfx.Sb.Draw(Text.Fit(gfx, text, textW), new Vector2(tx, y), done ? DoneInk : InfoInk);
            y += lineH;
        }
        if (d.Footer != "" && drawn < max)
            gfx.Sb.Draw(Text.Fit(gfx, d.Footer, textW), new Vector2(tx, y), FooterInk);

        if (d.Rewards.Count == 0) return;
        int bx = r.X + _lay.Get("boxX", 16), by = r.Y + _lay.Get("boxY", 415);
        var box = gfx.Tex("ui/quest_box.png");
        gfx.Sb.Draw(box, new Vector2(bx, by), Color.White);
        int centerY = by + box.Height / 2; // one line through the box: label, icons and counts share it
        var label = gfx.Tex("ui/quest_reward.png");
        gfx.Sb.Draw(label, new Vector2(r.X + _lay.Get("rewardLabelX", 30), centerY - label.Height / 2), Color.White);
        int ix = r.X + _lay.Get("rewardX", 65);
        int gap = _lay.Get("rewardGap", 12);
        int textGap = _lay.Get("rewardTextGap", 4);
        int right = bx + box.Width;
        foreach (var reward in d.Rewards)
        {
            var icon = gfx.Tex(reward.Image);
            var t = gfx.Ui.GetSmall(reward.Text);
            if (ix + icon.Width + textGap + t.Width > right) break; // the box is one row: stop at its right edge
            gfx.Sb.Draw(icon, new Vector2(ix, centerY - icon.Height / 2 + reward.Dy), Color.White);
            gfx.Sb.Draw(t, new Vector2(ix + icon.Width + textGap, centerY - t.Height / 2), Color.White);
            ix += icon.Width + textGap + t.Width + gap;
        }
    }
}
