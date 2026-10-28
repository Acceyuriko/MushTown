using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The sixteen fixed cells of the official bottom strip. A cell is a key: the
// four hints (Ctrl, Alt, I, C) then 1-9, 0, - and =. It draws the official cap
// and the target the table binds to that key; nothing bound, only the cap.
// A key with an Item target drinks its potion. The cells are display only:
// the drag that binds them comes with the slot merge.
public partial class MushTownGame
{
    // The key of each cell, in strip order.
    private static readonly Keys[] SlotKeys =
    {
        Keys.LeftControl, Keys.LeftAlt, Keys.I, Keys.C,
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6,
        Keys.D7, Keys.D8, Keys.D9, Keys.D0, Keys.OemMinus, Keys.OemPlus,
    };

    // The cap art of the cells, from the key window's table (keys.json).
    // Built once: the strip draws sixteen caps every frame.
    private static string[]? _slotCaps;
    private static string[] SlotCaps =>
        _slotCaps ??= SlotKeys.Select(k => KeyConfigWindow.CapArt(k.ToString())).ToArray();

    private const int HintCells = 4; // the four key hints take the first four cells
    private const int CapPad = 1;    // the item cap keeps off the cell border
    private const int CapShadow = 1; // the dark offset that keeps the cap readable on an icon

    private GameObject BuildQuickSlots()
    {
        var root = new GameObject();
        for (int k = 0; k < SlotKeys.Length; k++)
        {
            int i = k; // the loop variable is shared: copy it for the lambdas
            var s = QslotCell(i);
            var key = SlotKeys[i];
            var cell = new GameObject();
            cell.Add(new Rect(s));
            cell.Add(new ItemTip(() => ItemOn(key))); // the tooltip of the bound item
            cell.Add(new Invoke(gfx => DrawSlot(gfx, i, s)));
            root.AddChild(cell);
        }
        return root;
    }

    // One cell: the icon of the target the table binds to the key, then the
    // cap.
    private void DrawSlot(UiGfx gfx, int index, Rectangle s)
    {
        if (_input.TargetOf(SlotKeys[index]) is { } target)
        {
            string art = TargetArt(target);
            if (art != "") // an action with no official icon (Settings, the sub-windows) draws nothing
            {
                LoadTexture(art);
                var icon = gfx.Tex(art);
                // The hint cells take the layout offset; the item cells stay centered.
                var nudge = index < HintCells ? Layout.HintIcon : default;
                gfx.Sb.Draw(icon, new Vector2(
                    s.X + (s.Width - icon.Width) / 2 + nudge.X,
                    s.Y + (s.Height - icon.Height) / 2 + nudge.Y), Color.White);
            }
        }
        DrawCap(gfx, index, s);
    }

    // The four hints keep their cap in the top left (the layout offset); the
    // twelve item cells put it in the bottom right, like the official strip.
    private void DrawCap(UiGfx gfx, int index, Rectangle s)
    {
        string art = SlotCaps[index];
        LoadTexture(art);
        var tex = gfx.Tex(art);
        if (index < HintCells)
        {
            gfx.Sb.Draw(tex, new Vector2(s.X + Layout.HintCap.X, s.Y + Layout.HintCap.Y), Color.White);
            return;
        }
        var at = new Vector2(s.Right - tex.Width - CapPad, s.Bottom - tex.Height);
        gfx.Sb.Draw(tex, at + new Vector2(CapShadow, CapShadow), Color.Black);
        gfx.Sb.Draw(tex, at, new Color(200, 200, 200));
    }

    // The art of a bound target: the item picture, the skill icon, or the
    // official icon of the action.
    private string TargetArt(BindTarget target) => target.Kind switch
    {
        BindKind.Item => _items[target.Id].Image,
        BindKind.Skill => _skills[target.Id].Icon,
        _ => KeyConfigWindow.IconTex((HotkeyAction)target.Id),
    };

    // The item on a cell, 0 when the cell holds no item.
    private int ItemOn(Keys key)
    {
        var target = _input.TargetOf(key);
        return target is { } t && t.Kind == BindKind.Item ? t.Id : 0;
    }

    // An Item target drinks its potion on a fresh key. The edges stay fresh
    // every frame; the drink waits for a screen that can take it.
    private void ItemEdges(bool canDrink)
    {
        if (!canDrink) return;
        foreach (var action in _input.Actions)
            if (action.Target.Kind == BindKind.Item && action.Fresh) DrinkItem(action.Target.Id);
    }

    private void DrinkItem(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null) return;
        var item = _items[id];
        Progression.UsePotion(chara, id, item.Hp, item.Mp, OwnHpPct());
    }
}
