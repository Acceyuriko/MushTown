using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The equipment window (UIEquip.img): the Equipment/Pet pages share one
// 366x444 frame, the Outfits/Android/Damage Skin pages share the other.
// The Equipment page shows the worn gear, the Outfits page the worn cash
// items; the other three pages only show their empty canvases.
public sealed class EquipWindow : UiWindow
{
    private readonly MushTownGame _game;
    public EquipWindow(MushTownGame game) : base(HotkeyAction.Equip) { _game = game; }

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-equip.json"));

    // The 7x6 slot grid on both pages: column and row in slots/, plus the
    // slot-name art (one file per part name; rings share one).
    private readonly record struct SlotCell(int Col, int Row, string Art);

    // The boxes both pages share. Equipment and Outfits only add their own
    // extra boxes, so a common slot is changed in one place.
    private static readonly Dictionary<string, SlotCell> SharedBoxes = new()
    {
        ["ring1"] = new(0, 0, "ring"),
        ["face_acc"] = new(1, 0, "face_acc"),
        ["hat"] = new(5, 0, "hat"),
        ["cape"] = new(6, 0, "cape"),
        ["ring2"] = new(0, 1, "ring"),
        ["eye_acc"] = new(1, 1, "eye_acc"),
        ["top"] = new(5, 1, "top"),
        ["gloves"] = new(6, 1, "gloves"),
        ["ring3"] = new(0, 2, "ring"),
        ["earrings"] = new(1, 2, "earrings"),
        ["bottom"] = new(5, 2, "bottom"),
        ["shoes"] = new(6, 2, "shoes"),
        ["ring4"] = new(0, 3, "ring"),
    };

    // Every visible box on the Equipment page, labels included. A new slot
    // adds one row here and its label art in UIEquip.img.
    private static readonly Dictionary<string, SlotCell> EquipBoxes = new(SharedBoxes)
    {
        ["pendant1"] = new(1, 3, "pendant"),
        ["shoulder"] = new(5, 3, "shoulder"),
        ["medal"] = new(6, 3, "medal"),
        ["belt"] = new(0, 4, "belt"),
        ["pendant2"] = new(1, 4, "pendant"),
        ["weapon"] = new(2, 4, "weapon"),
        ["secondary"] = new(3, 4, "secondary"),
        ["emblem"] = new(4, 4, "emblem"),
        ["android"] = new(5, 4, "android"),
        ["heart"] = new(6, 4, "heart"),
        ["pocket"] = new(0, 5, "pocket"),
        ["badge"] = new(6, 5, "badge"),
    };

    // The Outfits page uses the same 7x6 grid. It starts from the Equipment
    // boxes, then adds its own Hair / Face / Skin boxes in the last row.
    private static readonly Dictionary<string, SlotCell> DecoBoxes = new(EquipBoxes)
    {
        ["hair"] = new(2, 5, "hair"),
        ["face"] = new(3, 5, "face"),
        ["skin"] = new(4, 5, "skin"),
    };

    private enum EquipPage { Equipment, Pet, Outfits, Android, DamageSkins }

    private EquipPage _page = EquipPage.Equipment;

    public override void OnClosed() => _game.ClearWornHand();

    private void CloseEquipWindow()
    {
        OnClosed();
        Show = false;
    }

    private bool IsDeco => _page is EquipPage.Outfits or EquipPage.Android or EquipPage.DamageSkins;

    public override Rectangle Rect() => new(0, 0, _lay.Get("w", 366), _lay.Get("h", 444));
    public override Rectangle TitleRect() => new(0, 0, Rect().Width, _lay.Get("titleH", 31));
    public override Point LayoutPos() => new(_lay.Get("x", 500), _lay.Get("y", 162));

    // The screenshot opens the decoration page directly.
    public void ShowOutfits() => SetPage(EquipPage.Outfits);

