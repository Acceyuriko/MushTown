using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The tooltip of a piece of gear (official art, UIToolTip.img/Item/Equip). The left frame shows the
// item; when the item belongs to a set, a second frame on its right shows
// the set. The bag and the equipment window both reach it through Tips.
// Every number is in layouts/panel-itemtip.json.
public sealed class EquipTip : Component
{
    // The tooltip text colors (UIToolTip.img/Item/Equip/font/common).
    private static readonly Color White = Color.White;
    private static readonly Color Gray = new(183, 191, 197);
    private static readonly Color Dark = new(133, 145, 159);
    private static readonly Color Emphasis = new(255, 204, 0);
    private static readonly Color Red = new(205, 12, 12); // the job bar's unwearable jobs: the color of the reference picture (the WZ font "red" is orange)

    private const int StarsPerBlock = 5;

    private readonly Func<IReadOnlyCollection<int>?> _worn;
    private readonly IReadOnlyDictionary<int, SetData> _sets;
    private readonly PanelLayout _lay;

    public EquipTip(Func<IReadOnlyCollection<int>?> worn, IReadOnlyDictionary<int, SetData> sets, PanelLayout lay)
    {
        _worn = worn;
        _sets = sets;
        _lay = lay;
    }

    public void Draw(UiGfx gfx, TipAnchor anchor, ItemData item)
    {
        var set = SetRules.Of(item, _sets);
        int leftW = gfx.Tex("ui/etip_top.png").Width;
        int setW = set == null ? 0 : gfx.Tex("ui/etip_set_top.png").Width;
        int gap = set == null ? 0 : _lay.Get("setGap", 4);

        var stats = new List<string>();
        if (item.Category != "") stats.Add("Equipment Category: " + item.Category);
        if (item.AttackSpeed != 0) stats.Add("Attack Speed: " + StatTables.AttackSpeedStage(item.AttackSpeed));
        stats.AddRange(ItemLines.Bonuses(item));
        var desc = item.Desc.Length == 0 ? new List<string>() : gfx.Ui.Wrap(item.Desc, _lay.Get("descW", 290));
        int lineGap = _lay.Get("lineGap", 16);
        int leftH = _lay.Get("textY", 216) + stats.Count * lineGap
            + (desc.Count > 0 ? _lay.Get("descGap", 8) + desc.Count * lineGap : 0)
            + _lay.Get("portraitH", 96) + gfx.Tex("ui/etip_btm.png").Height;
        int h = Math.Max(leftH, set == null ? 0 : SetHeight(gfx, set)); // the two frames are as tall as the taller one

        var box = anchor.At(gfx, leftW + gap + setW, h);
        DrawLeft(gfx, box.X, box.Y, leftW, h, item, stats, desc);
        if (set != null) DrawSet(gfx, box.X + leftW + gap, box.Y, h, set);
    }

