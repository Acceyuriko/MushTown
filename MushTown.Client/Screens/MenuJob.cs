using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Job screen: the official ClassSelect page. Class cards page 14 at a time on
// the right; the picked class fills the left with its own illustration and
// the WZ name, mark, blurb, and stats. Only the Hero is playable yet.
public partial class MushTownGame
{
    // Card grid inside the list panel: 2 columns x 7 rows of 152x74 cards,
    // the first card 35px under the baked title (pitch measured off the art).
    private const int JobCellTop = 35, JobCellPitchX = 161, JobCellPitchY = 81;
    private const int JobCols = 2, JobRows = 7;
    private const int JobPlayable = 110; // every other class answers "Not playable yet."
    private const int JobTextW = 305; // WZ descWH width: name, blurb, and stat rows use it
    private string? _jobArt; // class art currently in _textures (BuildJobScene keeps one)
    private SpineClip? _spine; // baked animation for the picked class, if it has one
    private int _spineId = -1;

    private JobData Picked => _jobs[_jobPick];
    private int JobPages => (_jobs.Count + JobCols * JobRows - 1) / (JobCols * JobRows);

    private static Rectangle JobCellRect(int col, int row) =>
        new(JobLayout.List.X + JobCellPitchX * col, JobLayout.List.Y + JobCellTop + JobCellPitchY * row, 152, 74);

    // Pager strip under the panel: [<] page/total [>] on one glass bar.
    // Panel art height + pad + gap, so the bar clears the Create button.
    private Rectangle JobPageBar() => new(JobLayout.List.X + 76, JobLayout.List.Y + RectOf("ui/listbg.png", 0, 0).Height + JobLayout.ListPad + JobLayout.PageGap, 160, 26);