    public override void Build()
    {
        LoadArt();
        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        root.Add(new Picture(IsDeco ? "ui/deco_bg.png" : "ui/equip_bg.png",
            _lay.Get("bgX", 0), _lay.Get("bgY", 0)));
        root.Add(new Picture(PageArt(), _lay.Get("contentX", 12), _lay.Get("contentY", 61)));
        if ((_page is EquipPage.Equipment or EquipPage.Outfits) && _game.Hero != null)
        {
            var doll = new GameObject { NoHit = true };
            doll.Add(new Rect(new Rectangle(_lay.Get("dollX", 183), _lay.Get("dollY", 218), 1, 1)));
            doll.Add(new AvatarPreview(_game, "stand1", false, chara: () => _game.Hero));
            root.AddChild(doll);
        }
        var slots = SlotsForPage();
        var occupied = slots.Select(s => s.Slot).ToHashSet();
        bool bottomLocked = BottomLocked();
        if (bottomLocked) occupied.Add("bottom");
        AddSlotNames(root, occupied);
        AddWorn(root, slots);
        if (bottomLocked) AddLockedBottom(root);
        if (IsDeco)
        {
            AddTab(root, _lay.Get("decoTab0X", 12), _lay.Get("decoTabW0", 113), "deco_tab", 0, EquipPage.Outfits);
            AddTab(root, _lay.Get("decoTab1X", 127), _lay.Get("decoTabW1", 114), "deco_tab", 1, EquipPage.Android);
            AddTab(root, _lay.Get("decoTab2X", 243), _lay.Get("decoTabW2", 113), "deco_tab", 2, EquipPage.DamageSkins);
        }
        else
        {
            AddTab(root, _lay.Get("equipTab0X", 11), _lay.Get("equipTabW", 171), "equip_tab", 0, EquipPage.Equipment);
            AddTab(root, _lay.Get("equipTab1X", 184), _lay.Get("equipTabW", 171), "equip_tab", 1, EquipPage.Pet);
        }
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        if (_page == EquipPage.Equipment)
            root.AddChild(PictureButton(
                new Rectangle(_lay.Get("switchX", 251), _lay.Get("switchYEquip", 72),
                    _lay.Get("switchW", 89), _lay.Get("switchH", 22)),
                "ui/equip_deco.png", () => SetPage(EquipPage.Outfits)));
        else if (_page == EquipPage.Outfits)
            root.AddChild(PictureButton(
                new Rectangle(_lay.Get("switchX", 251), _lay.Get("switchYDeco", 74),
                    _lay.Get("switchW", 89), _lay.Get("switchH", 22)),
                "ui/deco_equip.png", () => SetPage(EquipPage.Equipment)));
        root.AddChild(PictureButton(
            new Rectangle(_lay.Get("closeX", 343), _lay.Get("closeY", 12),
                _lay.Get("closeW", 11), _lay.Get("closeH", 11)),
            "ui/equip_close.png", CloseEquipWindow));
        Host.AddChild(root);
    }

    private string PageArt() => _page switch
    {
        EquipPage.Equipment => "ui/equip_panel.png",
        EquipPage.Pet => "ui/equip_pet.png",
        EquipPage.Outfits => "ui/deco_panel.png",
        EquipPage.Android => "ui/deco_android.png",
        _ => "ui/deco_damage.png",
    };

