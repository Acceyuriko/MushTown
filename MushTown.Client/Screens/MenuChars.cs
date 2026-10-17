using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Chars screen: the doll shelf, pager, select/new/delete/back.
public partial class MushTownGame
{
    // Char select in game pixels (1366x768), shared by click and draw code.
    // All numbers live in layouts/charselect-layout.json (DEBUG builds reload it
    // live); code only centers. Button sizes come from the art.
    private const int PageSlots = 12;
    private const int Pages = 5;
    private static (int fx, int fy) SlotFeet(int cell) =>
        cell < 6 ? (CharLayout.SlotTopX[cell], CharLayout.SlotTopY) : (CharLayout.SlotBotX[cell - 6], CharLayout.SlotBotY);
    private static Rectangle SlotRect(int cell)
    {
        var (fx, fy) = SlotFeet(cell);
        return new Rectangle(fx - CharLayout.SlotW / 2, fy - CharLayout.SlotH, CharLayout.SlotW, CharLayout.SlotH);
    }
    private Rectangle RectOf(string key, int x, int y)
    {
        var t = _textures[key];
        return new Rectangle(x, y, t.Width, t.Height);
    }
    private Rectangle UiRect(string file, int x, int y) => RectOf("ui/" + file, x, y);
    private static Rectangle WorldPillRect() => new(CharLayout.Pill.X, CharLayout.Pill.Y, 200, 57);
    private Rectangle BoardRect() => UiRect("cs_board.png", CharLayout.Board.X, CharLayout.Board.Y);
    private Rectangle SelectBtnRect() => UiRect("cs_select_n.png", CharLayout.Select.X, CharLayout.Select.Y);
    private Rectangle NewBtnRect() => UiRect("cs_new_n.png", CharLayout.New.X, CharLayout.New.Y);
    private Rectangle DeleteBtnRect() => UiRect("cs_delete_n.png", CharLayout.Delete.X, CharLayout.Delete.Y);
    private Rectangle BackBtnRect() => UiRect("cs_back_n.png", CharLayout.Back.X, CharLayout.Back.Y);
    private Rectangle PagerBgRect() => UiRect("cs_page_bg.png", CharLayout.Pager.X, CharLayout.Pager.Y);
    // Pager cells: L, five digits, R. The arrows sit apart in the black
    // bar ends (layout file); digits tile the white bar (~37.6px each).
    // Digit art (30x33) overflows the 26px bar height, like the official look.
    private Rectangle PageLRect() => UiRect("cs_page_l_n.png", CharLayout.PageL.X, CharLayout.PageL.Y);
    private Rectangle PageRRect() => UiRect("cs_page_r_n.png", CharLayout.PageR.X, CharLayout.PageR.Y);
    private Rectangle PageCellRect(int cell)
    {
        var bg = PagerBgRect();
        if (cell == 0) return PageLRect();
        if (cell == 6) return PageRRect();
        // Digits tile the whole bar now the arrows moved out (188/5 = 37.6px each).
        float pitch = bg.Width / 5f;
        int x0 = bg.X + (int)((cell - 1) * pitch);
        return new Rectangle(x0, bg.Y, bg.X + (int)(cell * pitch) - x0, bg.Height);
    }
    private static Rectangle DeletePanelRect() => new(CharLayout.DelPanel.X, CharLayout.DelPanel.Y, CharLayout.DelSize.X, CharLayout.DelSize.Y);
    private Rectangle DeleteYesRect() => UiRect("cs_delyes_n.png", CharLayout.DelYes.X, CharLayout.DelYes.Y);
    private Rectangle DeleteNoRect() => UiRect("cs_delno_n.png", CharLayout.DelNo.X, CharLayout.DelNo.Y);