    private void BuildJobScene()
    {
        var job = Picked;
        // Animated classes play their bake; the rest keep static art. Only
        // one side is resident: picks and revisits evict whatever is old.
        string spineJson = Path.Combine(_assetsDir, "spine", job.Id.ToString(), $"{job.Id}.json");
        bool animated = File.Exists(spineJson);
        if (animated && _jobArt != null)
        {
            if (_textures.Remove(_jobArt, out var old)) old.Dispose();
            _jobArt = null;
        }
        if (!animated && _spine != null)
        {
            _spine.Dispose();
            _spine = null;
            _spineId = -1;
        }
        if (animated && _spineId != job.Id)
        {
            var (w, h) = PngSize(Path.Combine(_assetsDir, job.Art));
            var next = SpineClip.Load(GraphicsDevice, spineJson, GameWidth, GameHeight,
                new Rectangle(-job.ArtX, -job.ArtY, w, h));
            _spine?.Dispose();
            _spine = next;
            _spineId = job.Id;
        }
        if (!animated && _jobArt != job.Art)
        {
            if (_jobArt != null && _textures.Remove(_jobArt, out var old)) old.Dispose();
            LoadTexture(job.Art);
            _jobArt = job.Art;
        }
        if (_shotPath != null && _spine != null)
        {
            // Frozen pose: Update never runs in shots. --frame picks it (0 = mid-motion default).
            int n = _spine.Data.Slots[0].Frames.Count;
            _spine.Frame = _shotFrame == 0 ? n / 3 : _shotFrame % n;
        }
        _menuScene = new Scene();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(new Rectangle(0, 0, GameWidth, GameHeight)));
        bg.Add(new Picture("ui/job_bg.png"));
        _menuScene.Add(bg);
        var art = new GameObject { NoHit = true };
        if (_spine != null) art.Add(new SpineMesh(_spine));
        else
        {
            art.Add(new Rect(RectOf(job.Art, -job.ArtX, -job.ArtY))); // WZ origin: every class art places itself
            art.Add(new Picture(job.Art, 0, 0));
        }
        _menuScene.Add(art);
        // The list panel: dark backing plus the WZ glass grid. The official
        // client blurs what sits behind the panel; that is a shader we skip,
        // so the backing does the work. It hangs ListPad past the art because
        // the WZ grid sits flush with its edges.
        var panel = RectOf("ui/listbg.png", JobLayout.List.X, JobLayout.List.Y);
        int pad = JobLayout.ListPad;
        var list = new GameObject { NoHit = true };
        list.Add(new Rect(new Rectangle(panel.X - pad, panel.Y - pad, panel.Width + pad * 2, panel.Height + pad * 2)));
        list.Add(new Round(8, new Color(0, 8, 20, JobLayout.ListGlass)));
        list.Add(new Picture("ui/listbg.png", pad, pad));
        _menuScene.Add(list);
        int first = _jobPage * JobCols * JobRows;
        for (int i = first; i < Math.Min(_jobs.Count, first + JobCols * JobRows); i++)
        {
            var card = _jobs[i];
            int cell = i - first;
            int index = i; // a for-loop variable is shared: the closure must own its copy
            // One id per card: the hover sfx plays when the id changes, so a
            // shared "job:cell" would stay silent while crossing the grid.
            var o = MenuButton(JobCellRect(cell % JobCols, cell / JobCols), $"job:cell:{index}", () => JobPick(index), ButtonKind.Click);
            o.Add(new Picture(card.Card, 0, 0) { HoverAlpha = MenuAlpha.Hover, PressAlpha = MenuAlpha.Pressed });
            if (i == _jobPick) o.Add(new Picture("ui/job_cover.png", -3, -3)); // selection frame
            _menuScene.Add(o);
        }
        // One info card behind the whole left column: the WZ text sits straight
        // on the class art and some classes run bright right there, so the glass
        // goes under all of it. Bounds follow the text, so a moved row moves it.
        (string Label, string Value)[] stats = [("Race", job.Race), ("Move", job.Move), ("Stat", job.Stat)];
        int cardLeft = JobLayout.Desc.X - JobLayout.CardPad, cardTop = JobLayout.Name.Y - JobLayout.CardPad;
        int cardRight = JobLayout.Desc.X + JobTextW + JobLayout.CardPad;
        int cardBottom = JobLayout.Stat.Y + (stats.Length - 1) * JobLayout.StatLine + JobLayout.StatLine;
        var infoCard = new GameObject { NoHit = true };
        infoCard.Add(new Rect(new Rectangle(cardLeft, cardTop, cardRight - cardLeft, cardBottom - cardTop)));
        infoCard.Add(new Round(8, new Color(0, 8, 20, JobLayout.ListGlass)));
        _menuScene.Add(infoCard);
        var name = new GameObject { NoHit = true };
        var nameRect = RectOf(job.Name, JobLayout.Name.X, JobLayout.Name.Y);
        if (nameRect.Width > JobTextW) // className art runs 388 (Fire, Poison) to 409 (Ice, Lightning) wide
        {
            int h = nameRect.Height * JobTextW / nameRect.Width;
            nameRect = new Rectangle(JobLayout.Name.X, JobLayout.Name.Y + (nameRect.Height - h) / 2, JobTextW, h);
        }
        name.Add(new Rect(nameRect));
        name.Add(new Picture(job.Name)); // stretched: native size unless the name is wider than the card
        _menuScene.Add(name);
        var mark = new GameObject { NoHit = true };
        mark.Add(new Rect(RectOf(job.Mark, JobLayout.Mark.X, JobLayout.Mark.Y)));
        mark.Add(new Picture(job.Mark, 0, 0));
        _menuScene.Add(mark);
        var sub = new GameObject { NoHit = true };
        sub.Add(new Rect(new Rectangle(JobLayout.Sub.X, JobLayout.Sub.Y, JobTextW, 22)));
        sub.Add(new LiveLabel(() => job.Sub, 0, 0, Color.White)); // no shadow: the card is dark, a shadow only smears small text
        _menuScene.Add(sub);
        var desc = new GameObject { NoHit = true };
        desc.Add(new Rect(new Rectangle(JobLayout.Desc.X, JobLayout.Desc.Y, JobTextW, JobLayout.DescLine * job.Lines.Count)));
        var lines = job.Lines; // the WZ desc carries its own line breaks
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            desc.Add(new LiveLabel(() => line, 0, i * JobLayout.DescLine, Color.White));
        }
        _menuScene.Add(desc);
        var rows = new GameObject { NoHit = true };
        rows.Add(new Rect(new Rectangle(JobLayout.Stat.X, JobLayout.Stat.Y, JobTextW, stats.Length * JobLayout.StatLine)));
        int valueX = JobLayout.StatValueX - JobLayout.Stat.X;
        for (int i = 0; i < stats.Length; i++)
        {
            var (label, value) = stats[i];
            int y = i * JobLayout.StatLine;
            rows.Add(new LiveLabel(() => label, 0, y, ListGray, LabelFont.Mid));
            rows.Add(new LiveLabel(() => value, valueX, y, Color.White, LabelFont.Mid));
        }
        _menuScene.Add(rows);
        var make = MenuButton(UiRect("btn_make_n.png", JobLayout.Make.X, JobLayout.Make.Y), "job:make", () => JobMake(), ButtonKind.Click);
        make.Add(new Image("ui/btn_make")); // the WZ hover/pressed art, not a white box over the pill
        _menuScene.Add(make);
        var back = MenuButton(UiRect("cs_back_n.png", JobLayout.Back.X, JobLayout.Back.Y), "job:back", () => JobToChars(), ButtonKind.Click);
        back.Add(new Round(8, new Color(0, 0, 0, JobLayout.ListGlass))); // glass behind the pill, like chars
        back.Add(new Image("ui/cs_back"));
        _menuScene.Add(back);
        var bar = JobPageBar();
        var strip = new GameObject { NoHit = true };
        strip.Add(new Rect(bar));
        strip.Add(new Round(6, new Color(0, 8, 20, JobLayout.ListGlass)));
        strip.Add(new CenterText(() => $"{_jobPage + 1}/{JobPages}", bar.X + bar.Width / 2, bar.Y + 7, Color.White, small: true));
        _menuScene.Add(strip);
        JobPageArrow(bar.X + 14, _jobPage > 0, "job_page_l", -1);
        JobPageArrow(bar.Right - 24, _jobPage < JobPages - 1, "job_page_r", 1);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, JobLayout.ErrorY, ErrRed));
        _menuScene.Add(err);
    }

    // One pager arrow: grayed and unhittable at the first/last card page.
    private void JobPageArrow(int x, bool on, string stem, int dir)
    {
        var o = new GameObject { NoHit = !on };
        o.Add(new Rect(new Rectangle(x, JobPageBar().Y + 6, 10, 15)));
        // No _off frame: pagePrev/0-13 are animation frames, not states, so
        // the tint does the dimming.
        var pic = new Picture($"ui/{stem}.png", 0, 0);
        if (!on) pic.Tint = new Color(120, 120, 120);
        o.Add(pic);
        if (on) o.Add(new Button { LegacyId = $"job:page:{dir}", Kind = ButtonKind.Click, OnClick = () => JobTurnPage(dir) });
        _menuScene.Add(o);
    }

    private void JobTurnPage(int dir)
    {
        _jobPage += dir;
        BuildJobScene();
        _scenes.Show(_menuScene);
    }

    private void JobPick(int index)
    {
        _jobPick = index;
        BuildJobScene();
        _scenes.Show(_menuScene);
    }

    private void JobMake()
    {
        if (Picked.Id != JobPlayable) { _error = "Not playable yet."; return; }
        _name = "";
        _focus = "name";
        _screen = Screen.Setup;
        BuildSetupScene();
        _scenes.Show(_menuScene);
    }

    private void JobToChars()
    {
        _screen = Screen.Chars;
        BuildCharsScene();
        _scenes.Show(_menuScene);
    }

    private void UpdateJob(bool click, bool enter, bool esc, int gx, int gy, float dtMs)
    {
        _spine?.Advance(dtMs);
        SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (esc) { JobToChars(); return; }
        GameObject? took = click ? ClickTree(_scenes.Current, gx, gy) : null;
        // Enter confirms unless the click took something: legacy chained
        // cell, then make-or-enter, so any tree click swallows enter.
        if (enter && took == null && _screen == Screen.Job) JobMake();
    }

    private void EnterJob(int slot)
    {
        _slot = slot;
        _jobPick = 0; // Hero first (WZ card order)
        _jobPage = 0;
        _screen = Screen.Job;
        BuildJobScene();
        _scenes.Show(_menuScene);
    }

    // PNG width/height straight from the IHDR: the spine fit needs the art
    // size without paying for the texture.
    private static (int w, int h) PngSize(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing exported png: {Path.GetFullPath(path)}", path);
        using var stream = File.OpenRead(path);
        var head = new byte[24];
        stream.ReadExactly(head);
        return (head[16] << 24 | head[17] << 16 | head[18] << 8 | head[19],
            head[20] << 24 | head[21] << 16 | head[22] << 8 | head[23]);
    }
}
