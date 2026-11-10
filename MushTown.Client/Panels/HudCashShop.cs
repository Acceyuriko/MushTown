using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MushTown.Shared;

namespace MushTown.Client;

// Which cash shop page the player is on. Beauty uses one state per preview.
public enum CashShopPage { Utility, Fashion, BeautyHair, BeautyFace }

// The full-screen cash shop window. Week 6 is the frame: top bar, search,
// the Utility/Fashion item grid, the Beauty backgrounds, and the bottom tabs.
// Preview, cart and checkout are later tickets, so their art is exported but
// not built here.
public sealed class CashShopWindow : UiWindow
{
    private readonly MushTownGame _game;
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-cashshop.json"));

    private static readonly string[] ArtFiles =
    {
        "cashshop_scene_bg.png", "cashshop_list_bg.png", "cashshop_bg_list.png", "cashshop_no_result.png",
        "cashshop_item_bg.png", "cashshop_item_bg_main.png",
        "cashshop_preview_back.png", "cashshop_preview_bottom.png", "cashshop_preview_frame.png",
        "cashshop_bg_search.png", "cashshop_bg_cash.png", "cashshop_bg_maplepoint.png", "cashshop_bg_mileage.png",
        "cashshop_exit.png", "cashshop_help_disabled.png", "cashshop_probability_disabled.png", "cashshop_coupon_disabled.png",
        "cashshop_search.png", "cashshop_menu_500.png", "cashshop_menu_600.png", "cashshop_menu_700.png",
        "cashshop_beauty_bg.png", "cashshop_beauty_list.png",
    };

    private static readonly int[] GroupIds = { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };
    private static readonly Color TextInk = new(35, 40, 48);

    private CashShopPage _page = CashShopPage.Utility;
    private int _tab = 2; // Convenience is the third Utility sub tab
    private int _selectedId;
    private string _search = "";
    private TextField? _searchField;
    private Rectangle _searchBox;
    private GameObject? _noResult;
    private readonly List<(GameObject Cell, string Name)> _cells = new();

    public CashShopWindow(MushTownGame game) : base(HotkeyAction.CashShop)
    {
        _game = game;
        Host.Modal = true; // the shop owns the input and the player leaves the map; the world keeps ticking
        Host.Enabled = false;
    }

    private int L(string key) => _lay.Get(key, 0);

    public override Rectangle Rect() => new(0, 0, L("w"), L("h"));
    public override Rectangle TitleRect() => Rectangle.Empty;
    public override Point LayoutPos() => new(L("x"), L("y"));

    public bool SearchFocused => _searchField?.Focused == true;

    // The shot pose: Fashion opens on Weapons.
    public void PoseFashion()
    {
        _page = CashShopPage.Fashion;
        _tab = 0;
        _selectedId = 0;
        Dirty = true;
    }

    // The shot pose: Fashion opens on Outfits.
    public void PoseFashionOutfits()
    {
        _page = CashShopPage.Fashion;
        _tab = 2;
        _selectedId = 0;
        Dirty = true;
    }

    // The shot pose: Utility / Convenience with a search term.
    public void PoseSearch(string text)
    {
        _page = CashShopPage.Utility;
        _tab = 2;
        _search = text;
        _selectedId = 0;
        Dirty = true;
    }

    public override void OnShown() => _game.EnterCashShop();

    public override void OnClosed()
    {
        _page = CashShopPage.Utility;
        _tab = 2;
        _selectedId = 0;
        _search = "";
        _searchField?.Unfocus();
        _searchField = null;
        Dirty = true;
        _game.LeaveCashShop();
    }

    public void Close()
    {
        Show = false;
        OnClosed();
    }

    public void UnfocusSearch() => _searchField?.Unfocus();

    public bool SearchPanelAt(Point p) => OnScreen(_searchBox).Contains(p);

    public void UpdateKeys(KeyboardState kb, float dtMs)
    {
        if (_searchField is not { Focused: true }) return;
        FieldKey key = _searchField.Read(kb, dtMs);
        if (_searchField.Text != _search)
        {
            _search = _searchField.Text;
            ApplyFilter();
        }
        if (key == FieldKey.Leave) _searchField.Unfocus();
    }