    private void BuildCharsScene()
    {
        _menuScene = new Scene();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(new Rectangle(0, 0, GameWidth, GameHeight)));
        bg.Add(new Picture("back/charselect.png"));
        _menuScene.Add(bg);
        for (int cell = 0; cell < PageSlots; cell++) // slots first (bottom): pager, buttons, modal cover them
        {
            int i = _page * PageSlots + cell;
            string id = _save!.Slots[i] != null ? "chars:slot" + cell : ""; // ghosts (empty slots) stay silent
            _menuScene.Add(MenuButton(SlotRect(cell), id, () => { if (_save!.Slots[i] != null) { PlaySfx("click"); _slot = i; } }));
        }
        var dolls = new GameObject { NoHit = true };
        dolls.Add(new Invoke(_ => DrawCharsSlots()));
        _menuScene.Add(dolls);
        var pill = WorldPillRect();
        var pillO = new GameObject { NoHit = true };
        pillO.Add(new Rect(pill));
        pillO.Add(new Picture("ui/selectworld.png", 0, 0));
        pillO.Add(new CenterText(() => ServerLabel(), pill.X + pill.Width / 2, pill.Y + 15, Ink));
        _menuScene.Add(pillO);
        var b = BoardRect();
        var board = new GameObject { NoHit = true };
        board.Add(new Rect(b));
        board.Add(new Picture("ui/cs_board.png", 0, 0));
        board.Add(new CenterText(() => $"{_save!.Slots.Count(s => s != null)}/{SaveStore.MaxHeroes}",
            b.X + CharLayout.BoardCount.X, b.Y + CharLayout.BoardCount.Y, Color.White, small: true));
        _menuScene.Add(board);
        _pager = new GameObject(); // container: no Rect (unhittable), cells tested through it
        var pager = PagerBgRect();
        var glass = new GameObject { NoHit = true };
        glass.Add(new Rect(pager));
        var bb = _textures["ui/cs_page_base.png"];
        glass.Add(new Picture("ui/cs_page_base.png", (pager.Width - bb.Width) / 2, 0) { Tint = new Color(255, 255, 255, CharLayout.PagerGlass) });
        _pager.AddChild(glass);
        var pbg = new GameObject { NoHit = true };
        pbg.Add(new Rect(pager));
        pbg.Add(new Picture("ui/cs_page_bg.png", 0, 0));
        _pager.AddChild(pbg);
        for (int c = 0; c < 7; c++)
        {
            int p = c; // for-loop variables capture shared: copy per cell
            var cb = MenuButton(PageCellRect(c), "chars:page" + c,
                () => { _page = p == 0 ? Math.Max(0, _page - 1) : p == 6 ? Math.Min(Pages - 1, _page + 1) : p - 1; BuildCharsScene(); _scenes.Show(_menuScene); }, ButtonKind.Click);
            string stem = c == 0 ? "page_l" : c == 6 ? "page_r" : $"page_{c}";
            cb.Add(new PagerCell(stem, c - 1 == _page, c != 0 && c != 6));
            _pager.AddChild(cb);
        }
        _menuScene.Add(_pager);
        var sel = _save!.Slots[_slot]; // select/delete exist only over a hero (slot clicks never empty it: no rebuild needed)
        if (sel != null)
        {
            var sb = MenuButton(SelectBtnRect(), "chars:select", () => { if (_save!.Slots[_slot] != null) { PlaySfx("click"); StartGame(ServerMode()); } });
            sb.Add(new Image("ui/cs_select"));
            _menuScene.Add(sb);
        }
        var nb = MenuButton(NewBtnRect(), "chars:new", () => { PlaySfx("click"); EnterJob(FirstEmptyHeroSlot()); });
        nb.Add(new Image("ui/cs_new"));
        _menuScene.Add(nb);
        if (sel != null)
        {
            var db = MenuButton(DeleteBtnRect(), "chars:delete", () => { if (_save!.Slots[_slot] != null) { PlaySfx("click"); _csDeleteModal.Enabled = true; } });
            db.Add(new Picture("ui/cs_delglass.png", 0, 0) { Tint = new Color(255, 255, 255, CharLayout.PagerGlass) });
            db.Add(new Image("ui/cs_delete"));
            _menuScene.Add(db);
        }
        var back = MenuButton(BackBtnRect(), "chars:back", () => EnterServer(), ButtonKind.Click);
        back.Add(new Round(8, new Color(0, 0, 0, CharLayout.PagerGlass))); // radius 8 matches the official delete backing
        back.Add(new Image("ui/cs_back"));
        _menuScene.Add(back);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, CharLayout.ErrorY, ErrRed));
        _menuScene.Add(err);
        _csDeleteModal = new GameObject { Modal = true };
        _csDeleteModal.Add(new Rect(DeletePanelRect())); // panel hits (not buttons): the box stays open, like legacy
        var dim = new GameObject { NoHit = true };
        dim.Add(new Dim(120));
        _csDeleteModal.AddChild(dim);
        var panel = new GameObject { NoHit = true };
        panel.Add(new Rect(DeletePanelRect()));
        panel.Add(new WhitePanel());
        _csDeleteModal.AddChild(panel);
        var dt = new GameObject(); // no Rect: draws, never hits
        dt.Add(new CenterText(() => $"Delete {_save!.Slots[_slot]!.Name}?", 683, CharLayout.DelTextY, Ink));
        _csDeleteModal.AddChild(dt);
        var dy = MenuButton(DeleteYesRect(), "chars:delyes", () => DoDelete(), ButtonKind.Click);
        dy.Add(new Image("ui/cs_delyes"));
        _csDeleteModal.AddChild(dy);
        var dn = MenuButton(DeleteNoRect(), "chars:delno", () => _csDeleteModal.Enabled = false, ButtonKind.Click);
        dn.Add(new Image("ui/cs_delno"));
        _csDeleteModal.AddChild(dn);
        _csDeleteModal.Enabled = false;
        _menuScene.Add(_csDeleteModal);
    }

    private void UpdateChars(bool click, bool enter, bool esc, int gx, int gy, float dtMs)
    {
        SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        AdvanceClip(_ghost, dtMs);
        AdvanceClip(_portal, dtMs);
        AdvanceClip(_motes, dtMs);
        if (_walkSlot != _slot) { _walkSlot = _slot; _walkFrame = 0; _walkT = 0; } // fresh selection walks from frame 0
        _walkT += dtMs;
        var delays = _delays["walk2"];
        if (_walkT >= delays[_walkFrame]) { _walkT -= delays[_walkFrame]; _walkFrame = (_walkFrame + 1) % delays.Count; }
        if (_csDeleteModal.Enabled)
        {
            if (esc) { _csDeleteModal.Enabled = false; return; }
            if (click && ClickTree(_scenes.Current, gx, gy) == null) _csDeleteModal.Enabled = false; // outside click closes, silent
            return;
        }
        if (esc) { EnterServer(); return; }
        try
        {
            var pagerBefore = _pager; // snapshot: pager clicks rebuild the tree (and _pager) mid-dispatch
            GameObject? took = click ? ClickTree(_scenes.Current, gx, gy) : null;
            // Enter selects unless the pager took the click (legacy order:
            // pager, then select) or the click left this screen. Enter plays
            // nothing. Same-frame enter + new/delete/slot clicks resolve
            // mouse-first: legacy interleaved enter between the pager and the
            // buttons, and a unified dispatch cannot split them.
            if (_screen == Screen.Chars && enter && took?.Parent != pagerBefore && _save!.Slots[_slot] != null)
                StartGame(ServerMode());
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            && ex is not FileNotFoundException and not DirectoryNotFoundException)
        {
            _error = ex.Message; // save/net/data failures become menu errors (missing art crashes loud above)
            ToChars(); // a failed Start may have moved the map/camera: step back out
        }
        if (_screen != Screen.Chars) return;
    }

    // Server choice confirmed: characters become the hub (first occupied selected).
    private void EnterChars(string? host)
    {
        _hostPick = host;
        _focus = "";
        ZeroCamera();
        SelectFirstHero();
        GameLog($"server: {ServerLabel()}");
        _screen = Screen.Chars;
        BuildCharsScene();
        _scenes.Show(_menuScene);
    }

    // First filled slot (or 0 when all are empty); the page follows it.
    private void SelectFirstHero()
    {
        _slot = 0;
        for (int i = 0; i < SaveStore.MaxHeroes; i++)
            if (_save!.Slots[i] != null) { _slot = i; break; }
        _page = _slot / PageSlots;
    }

    private int FirstEmptyHeroSlot()
    {
        for (int i = 0; i < SaveStore.MaxHeroes; i++)
            if (_save!.Slots[i] == null) return i;
        throw new InvalidDataException("slots full, delete one first");
    }

    private void DoDelete()
    {
        string name = _save!.Slots[_slot]!.Name;
        var backup = _save.Slots.ToList(); // delete compacts: roll back the whole row, not one slot
        try
        {
            _store.DeleteHero(_saveSlot, _slot);
            _store.Save(_savesPath);
            GameLog($"deleted {name}");
            SelectFirstHero(); // the deleted slot is empty now: show a filled one (or nothing when all are empty)
            _csDeleteModal.Enabled = false;
            BuildCharsScene(); // the fill-set changed: silent slots move
            _scenes.Show(_menuScene);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _save.Slots.Clear();
            _save.Slots.AddRange(backup); // a failed Save must roll the row back out
            _csDeleteModal.Enabled = false;
            _error = ex.Message;
        }
    }

    // The doll strip stays code-drawn: the sim phase owns the doll,
    // the tree only orders it (behind the pill, above the grass).
    private void DrawCharsSlots()
    {
        for (int cell = 0; cell < PageSlots; cell++)
        {
            int i = _page * PageSlots + cell;
            var (fx, fy) = SlotFeet(cell);
            var s = _save!.Slots[i];
            bool faceRight = cell % 6 < 3; // outer slots face the middle
            if (s == null) { DrawClip(_ghost, fx, fy, !faceRight); continue; } // raw art faces right
            if (i == _slot)
            {
                // The totem sits behind the walker, mirrored with the facing.
                var pt = _textures["ui/cs_pillar.png"];
                int px = faceRight ? fx + CharLayout.Pillar.X : fx - (pt.Width + CharLayout.Pillar.X);
                DrawUi("cs_pillar.png", px, fy + CharLayout.Pillar.Y, !faceRight);
                DrawClip(_portal, fx, fy);
                DrawClip(_motes, fx, fy); // glow stays behind: the walker stays crisp
                DrawAvatar(fx, fy, "walk2", _walkFrame, faceRight, s.Equips);
            }
            else DrawAvatar(fx, fy, "stand2", 0, faceRight, s.Equips);
            DrawName(fx, fy + CharLayout.TagDown, s.Name, PlateFor(s.Equips));
        }
    }
}
