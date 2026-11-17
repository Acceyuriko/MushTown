using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// The 32 cells of the official bottom strip, two rows of 16. A cell draws the official
// cap of its key and the target the table binds to that key; nothing bound, only
// the cap. The key of each cell comes from the active preset (CharaSlot.QuickKeys). A key with an Item target drinks its potion. A cell is a drop area
// for the click-style drag: a click picks its target up, a click while holding
// binds the held thing to its key.
public partial class MushTownGame
{
    private const int QslotColumns = 16; // cells in a row of the strip

    // The cell of each slot, in strip order (row by row): a drop asks whether the
    // point is on the cell and not on a window dragged over the strip.
    private readonly GameObject[] _cells = new GameObject[KeyDefaults.QuickKeys().Count];

    // The key names of the strip: the key window's draft while it is open (the preset it
    // looks at, which may not be the live one), else the active preset's list, else the
    // defaults with no character. Every change replaces the list, so a new list is a new key set.
    private List<string> SlotNames()
    {
        if (DraftWindow is { } keyWin) return keyWin.QuickKeys;
        if (Hero is not { } chara) return DefaultQuickKeys;
        KeyBindings.Ensure(chara);
        return chara.QuickKeys![chara.ActivePreset];
    }

    private string SlotName(int cell) => SlotNames()[cell];

    private static readonly List<string> DefaultQuickKeys = KeyDefaults.QuickKeys();

    // The parsed keys of the list in use: parsed again only when the list is another one.
    private List<string>? _parsedNames;
    private Keys[] _parsedKeys = Array.Empty<Keys>();

    private Keys SlotKey(int cell)
    {
        var names = SlotNames();
        if (!ReferenceEquals(names, _parsedNames))
        {
            _parsedNames = names;
            _parsedKeys = names.Select(Enum.Parse<Keys>).ToArray();
        }
        return _parsedKeys[cell];
    }

    // The cell the key window picked to get a new key, null when no cell is picked.
    private Rectangle? PickedCell() =>
        DraftWindow is { QuickSelected: >= 0 and var cell } ? QslotCell(cell) : null;

    // The key window while it is open: the strip draws its draft and a drop on a
    // cell is a drop on that key.
    private KeyConfigWindow? DraftWindow => KeyWindow is { Show: true } w ? w : null;

    private GameObject BuildQuickSlots()
    {
        var root = new GameObject();
        for (int i = 0; i < _cells.Length; i++)
        {
            int slot = i; // its own copy: the closures below outlive the loop and a for loop shares one variable
            var cell = new GameObject();
            _cells[slot] = cell;
            cell.Add(new Rect(QslotCell(slot)));
            cell.Add(new ItemTip(() => ItemOn(slot))); // the tooltip of the bound item
            cell.Add(new QuickCell(() => SlotName(slot), () => IconOn(slot), LoadArt));
            cell.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => PickSlot(slot) }); // the drop is taken before the click chain; PickSlot plays the click
            cell.Add(new Grab(() => Pickable(slot)));
            root.AddChild(cell);
        }
        var picked = new GameObject { NoHit = true };
        picked.Add(new Rect(QslotRect()));
        picked.Add(new QuickSelect(PickedCell, LoadArt));
        root.AddChild(picked);
        return root;
    }

    // What a cell shows and picks: the key window's draft while it is open (a
    // drop on a cell is the same as on its key), the live table otherwise.
    // The draft is the only truth while open: clearing an item there must
    // clear the cell too, before Save.
    private BindTarget? SlotTarget(int cell)
    {
        var keyWin = DraftWindow;
        return keyWin == null ? _input.TargetOf(SlotKey(cell)) : keyWin.DraftOn(SlotName(cell));
    }

    // A click on a cell with something to pick up: the action, skill or item
    // on it comes up and rides the mouse.
    private void PickSlot(int cell)
    {
        if (DraftWindow is { EditingQuick: true } keyWin) // editing the strip keys: a click only picks the cell
        {
            keyWin.SelectQuick(cell);
            PlaySfx("click");
            return;
        }
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
    private bool Pickable(int cell) => DraftWindow is { EditingQuick: true } || SlotTarget(cell) != null; // editing: the cell can always be picked

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
        if (_macroWindow.TryDropAt(gx, gy, target)) return true; // a slot of a macro
        if (DropOnOwnSkillCell(gx, gy, target)) return true; // the skill window cell it came from: the grab ends
        var keyWin = DraftWindow;
        if (keyWin != null && keyWin.TryDropAt(gx, gy, target)) return true; // the keyboard or the pool
        if (!QslotRect().Contains(gx, gy)) return false; // not near the strip: no need to hit test
        if (_uiScene.TopAt(gx, gy) is not { } top) return false;
        int cell = Array.IndexOf(_cells, top); // -1: not a strip cell
        if (cell < 0) return false; // a window covers the strip: the click is its
        if (keyWin is { EditingQuick: true }) return false; // editing the strip keys: the click picks the cell instead
        if (keyWin != null)
        {
            keyWin.ApplyDrop(SlotName(cell), target); // a cell is the same key as its cap on the keyboard
            return true;
        }
        return BindSlotLive(SlotKey(cell), target); // no draft open: the cell binds the live preset
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

    // The art of a bound target: the item picture, the skill icon, the official
    // macro icon, or the official icon of the action.
    internal string TargetArt(BindTarget target) => target.Kind switch
    {
        BindKind.Item => _items.TryGetValue(target.Id, out var item) ? item.Image : "",
        BindKind.Skill => SkillIconTex(target.Id),
        BindKind.Macro => $"ui/macro_icon{target.Id}.png",
        _ => KeyConfigWindow.IconTex((HotkeyAction)target.Id),
    };

    // The item on a cell, 0 when the cell holds no known item: the tooltip
    // follows the icon, so a draft action on an item key shows no tooltip.
    private int ItemOn(int cell) =>
        SlotTarget(cell) is { Kind: BindKind.Item } target && _items.ContainsKey(target.Id) ? target.Id : 0;

    // An Item target drinks its potion while the key is held.
    private void ItemHeld(bool canDrink)
    {
        if (!canDrink) return;
        foreach (var action in _input.Actions)
            if (action.Target.Kind == BindKind.Item && action.Fire) DrinkItem(action.Target.Id);
    }

    private void DrinkItem(int id)
    {
        var chara = _save?.Slots[_slot];
        if (chara == null || !_hand.Empty) return; // the bag hand owns the next click
        if (!_items.TryGetValue(id, out var item)) return;
        Progression.UsePotion(chara, id, item.Hp, item.Mp, MaxHpNow(), MaxMpNow());
    }
}
