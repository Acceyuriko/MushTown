using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The twelve item quickslots: the empty cells of the official bottom strip to
// the right of the four key hints. One pick from the bag lands on a cell, the
// number row then drinks it. The bindings live in the save (CharaSlot.Qs).
public partial class MushTownGame
{
    private const int QuickSlotCount = 12;

    // The keys under the cells, in the order of the strip: 1-9, 0, - and =.
    private static readonly Keys[] QuickSlotBinds =
    {
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6,
        Keys.D7, Keys.D8, Keys.D9, Keys.D0, Keys.OemMinus, Keys.OemPlus,
    };

    private static readonly string[] QuickSlotCaps = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };

    private readonly bool[] _qsWasDown = new bool[QuickSlotCount];

    private GameObject BuildQuickSlots()
    {
        var root = new GameObject();
        for (int k = 0; k < QuickSlotCount; k++)
        {
            int i = k; // the loop variable is shared: copy it for the lambdas
            var s = QslotCell(4 + i); // the four key hints take the first four cells
            var cell = new GameObject();
            cell.Add(new Rect(s));
            cell.Add(new Button { LegacyId = $"qs{i}", Kind = ButtonKind.Click, OnClick = () => ClickQuickSlot(i) });
            cell.Add(new Invoke(gfx => DrawQuickSlot(gfx, i, s)));
            root.AddChild(cell);
        }
        return root;
    }

    // The picked item on the cell, the key under it.
    private void DrawQuickSlot(UiGfx gfx, int k, Rectangle s)
    {
        int id = QuickSlotItem(k);
        if (id != 0)
        {
            var icon = gfx.Tex(_items[id].Image);
            gfx.Sb.Draw(icon, new Rectangle(s.X + 5, s.Y + 2, 24, 24), Color.White);
        }
        var cap = gfx.Ui.GetSmall(QuickSlotCaps[k]);
        var at = new Vector2(s.Right - cap.Width - 1, s.Bottom - cap.Height);
        gfx.Sb.Draw(cap, at + new Vector2(1, 1), Color.Black); // a shadow keeps the cap readable on an icon
        gfx.Sb.Draw(cap, at, new Color(200, 200, 200));
    }

    // A click binds the held potion, or drinks what the cell already has.
    private void ClickQuickSlot(int k)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        if (_hand.Empty) { DrinkQuickSlot(k); return; }
        if (_hand.Kind != "use") return; // only potions can be drunk by a key
        chara.Qs ??= new List<int>();
        while (chara.Qs.Count < QuickSlotCount) chara.Qs.Add(0);
        chara.Qs[k] = _hand.Id;
        _hand.Return(chara);
        PlaySfx("click");
    }

    private void DrinkQuickSlot(int k)
    {
        var chara = _save?.Slots[_slot];
        int id = QuickSlotItem(k);
        if (chara == null || id == 0) return;
        var item = _items[id];
        Progression.UsePotion(chara, id, item.Hp, item.Mp, OwnHpPct());
    }

    private void QuickSlotKeyEdges(KeyboardState kb)
    {
        for (int k = 0; k < QuickSlotCount; k++)
            if (FreshKey(kb, QuickSlotBinds[k], ref _qsWasDown[k])) DrinkQuickSlot(k);
    }

    // The item bound to one cell, 0 when the cell is empty.
    private int QuickSlotItem(int k)
    {
        var qs = _save?.Slots[_slot]?.Qs;
        return qs != null && k < qs.Count ? qs[k] : 0;
    }
}
