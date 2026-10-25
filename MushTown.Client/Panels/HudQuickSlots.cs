using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The twelve item quickslots: the empty cells of the official bottom strip to
// the right of the four key hints. One pick from the bag lands on a cell, the
// number row then drinks it. The bindings live in the save (CharaSlot.QuickSlots).
public partial class MushTownGame
{
    // The keys of the cells, in the order of the strip: 1-9, 0, - and =.
    // The cap text sits in the corner of each cell.
    private static readonly (Keys Key, string Cap)[] QuickSlotKeys =
    {
        (Keys.D1, "1"), (Keys.D2, "2"), (Keys.D3, "3"), (Keys.D4, "4"), (Keys.D5, "5"), (Keys.D6, "6"),
        (Keys.D7, "7"), (Keys.D8, "8"), (Keys.D9, "9"), (Keys.D0, "0"), (Keys.OemMinus, "-"), (Keys.OemPlus, "="),
    };

    private const int QuickSlotCount = 12;
    private const int CapPad = 1;    // the cap keeps off the cell border
    private const int CapShadow = 1; // the dark offset that keeps the cap readable on an icon

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
            // Silent: the click sound comes from ClickQuickSlot, and drinking is quiet.
            cell.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => ClickQuickSlot(i) });
            cell.Add(new ItemTip(() => QuickSlotItem(i))); // the same tooltip as a bag slot
            cell.Add(new Invoke(gfx => DrawQuickSlot(gfx, i, s)));
            root.AddChild(cell);
        }
        return root;
    }

    // The picked item on the cell at its own size, the key in the corner.
    private void DrawQuickSlot(UiGfx gfx, int k, Rectangle s)
    {
        int id = QuickSlotItem(k);
        if (id != 0)
        {
            var icon = gfx.Tex(_items[id].Image);
            gfx.Sb.Draw(icon, new Vector2(s.X + (s.Width - icon.Width) / 2, s.Y + (s.Height - icon.Height) / 2), Color.White);
        }
        var cap = gfx.Ui.GetSmall(QuickSlotKeys[k].Cap);
        var at = new Vector2(s.Right - cap.Width - CapPad, s.Bottom - cap.Height);
        gfx.Sb.Draw(cap, at + new Vector2(CapShadow, CapShadow), Color.Black);
        gfx.Sb.Draw(cap, at, new Color(200, 200, 200));
    }

    // A click binds the held potion, or drinks what the cell already has.
    private void ClickQuickSlot(int k)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        if (_hand.Empty) { DrinkQuickSlot(k); return; }
        if (_hand.Kind != "use") return; // only potions can be drunk by a key
        chara.QuickSlots ??= new List<int>();
        while (chara.QuickSlots.Count < QuickSlotCount) chara.QuickSlots.Add(0);
        chara.QuickSlots[k] = _hand.Id;
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

    // The edges stay fresh every frame; the drink waits for a screen that can take it.
    private void QuickSlotKeyEdges(KeyboardState kb, bool canDrink)
    {
        for (int k = 0; k < QuickSlotCount; k++)
            if (FreshKey(kb, QuickSlotKeys[k].Key, ref _qsWasDown[k]) && canDrink) DrinkQuickSlot(k);
    }

    // The item bound to one cell, 0 when the cell is empty.
    private int QuickSlotItem(int k)
    {
        var qs = _save?.Slots[_slot]?.QuickSlots;
        return qs != null && k < qs.Count ? qs[k] : 0;
    }

    // True for the keys of the strip: the key window must not bind them to
    // anything else, or one press would both act and drink.
    internal static bool QuickSlotKey(string name) => QuickSlotKeys.Any(q => q.Key.ToString() == name);
}