    public override void Build()
    {
        foreach (string file in ArtFiles) _game.LoadTexture("ui/" + file);
        foreach (int group in GroupIds)
        {
            _game.LoadTexture($"ui/cashshop_group_{group}.png");
            if (group is 500 or 600 or 700) _game.LoadTexture($"ui/cashshop_group_{group}_sel.png");
        }
        for (int i = 0; i < 10; i++)
        {
            _game.LoadTexture($"ui/cashshop_sub_500_{i}.png");
            _game.LoadTexture($"ui/cashshop_sub_500_{i}_sel.png");
            _game.LoadTexture($"ui/cashshop_sub_600_{i}.png");
            _game.LoadTexture($"ui/cashshop_sub_600_{i}_sel.png");
        }
        for (int i = 0; i < 4; i++)
        {
            _game.LoadTexture($"ui/cashshop_sub_700_{i}.png");
            _game.LoadTexture($"ui/cashshop_sub_700_{i}_sel.png");
        }
        Host.Clear();
        _cells.Clear();
        _searchField = null;
        _noResult = null;

        var root = new GameObject();
        root.Add(new Rect(Rect()));
        BuildPreview(root);
        if (_page is CashShopPage.BeautyHair or CashShopPage.BeautyFace)
            BuildBeauty(root);
        else
            BuildItems(root);
        BuildTopBar(root);
        BuildBottom(root);
        Host.AddChild(root);
    }

    private void BuildPreview(GameObject root)
    {
        AddPicture(root, "cashshop_scene_bg.png", "sceneBgX", "sceneBgY", "sceneBgW", "sceneBgH");
        AddPicture(root, "cashshop_preview_back.png", "previewBackX", "previewBackY", "previewBackW", "previewBackH");
        AddPicture(root, "cashshop_preview_bottom.png", "previewBottomX", "previewBottomY", "previewBottomW", "previewBottomH");
        AddPicture(root, "cashshop_preview_frame.png", "previewFrameX", "previewFrameY", "previewFrameW", "previewFrameH");
    }

    private void BuildItems(GameObject root)
    {
        AddPicture(root, "cashshop_list_bg.png", "listBgX", "listBgY", "listBgW", "listBgH");
        AddPicture(root, "cashshop_bg_list.png", "bgListX", "bgListY", "bgListW", "bgListH");

        string group = _page == CashShopPage.Utility ? "Utility" : "Fashion";
        var items = _game.CashShop.Items
            .Where(item => item.Group == group && CashShopCatalog.TabIndex(item.Group, item.Tab) == _tab)
            .ToList();

        int slots = L("gridRows") * L("gridCols");
        for (int i = 0; i < items.Count && i < slots; i++)
        {
            int col = i % L("gridCols");
            int row = i / L("gridCols");
            int x = L("listX") + col * (L("cardW") + L("gapX"));
            int y = L("listY") + row * (L("cardH") + L("gapY"));
            AddCard(root, items[i], new Rectangle(x, y, L("cardW"), L("cardH")));
        }

        _noResult = new GameObject { NoHit = true };
        _noResult.Add(new Rect(new Rectangle(L("noResultX"), L("noResultY"), L("noResultW"), L("noResultH"))));
        _noResult.Add(new Picture("ui/cashshop_no_result.png"));
        root.AddChild(_noResult);
        ApplyFilter();
    }

    private void AddCard(GameObject root, CashShopItem item, Rectangle box)
    {
        var cell = new GameObject();
        cell.Add(new Rect(box));
        cell.Add(new CardBack(() => _selectedId == item.Id));
        cell.Add(new ItemTip(() => item.Id));

        var icon = new GameObject { NoHit = true };
        icon.Add(new Rect(new Rectangle(L("iconX"), L("iconBottom") - L("iconSize"), L("iconSize"), L("iconSize"))));
        icon.Add(new InvItem(() => _game.Items[item.Id].Image, L("iconSize")));
        cell.AddChild(icon);

        var name = new GameObject { NoHit = true };
        name.Add(new Rect(new Rectangle(L("nameX"), L("nameY"), L("cardW"), L("textH"))));
        name.Add(new CenterText(() => _game.Items[item.Id].Name, TextInk, L("nameFont")));
        cell.AddChild(name);

        var price = new GameObject();
        price.Add(new Rect(new Rectangle(L("priceX"), L("priceY"), L("priceW"), L("priceH"))));
        price.Add(new ItemTip(() => item.Id));
        price.Add(new Button { Kind = ButtonKind.Click, OnClick = () => _selectedId = item.Id });
        price.Add(new CenterText(() => item.Price.ToString("#,##0"), TextInk, L("priceFont")));
        cell.AddChild(price);

        root.AddChild(cell);
        _cells.Add((cell, _game.Items[item.Id].Name));
    }