    // One tab: the selected art while its page is open, the normal art otherwise.
    private void AddTab(GameObject root, int x, int w, string stem, int index, EquipPage page)
    {
        int y = _lay.Get("tabY", 31);
        int h = _lay.Get("tabH", 22);
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(x, y, w, h)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = () => SetPage(page) });
        o.Add(new Picture($"ui/{stem}_{(_page == page ? "s" : "n")}{index}.png", 0, 0));
        root.AddChild(o);
    }

    // Every empty box carries its part name; worn items draw over them.
    private void AddSlotNames(GameObject root, HashSet<string> occupied)
    {
        if (_page is not (EquipPage.Equipment or EquipPage.Outfits)) return;
        foreach (var (slot, cell) in (IsDeco ? DecoBoxes : EquipBoxes))
        {
            if (occupied.Contains(slot)) continue;
            _game.LoadTexture($"ui/equip_slot_{cell.Art}.png");
            var o = new GameObject { NoHit = true };
            o.Add(new Rect(SlotBox(cell)));
            o.Add(new Picture($"ui/equip_slot_{cell.Art}.png", 0, 0));
            root.AddChild(o);
        }
    }

    // The items of the open page. Pet / Android / Damage Skins show no items.
    private List<(string Slot, int Id)> SlotsForPage()
    {
        if (_page is not (EquipPage.Equipment or EquipPage.Outfits)) return new();
        if (_game.Hero is not { } chara) return new();
        return IsDeco ? EquipSlots.Cash(chara, _game.Items) : EquipSlots.Worn(chara, _game.Items);
    }

    private bool BottomLocked()
    {
        if (_page is not (EquipPage.Equipment or EquipPage.Outfits)) return false;
        if (_game.Hero is not { } chara) return false;
        return EquipSlots.BottomLocked(IsDeco ? chara.CashWorn : chara.Equips, _game.Items);
    }

    // An overall shows Top and paints Bottom red with a lock: no pants can go
    // under it.
    private void AddLockedBottom(GameObject root)
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Rect(SlotBox("bottom")));
        o.Add(new Picture("ui/equip_disabled.png", 0, 0) { Tint = new Color(255, 120, 120) });
        root.AddChild(o);
    }

    // The worn items: the Equipment page shows the gear, the Outfits page the
    // cash items. Pet / Android / Damage Skins stay empty.
    private void AddWorn(GameObject root, List<(string Slot, int Id)> slots)
    {
        var items = _game.Items;
        foreach (var (slot, id) in slots)
        {
            var box = SlotBox(slot);
            var data = items[id];
            var o = new GameObject();
            o.Add(new Rect(box));
            if (slot is not ("hair" or "face"))
            {
                o.Add(new Button { OnClick = () => ClickWorn(id) });
                o.Add(new Grab(() => true));
            }
            o.Add(new ItemTip(() => id));
            int cosmetic = _game.CouponBust(id);
            if (cosmetic != 0)
                o.Add(new LookBust(() => cosmetic, (gfx, rect, c) => _game.DrawLookBust(gfx, rect, c)));
            else
            {
                _game.LoadTexture(data.Image);
                o.Add(new InvItem(() => data.Image));
            }
            root.AddChild(o);
        }
    }

    // A click on a worn item picks it up. Dropping it on the bag takes it off.
    private void ClickWorn(int id)
    {
        _game.PickUpWorn(id, IsDeco);
    }

    // The box under a point, or null. The drop uses it to decide whether the
    // item belongs in that slot.
    internal string? SlotAt(int gx, int gy)
    {
        foreach (var (slot, cell) in EquipBoxes)
            if (OnScreen(SlotBox(cell)).Contains(gx, gy)) return slot;
        return null;
    }

    internal bool EquipmentPage => _page == EquipPage.Equipment;

    // The box of one window slot: the page's table names its grid cell.
    private Rectangle SlotBox(string slot) => SlotBox((IsDeco ? DecoBoxes : EquipBoxes)[slot]);

    private Rectangle SlotBox(SlotCell cell)
    {
        int size = _lay.Get("slotSize", 42);
        return new Rectangle(_lay.Get("slotX", 27) + cell.Col * _lay.Get("slotPitchX", 45),
            _lay.Get("slotY", 100) + cell.Row * _lay.Get("slotPitchY", 45), size, size);
    }

    private GameObject PictureButton(Rectangle r, string art, Action onClick)
    {
        var o = new GameObject();
        o.Add(new Rect(r));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Picture(art, 0, 0));
        return o;
    }

    private void SetPage(EquipPage page)
    {
        if (_page == page) return;
        _game.ClearWornHand();
        _page = page;
        Dirty = true; // the frame, tabs and canvas change together
    }

    private void LoadArt()
    {
        foreach (string f in new[]
        {
            "equip_bg.png", "deco_bg.png", "equip_panel.png", "equip_pet.png",
            "deco_panel.png", "deco_android.png", "deco_damage.png",
            "equip_deco.png", "deco_equip.png", "equip_close.png", "equip_disabled.png",
        })
            _game.LoadTexture("ui/" + f);
        for (int t = 0; t < 2; t++)
            foreach (string s in new[] { "n", "s" })
                _game.LoadTexture($"ui/equip_tab_{s}{t}.png");
        for (int t = 0; t < 3; t++)
            foreach (string s in new[] { "n", "s" })
                _game.LoadTexture($"ui/deco_tab_{s}{t}.png");
    }
}

public partial class MushTownGame
{
    private int _wornHeldId;
    private bool _wornHeldCash;

    internal bool WornHeld => _wornHeldId != 0;

    // A worn item can be picked up while neither hand holds anything. Any
    // later click is a drop or puts the piece back (HudBar handles that
    // before a button can run).
    internal void PickUpWorn(int id, bool cash)
    {
        if (!_hand.Empty || Holding) return;
        _wornHeldId = id;
        _wornHeldCash = cash;
    }

    internal void ClearWornHand()
    {
        _wornHeldId = 0;
        _wornHeldCash = false;
    }

    // The bust a coupon draws, or 0 when this build has no exported look.
    internal int CouponBust(int id) =>
        _items.TryGetValue(id, out var data) && data.Cosmetic != 0 && HasLookBust(data.Cosmetic)
            ? data.Cosmetic
            : 0;

    internal bool TakeOffWorn(int id, bool cash)
    {
        if (Hero is not { } chara) return false;
        bool changed = cash ? Progression.TakeOffCashFashion(chara, id, _items) : Progression.TakeOffEquip(chara, id);
        if (!changed) return false;
        SaveChara();
        PlaySfx("click");
        if (!cash)
        {
            PushBuild(); // gear stats changed
            PushName(); // the name plate reads the worn gear too
        }
        return true;
    }
}

// A hair or face coupon in its slot: the doll's upper body, cropped by the
// slot box (the game draws it, so the item tip shows the same bust).
public sealed class LookBust : Component, IDrawable
{
    private readonly Func<int> _cosmetic;
    private readonly Action<UiGfx, Rectangle, int> _draw;
    public LookBust(Func<int> cosmetic, Action<UiGfx, Rectangle, int> draw)
    {
        _cosmetic = cosmetic;
        _draw = draw;
    }
    public void Draw(UiGfx gfx)
    {
        int cosmetic = _cosmetic();
        if (cosmetic == 0) return;
        _draw(gfx, Owner.Get<Rect>()!.Screen, cosmetic);
    }
}