    private void DrawLeft(UiGfx gfx, int x, int y, int w, int h, ItemData item, List<string> stats, List<string> desc)
    {
        Frame(gfx, "etip", x, y, h);
        int margin = _lay.Get("marginX", 15);

        // 25 empty stars: three blocks of five, then two (the star force is not a system here)
        var star = gfx.Tex("ui/etip_star_empty.png");
        int block = StarsPerBlock * star.Width, blockGap = _lay.Get("starBlockGap", 10);
        int rowGap = _lay.Get("starRowGap", 8);
        for (int row = 0; row < 2; row++)
        {
            int blocks = row == 0 ? 3 : 2;
            int sx = x + (w - (blocks * block + (blocks - 1) * blockGap)) / 2;
            int sy = y + _lay.Get("starsY", 10) + row * (star.Height + rowGap);
            for (int b = 0; b < blocks; b++)
                for (int s = 0; s < StarsPerBlock; s++)
                    gfx.Sb.Draw(star, new Vector2(sx + b * (block + blockGap) + s * star.Width, sy), Color.White);
        }

        Centered(gfx, item.Name, x, w, y + _lay.Get("nameY", 48), White, _lay.Get("nameFont", 14));
        Line(gfx, "etip", x, y + _lay.Get("line1Y", 72));

        // the icon box, the item icon enlarged inside it, then the box shade
        int ix = x + _lay.Get("iconX", 15), iy = y + _lay.Get("iconY", 82);
        var box = gfx.Tex("ui/etip_icon_base.png");
        gfx.Sb.Draw(box, new Vector2(ix, iy), Color.White);
        var art = gfx.Tex(item.Image);
        int scale = _lay.Get("iconScale", 2);
        gfx.Sb.Draw(art, new Rectangle(ix + (box.Width - art.Width * scale) / 2, iy + (box.Height - art.Height * scale) / 2, art.Width * scale, art.Height * scale), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/etip_icon_shade.png"), new Vector2(ix, iy), Color.White);

        // the requirements next to the icon
        int rx = x + _lay.Get("reqX", 111), rx2 = rx + _lay.Get("reqCol2X", 96);
        Text(gfx, $"REQ LV : {item.ReqLevel}", rx, y + _lay.Get("reqLvY", 92), Emphasis);
        int r1 = y + _lay.Get("reqRow1Y", 114), r2 = y + _lay.Get("reqRow2Y", 132);
        Text(gfx, $"REQ STR : {item.ReqStr:000}", rx, r1, Gray);
        Text(gfx, $"REQ DEX : {item.ReqDex:000}", rx2, r1, Gray);
        Text(gfx, $"REQ LUK : {item.ReqLuk:000}", rx, r2, Gray);
        Text(gfx, $"REQ INT : {item.ReqInt:000}", rx2, r2, Gray);

        // the job bar: five names, the ones that can wear the item lit
        int by = y + _lay.Get("jobY", 176);
        int bw = w - margin * 2;
        JobBar(gfx, new Rectangle(x + margin, by, bw, gfx.Tex("ui/etip_cat_c.png").Height));
        var names = ItemLines.JobBar.Select(gfx.Ui.GetSmall).ToList(); // the free width is shared evenly between the names
        int free = bw - names.Sum(t => t.Width), used = 0;
        for (int i = 0; i < names.Count; i++)
        {
            gfx.Sb.Draw(names[i], new Vector2(x + margin + used + free * (i + 1) / (names.Count + 1), by + _lay.Get("jobTextY", 1)), ItemLines.JobLit(item, i) ? White : Red);
            used += names[i].Width;
        }
        Line(gfx, "etip", x, y + _lay.Get("line2Y", 206));

        int ty = y + _lay.Get("textY", 216), lineGap = _lay.Get("lineGap", 16);
        foreach (string text in stats)
        {
            Text(gfx, text, x + margin, ty, White);
            ty += lineGap;
        }
        if (desc.Count > 0) ty += _lay.Get("descGap", 8);
        foreach (string text in desc)
        {
            Text(gfx, text, x + margin, ty, Gray);
            ty += lineGap;
        }
        // the blank area under the text is kept for the portrait
    }

    private int SetHeight(UiGfx gfx, SetData set)
    {
        int lineGap = _lay.Get("lineGap", 16);
        int h = _lay.Get("setPartsY", 48) + set.Parts.Count * lineGap + _lay.Get("setLineGap", 8);
        foreach (var tier in set.Tiers)
            h += (1 + tier.Effects.Count) * lineGap + _lay.Get("setTierGap", 8);
        return h + _lay.Get("setPadBottom", 10) + gfx.Tex("ui/etip_set_btm.png").Height;
    }

    private void DrawSet(UiGfx gfx, int x, int y, int h, SetData set)
    {
        Frame(gfx, "etip_set", x, y, h);
        int w = gfx.Tex("ui/etip_set_top.png").Width, margin = _lay.Get("marginX", 15), lineGap = _lay.Get("lineGap", 16);
        Centered(gfx, set.Name, x, w, y + _lay.Get("setNameY", 12), Emphasis, _lay.Get("nameFont", 14));
        Line(gfx, "etip_set", x, y + _lay.Get("setLine1Y", 38));

        var worn = _worn();
        int ty = y + _lay.Get("setPartsY", 48);
        foreach (var part in set.Parts)
        {
            Text(gfx, $"{part.Name} ({part.Label})", x + margin, ty, SetRules.PartWorn(part, worn) ? White : Dark);
            ty += lineGap;
        }
        ty += _lay.Get("setLineGap", 8);
        foreach (var tier in set.Tiers)
        {
            bool lit = SetRules.TierActive(set, tier, worn);
            Text(gfx, $"{tier.Count} Set Effect", x + margin, ty, lit ? Emphasis : Dark);
            ty += lineGap;
            foreach (var effect in tier.Effects)
            {
                Text(gfx, SetRules.Line(effect), x + margin, ty, lit ? White : Dark);
                ty += lineGap;
            }
            ty += _lay.Get("setTierGap", 8);
        }
    }

    // The frame art: a top, a stretched middle, a bottom.
    private static void Frame(UiGfx gfx, string stem, int x, int y, int h)
    {
        var top = gfx.Tex($"ui/{stem}_top.png");
        var mid = gfx.Tex($"ui/{stem}_mid.png");
        var btm = gfx.Tex($"ui/{stem}_btm.png");
        gfx.Sb.Draw(top, new Vector2(x, y), Color.White);
        gfx.Sb.Draw(mid, new Rectangle(x, y + top.Height, top.Width, h - top.Height - btm.Height), Color.White);
        gfx.Sb.Draw(btm, new Vector2(x, y + h - btm.Height), Color.White);
    }

    private static void Line(UiGfx gfx, string stem, int x, int y) =>
        gfx.Sb.Draw(gfx.Tex($"ui/{stem}_line.png"), new Vector2(x, y), Color.White);

    // The job bar art: a left end, a repeated middle, a right end.
    private static void JobBar(UiGfx gfx, Rectangle r)
    {
        var w = gfx.Tex("ui/etip_cat_w.png");
        var e = gfx.Tex("ui/etip_cat_e.png");
        gfx.Sb.Draw(w, new Vector2(r.X, r.Y), Color.White);
        gfx.Sb.Draw(gfx.Tex("ui/etip_cat_c.png"), new Rectangle(r.X + w.Width, r.Y, r.Width - w.Width - e.Width, r.Height), Color.White);
        gfx.Sb.Draw(e, new Vector2(r.Right - e.Width, r.Y), Color.White);
    }

    private static void Text(UiGfx gfx, string text, int x, int y, Color color) =>
        gfx.Sb.Draw(gfx.Ui.GetSmall(text), new Vector2(x, y), color);

    // px 0 is the small tip font, the others a sized font.
    private static void Centered(UiGfx gfx, string text, int x, int w, int y, Color color, int px)
    {
        var tex = px == 0 ? gfx.Ui.GetSmall(text) : gfx.Ui.GetSized(text, px);
        gfx.Sb.Draw(tex, new Vector2(x + (w - tex.Width) / 2, y), color);
    }
}