    private void BuildBeauty(GameObject root)
    {
        AddPicture(root, "cashshop_beauty_bg.png", "beautyBgX", "beautyBgY", "beautyBgW", "beautyBgH");
        AddPicture(root, "cashshop_beauty_list.png", "beautyListX", "beautyListY", "beautyListW", "beautyListH");

        if (_tab < 2)
        {
            var title = new GameObject { NoHit = true };
            title.Add(new Rect(new Rectangle(L("beautyTitleX"), L("beautyTitleY"), L("beautyTitleW"), L("beautyTitleH"))));
            title.Add(new LeftText(_tab == 1 ? "Royal Face Coupon" : "Royal Hair Coupon", TextInk, L("beautyTitleFont")));
            root.AddChild(title);
        }

        _noResult = new GameObject { NoHit = true };
        _noResult.Add(new Rect(new Rectangle(L("noResultX"), L("noResultY"), L("noResultW"), L("noResultH"))));
        _noResult.Add(new Picture("ui/cashshop_no_result.png"));
        _noResult.Enabled = false;
        root.AddChild(_noResult);
        ApplyFilter();
    }

    private void BuildTopBar(GameObject root)
    {
        AddPicture(root, "cashshop_bg_search.png", "bgSearchX", "bgSearchY", "bgSearchW", "bgSearchH");
        AddPicture(root, "cashshop_bg_cash.png", "bgCashX", "bgCashY", "bgCashW", "bgCashH");
        AddPicture(root, "cashshop_bg_maplepoint.png", "bgMaplePointX", "bgMaplePointY", "bgMaplePointW", "bgMaplePointH");
        AddPicture(root, "cashshop_bg_mileage.png", "bgMileageX", "bgMileageY", "bgMileageW", "bgMileageH");

        var field = new GameObject();
        _searchBox = new Rectangle(L("searchBoxX"), L("searchBoxY"), L("searchBoxW"), L("searchBoxH"));
        field.Add(new Rect(_searchBox));
        _searchField = new TextField(_lay) { Ink = TextInk };
        _searchField.SetText(_search);
        field.Add(_searchField);
        field.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => _searchField.Focus(Keyboard.GetState()) });
        root.AddChild(field);

        AddButton(root, "cashshop_search.png", "searchButtonX", "searchButtonY", "searchButtonW", "searchButtonH",
            () => _searchField.Focus(Keyboard.GetState()));

        AddText(root, () => $"Mesos {_game.Meso:#,##0}", "cashValueX", "cashValueY", "cashValueW", "cashValueH");
        AddText(root, () => "0", "mapleValueX", "mapleValueY", "mapleValueW", "mapleValueH");
        AddText(root, () => "0", "mileageValueX", "mileageValueY", "mileageValueW", "mileageValueH");

        AddButton(root, "cashshop_exit.png", "exitX", "exitY", "exitW", "exitH", Close);
        AddDisabled(root, "cashshop_help_disabled.png", "helpX", "helpY", "helpW", "helpH");
        AddDisabled(root, "cashshop_probability_disabled.png", "probX", "probY", "probW", "probH");
        AddDisabled(root, "cashshop_coupon_disabled.png", "couponX", "couponY", "couponW", "couponH");
    }

    private void BuildBottom(GameObject root)
    {
        AddPicture(root, $"cashshop_menu_{GroupId()}.png", "navBgX", "navBgY", "navBgW", "navBgH");

        foreach (int group in GroupIds)
        {
            bool current = group == GroupId();
            // WZ 的 menu/100/<组>/disabled 是 uol normal，和 normal 同像素；禁用分组只画图、不注册点击。
            bool live = group is 500 or 600 or 700;
            string suffix = current ? "_sel" : "";
            string xKey = current ? $"group{group}SelX"
                : group == 600 && GroupId() == 700 ? "group600XBeauty"
                : $"group{group}X";
            string yKey = current ? $"group{group}SelY" : $"group{group}Y";
            string wKey = current ? $"group{group}SelW" : $"group{group}W";
            string hKey = current ? $"group{group}SelH" : $"group{group}H";
            var tab = new GameObject();
            tab.Add(new Rect(new Rectangle(L(xKey), L(yKey), L(wKey), L(hKey))));
            if (live)
                tab.Add(new Button { Kind = ButtonKind.Click, OnClick = () => SetGroup(group) });
            tab.Add(new Picture($"ui/cashshop_group_{group}{suffix}.png"));
            root.AddChild(tab);
        }

        int count = GroupId() == 700 ? 4 : 10;
        int x = L("subX");
        for (int i = 0; i < count; i++)
        {
            int tab = i;
            int slot = L($"sub{GroupId()}W{i}");
            int w = GroupId() == 500 && _tab == i && i == 2 ? L("sub500W2Sel") : slot;
            int h = L($"sub{GroupId()}H");
            var sub = new GameObject();
            sub.Add(new Rect(new Rectangle(x - (w - slot) / 2, L("subY"), w, h)));
            sub.Add(new Button { Kind = ButtonKind.Click, OnClick = () => SetTab(tab) });
            sub.Add(new Picture($"ui/cashshop_sub_{GroupId()}_{i}{(_tab == i ? "_sel" : "")}.png"));
            root.AddChild(sub);
            x += slot + L("subGap");
        }
    }

    private void SetGroup(int group)
    {
        _page = group switch
        {
            500 => CashShopPage.Utility,
            600 => CashShopPage.Fashion,
            _ => CashShopPage.BeautyHair,
        };
        _tab = group == 500 ? 2 : 0;
        _selectedId = 0;
        Dirty = true;
    }

    private void SetTab(int tab)
    {
        _tab = tab;
        if (_page is CashShopPage.BeautyHair or CashShopPage.BeautyFace)
            _page = tab == 1 ? CashShopPage.BeautyFace : CashShopPage.BeautyHair;
        _selectedId = 0;
        Dirty = true;
    }

    private int GroupId() => _page switch
    {
        CashShopPage.Utility => 500,
        CashShopPage.Fashion => 600,
        _ => 700,
    };

    private void ApplyFilter()
    {
        bool any = false;
        foreach (var (cell, name) in _cells)
        {
            bool match = _search.Length == 0 || name.Contains(_search, StringComparison.OrdinalIgnoreCase);
            cell.Enabled = match;
            any |= match;
        }
        _noResult!.Enabled = _search.Length > 0 && !any;
    }

    private void AddPicture(GameObject root, string file, string x, string y, string w, string h)
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Rect(new Rectangle(L(x), L(y), L(w), L(h))));
        o.Add(new Picture("ui/" + file));
        root.AddChild(o);
    }

    private void AddDisabled(GameObject root, string file, string x, string y, string w, string h) =>
        AddPicture(root, file, x, y, w, h);

    private void AddButton(GameObject root, string file, string x, string y, string w, string h, Action onClick)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(L(x), L(y), L(w), L(h))));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Picture("ui/" + file));
        root.AddChild(o);
    }

    private void AddText(GameObject root, Func<string> text, string x, string y, string w, string h)
    {
        var o = new GameObject { NoHit = true };
        o.Add(new Rect(new Rectangle(L(x), L(y), L(w), L(h))));
        o.Add(new CenterText(text, TextInk, L("valueFont")));
        root.AddChild(o);
    }

    private sealed class CardBack : Component, IDrawable
    {
        private readonly Func<bool> _selected;
        public CardBack(Func<bool> selected) => _selected = selected;

        public void Draw(UiGfx gfx)
        {
            bool lit = _selected() || Inside(gfx.Hovered, Owner);
            gfx.Sb.Draw(gfx.Tex(lit ? "ui/cashshop_item_bg_main.png" : "ui/cashshop_item_bg.png"),
                Owner.Get<Rect>()!.Screen, Color.White);
        }

        private static bool Inside(GameObject? o, GameObject owner)
        {
            for (var p = o; p != null; p = p.Parent)
                if (p == owner) return true;
            return false;
        }
    }

    private sealed class CenterText : Component, IDrawable
    {
        private readonly Func<string> _text;
        private readonly Color _ink;
        private readonly int _font;
        public CenterText(Func<string> text, Color ink, int font) { _text = text; _ink = ink; _font = font; }

        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Screen;
            var tex = FitSized(gfx, _text(), r.Width, _font);
            gfx.Sb.Draw(tex, new Vector2(r.X + (r.Width - tex.Width) / 2, r.Y + (r.Height - tex.Height) / 2), _ink);
        }
    }

    private sealed class LeftText : Component, IDrawable
    {
        private readonly Func<string> _text;
        private readonly Color _ink;
        private readonly int _font;
        public LeftText(string text, Color ink, int font) { _text = () => text; _ink = ink; _font = font; }

        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Screen;
            var tex = FitSized(gfx, _text(), r.Width, _font);
            gfx.Sb.Draw(tex, new Vector2(r.X, r.Y + (r.Height - tex.Height) / 2), _ink);
        }
    }

    private static Texture2D FitSized(UiGfx gfx, string text, int width, int font)
    {
        var full = gfx.Ui.GetSized(text, font);
        if (full.Width <= width) return full;
        while (text.Length > 0 && gfx.Ui.GetSized(text + "..", font).Width > width) text = text[..^1];
        return gfx.Ui.GetSized(text + "..", font);
    }
}
