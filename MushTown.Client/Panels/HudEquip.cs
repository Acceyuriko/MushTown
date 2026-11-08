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

    // The layout keys of every box on each page. A new box adds one row here
    // and its numbers in panel-equip.json.
    private static readonly Dictionary<string, (string X, string Y)> EquipBoxes = new()
    {
        ["weapon"] = ("weaponX", "weaponY"),
        ["top"] = ("topX", "topY"),
        ["bottom"] = ("bottomX", "bottomY"),
    };

    private static readonly Dictionary<string, (string X, string Y)> DecoBoxes = new()
    {
        ["weapon"] = ("decoWeaponX", "decoWeaponY"),
        ["top"] = ("topX", "topY"),
        ["bottom"] = ("bottomX", "bottomY"),
        ["hair"] = ("hairX", "hairY"),
        ["face"] = ("faceX", "faceY"),
    };

    private enum EquipPage { Equipment, Pet, Outfits, Android, DamageSkins }

    private EquipPage _page = EquipPage.Equipment;

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
        AddWorn(root);
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
            "ui/equip_close.png", () => Show = false));
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

    // The worn items: the Equipment page shows the gear, the Outfits page the
    // cash items. Pet / Android / Damage Skins stay empty.
    private void AddWorn(GameObject root)
    {
        if (_page is not (EquipPage.Equipment or EquipPage.Outfits)) return;
        if (_game.Hero is not { } chara) return;
        var items = _game.Items;
        var slots = IsDeco ? EquipSlots.Cash(chara, items) : EquipSlots.Worn(chara, items);
        foreach (var (slot, id) in slots)
        {
            var box = SlotBox(slot);
            var data = items[id];
            var o = new GameObject();
            o.Add(new Rect(box));
            o.Add(new ItemTip(() => id));
            if ((slot is "hair" or "face") && _game.HasLookBust(data.Cosmetic))
            {
                int cosmetic = data.Cosmetic;
                o.Add(new LookBust((gfx, rect) => _game.DrawLookBust(gfx, rect, cosmetic)));
            }
            else
            {
                _game.LoadTexture(data.Image);
                o.Add(new InvItem(() => data.Image));
            }
            root.AddChild(o);
        }
    }

    // The box of one window slot: the page's table names its layout keys.
    private Rectangle SlotBox(string slot)
    {
        var (x, y) = (IsDeco ? DecoBoxes : EquipBoxes)[slot];
        int size = _lay.Get("slotSize", 42);
        int px = _lay.Get(x, -1), py = _lay.Get(y, -1);
        if (px < 0 || py < 0) // a missing key would draw the item at the screen corner
            throw new InvalidDataException($"panel-equip.json is missing {x}/{y} for slot {slot}");
        return new Rectangle(px, py, size, size);
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
        _page = page;
        Dirty = true; // the frame, tabs and canvas change together
    }

    private void LoadArt()
    {
        foreach (string f in new[]
        {
            "equip_bg.png", "deco_bg.png", "equip_panel.png", "equip_pet.png",
            "deco_panel.png", "deco_android.png", "deco_damage.png",
            "equip_deco.png", "deco_equip.png", "equip_close.png",
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

// A hair or face coupon in its slot: the doll's upper body, cropped by the
// slot box (the game draws it, so the item tip shows the same bust).
public sealed class LookBust : Component, IDrawable
{
    private readonly Action<UiGfx, Rectangle> _draw;
    public LookBust(Action<UiGfx, Rectangle> draw) => _draw = draw;
    public void Draw(UiGfx gfx) => _draw(gfx, Owner.Get<Rect>()!.Screen);
}
