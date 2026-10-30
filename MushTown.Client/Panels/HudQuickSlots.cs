using Microsoft.Xna.Framework.Graphics;
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
            cell.Add(new QuickCell(i, SlotCaps[i], () => IconOn(key), LoadArt));
            root.AddChild(cell);
        }
        return root;
    }

    // The art of the target the table binds to that key, "" when the cell is
    // empty or the action has no official icon (Settings, the sub-windows).
    private string IconOn(Keys key) => _input.TargetOf(key) is { } target ? TargetArt(target) : "";

    // Art on demand: the strip binds live, so the icon of a new target may not
    // be in the cache yet. The component cannot add to it, the game can.
    private Texture2D LoadArt(string name)
    {
        LoadTexture(name);
        return TexByKey(name);
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
