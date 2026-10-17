using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Job screen: pick the dummy Hero, then Make.
public partial class MushTownGame
{
    private static Rectangle ListBgRect() => new(526, 87, 313, 593);
    private static Rectangle CellRect() => new(554, 147, 152, 74);
    private static Rectangle MakeBtnRect() => new(489, 692, 200, 57);
    private static Rectangle JobCancelRect() => new(709, 699, 167, 43);

    private void BuildJobScene()
    {
        _menuScene = new Scene();
        var l = ListBgRect();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(l));
        bg.Add(new Picture("ui/listbg.png", 0, 0));
        _menuScene.Add(bg);
        var cell = MenuButton(CellRect(), "job:cell",
            () => { _jobPicked = true; BuildJobScene(); _scenes.Show(_menuScene); }, ButtonKind.Click);
        if (_jobPicked)
        {
            cell.Add(new Picture("ui/cell_hero.png", 0, 0));
            cell.Add(new Border(Color.Gold));
        }
        else cell.Add(new ImgButton("ui/cell_hero.png"));
        _menuScene.Add(cell);
        var blurb = new GameObject { NoHit = true };
        blurb.Add(new CenterText(() => "A dummy warrior. Real jobs come later.", 683, 640, Color.White));
        _menuScene.Add(blurb);
        var make = MenuButton(MakeBtnRect(), "job:make", () => JobMake(), ButtonKind.Click);
        make.Add(new ImgButton("ui/btn_make.png"));
        _menuScene.Add(make);
        var cancel = MenuButton(JobCancelRect(), "job:cancel", () => JobToChars(), ButtonKind.Click);
        cancel.Add(new ImgButton("ui/btn_cancel.png"));
        _menuScene.Add(cancel);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 683, 55, ErrRed));
        _menuScene.Add(err);
    }

    private void JobMake()
    {
        if (!_jobPicked) { _error = "pick the Hero first"; return; }
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

    private void UpdateJob(bool click, bool enter, bool esc, int gx, int gy)
    {
        if (HitRect(LogButtonRect(), gx, gy)) SfxOnHover("log");
        else SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (esc) { JobToChars(); return; }
        GameObject? took = click ? ClickTree(_scenes.Current, gx, gy) : null;
        // Enter confirms unless the click took something: legacy chained
        // cell, then make-or-enter, so any tree click swallows enter.
        if (enter && took == null && _screen == Screen.Job) JobMake();
    }

    private void EnterJob(int slot)
    {
        _slot = slot;
        _jobPicked = false;
        _screen = Screen.Job;
        BuildJobScene();
        _scenes.Show(_menuScene);
    }
}
