using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The sixteen fixed cells of the official bottom strip. A cell draws the official
// cap of its key and the target the table binds to that key; nothing bound, only
// the cap. A key with an Item target drinks its potion. A cell is a drop area
// for the click-style drag: a click picks its target up, a click while holding
// binds the held thing to its key.
public partial class MushTownGame
{
    // The key of each cell, in strip order.
    private static readonly Keys[] SlotKeys =
    {
        Keys.LeftControl, Keys.LeftAlt, Keys.I, Keys.C,
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6,
        Keys.D7, Keys.D8, Keys.D9, Keys.D0, Keys.OemMinus, Keys.OemPlus,
    };

    // The cell of each slot, in strip order: a drop asks whether the point is
    // on the cell and not on a window dragged over the strip.
    private readonly GameObject[] _cells = new GameObject[SlotKeys.Length];

    // The name and the cap art of each key, from the key window's table
    // (keys.json). Built once: the strip draws sixteen cells every frame and
    // must not build a string per cell.
    private static string[]? _slotNames;
    private static string[] SlotNames =>
        _slotNames ??= SlotKeys.Select(k => k.ToString()).ToArray();
    private static string[]? _slotCaps;
    private static string[] SlotCaps =>
        _slotCaps ??= SlotNames.Select(KeyConfigWindow.CapArt).ToArray();

    // The key window while it is open: the strip draws its draft and a drop on a
    // cell is a drop on that key.
    private KeyConfigWindow? DraftWindow => KeyWindow is { Show: true } w ? w : null;

    private GameObject BuildQuickSlots()
    {
        var root = new GameObject();
        for (int i = 0; i < SlotKeys.Length; i++)
        {
            int slot = i; // its own copy: the closures below outlive the loop and a for loop shares one variable
            var cell = new GameObject();
            _cells[slot] = cell;
            cell.Add(new Rect(QslotCell(slot)));
            cell.Add(new ItemTip(() => ItemOn(slot))); // the tooltip of the bound item
            cell.Add(new QuickCell(SlotCaps[slot], () => IconOn(slot), LoadArt));
            cell.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => PickSlot(slot) }); // the drop is taken before the click chain; PickSlot plays the click
            cell.Add(new Grab(() => Pickable(slot)));
            root.AddChild(cell);
        }
        return root;
    }

    // What a cell shows and picks: the key window's draft while it is open (a
    // drop on a cell is the same as on its key), the live table otherwise.
    // The draft is the only truth while open: clearing an item there must
    // clear the cell too, before Save.
    private BindTarget? SlotTarget(int cell)
    {
        var keyWin = DraftWindow;
        return keyWin == null ? _input.TargetOf(SlotKeys[cell]) : keyWin.DraftOn(SlotNames[cell]);
    }

    // A click on a cell with something to pick up: the action, skill or item
    // on it comes up and rides the mouse.
    private void PickSlot(int cell)
    {
        if (!_hand.Empty || SlotTarget(cell) is not { } target) return;
        Hold(target, true); // remember the cell: a world click clears its binding
        PlaySfx("click"); // the cell's button is silent: only a cell that gave something up clicks
    }

    // A quick slot target is in hand and the click hit no UI at all: the
    // binding is gone (the quick slot's own "drop on the floor"). An NPC click
    // still talks: the piece goes back instead.
    private bool CancelSlotHoldOnWorldClick(int gx, int gy, GameObject? top)
    {
        if (!_heldFromSlot || _uiScene.ModalOpen || top != null || NpcAt(gx, gy) != null) return false;
        var target = Held!.Value;
        if (DraftWindow is { } win) win.ApplyDrop("", target); // the pool clears the whole target, combo or not
        else if (Hero is { } chara)
        {
            KeyBindings.Ensure(chara);
            KeyBindings.ClearTarget(chara.KeyPresets![chara.ActivePreset], target);
            _keysDirty = true;
            SaveChara();
        }
        else return false; // no character: nothing to save, the click goes on
        ClearHeld();
        PlaySfx("click");
        return true;
    }

    // The cell holds something a click can pick up.
    private bool Pickable(int cell) => SlotTarget(cell) != null;

    // A click while a key target is in hand: use the shared drop check.
    private bool DropHeld(int gx, int gy)
    {
        if (!Holding || !DropTarget(gx, gy, Held!.Value)) return false;
        ClearHeld();
        return true;
    }

    // A click while an inventory item is in hand: a potion lands on a key or
    // a quick slot exactly like a key target does.
    private bool DropBagHeld(int gx, int gy)
    {
        if (_hand.Empty || _hand.Kind != "use") return false;
        if (!DropTarget(gx, gy, new BindTarget(BindKind.Item, _hand.Id))) return false;
        _hand.Return();
        return true;
    }

    // The point is a drop area (a key of the key window, a quick slot cell,
    // the pool). False when it is not, so the caller puts the piece back and
    // the click runs as usual.
    private bool DropTarget(int gx, int gy, BindTarget target)
    {
        var keyWin = DraftWindow;
        if (keyWin != null && keyWin.TryDropAt(gx, gy, target)) return true; // the keyboard or the pool
        if (!QslotRect().Contains(gx, gy)) return false; // not near the strip: no need to hit test
        if (_uiScene.TopAt(gx, gy) is not { } top) return false;
        int cell = Array.IndexOf(_cells, top); // -1: not a strip cell
        if (cell < 0) return false; // a window covers the strip: the click is its
        if (keyWin != null)
        {
            keyWin.ApplyDrop(SlotNames[cell], target); // a cell is the same key as its cap on the keyboard
            return true;
        }
        return BindSlotLive(SlotKeys[cell], target); // no draft open: the cell binds the live preset
    }

    // The drop landed on a cell with the key window closed: the key binds now.
    // False with no character: the table is read only, so the caller puts the
    // piece back and the click picks that cell instead.
    private bool BindSlotLive(Keys key, BindTarget target)
    {
        if (Hero == null) return false;
        BindLive(key, target);
        return true;
    }

    // The art the cell draws, "" while the thing on it is in hand: the key, the
    // pool cell and the strip cell go blank until the drop lands.
    private string IconOn(int cell) =>
        SlotTarget(cell) is { } target && Held != target ? TargetArt(target) : "";

    // Art on demand: the strip binds live, so the icon of a new target may not
    // be in the cache yet. The component cannot add to it, the game can.
    private Texture2D LoadArt(string name)
    {
        LoadTexture(name);
        return TexByKey(name);
    }

    // The art of a bound target: the item picture, the skill icon, or the
    // official icon of the action.
    internal string TargetArt(BindTarget target) => target.Kind switch
    {
        BindKind.Item => _items.TryGetValue(target.Id, out var item) ? item.Image : "",
        BindKind.Skill => SkillIconTex(target.Id),
        _ => KeyConfigWindow.IconTex((HotkeyAction)target.Id),
    };

    // The item on a cell, 0 when the cell holds no known item: the tooltip
    // follows the icon, so a draft action on an item key shows no tooltip.
    private int ItemOn(int cell) =>
        SlotTarget(cell) is { Kind: BindKind.Item } target && _items.ContainsKey(target.Id) ? target.Id : 0;

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
        if (chara == null || !_hand.Empty) return; // the bag hand owns the next click
        if (!_items.TryGetValue(id, out var item)) return;
        Progression.UsePotion(chara, id, item.Hp, item.Mp, MaxHpNow(), MaxMpNow());
    }
}
