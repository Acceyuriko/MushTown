using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Setup screen: name the new hero, then confirm.
public partial class MushTownGame
{
    private static Rectangle NameFieldRect() => new(660, 258, 304, 40);
    private static Rectangle ConfirmBtnRect() => new(660, 320, 166, 43);
    private static Rectangle SetupCancelRect() => new(846, 320, 167, 43);

    private void MenuTextInput(char c)
    {
        if (!FocusBelongs() || c is < ' ' or > '~') return;
        if (_focus == "name" && _name.Length < 12 && SaveStore.IsIdChar(c)) _name += c;
    }

    // Typing and backspace only reach the focused field when it belongs to this screen.
    private bool FocusBelongs() => _screen == Screen.Setup && _focus == "name";

    private void BuildSetupScene()
    {
        _menuScene = new Scene();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(new Rectangle(0, 0, GameWidth, GameHeight)));
        bg.Add(new Picture("ui/setupbg.png", 0, 0));
        _menuScene.Add(bg);
        var preview = new GameObject { NoHit = true };
        preview.Add(new Invoke(_ => DrawSetupAvatar()));
        _menuScene.Add(preview);
        var labels = new GameObject { NoHit = true };
        labels.Add(new Text("Hero", 660, 200, Ink));
        labels.Add(new Text("Name", 660, 232, Ink));
        _menuScene.Add(labels);
        var name = new GameObject();
        name.Add(new Rect(NameFieldRect()));
        name.Add(new Button { OnClick = () => _focus = "name" }); // the name field is input, not a button: silent
        name.Add(new UiBox(() => _name, "1-12: A-Z a-z 0-9 _ -", () => _focus == "name", Ink, Hint));
        _menuScene.Add(name);
        var confirm = MenuButton(ConfirmBtnRect(), "setup:confirm", () => DoCreate(), ButtonKind.Click);
        confirm.Add(new ImgButton("ui/btn_confirm.png"));
        _menuScene.Add(confirm);
        var cancel = MenuButton(SetupCancelRect(), "setup:cancel", () => SetupToJob(), ButtonKind.Click);
        cancel.Add(new ImgButton("ui/btn_cancel.png"));
        _menuScene.Add(cancel);
        var err = new GameObject { NoHit = true };
        err.Add(new CenterText(() => _error, 817, 375, ErrRed));
        _menuScene.Add(err);
    }

    private void SetupToJob()
    {
        _screen = Screen.Job;
        _focus = "";
        BuildJobScene();
        _scenes.Show(_menuScene);
    }

    private void UpdateSetup(bool click, bool enter, bool esc, int gx, int gy)
    {
        SfxOnHover(_scenes.Current?.Hover(gx, gy) ?? "");
        if (esc) { SetupToJob(); return; }
        GameObject? took = click ? ClickTree(_scenes.Current, gx, gy) : null;
        // Enter confirms unless the click took something: legacy chained
        // name, then confirm-or-enter, so any tree click swallows enter.
        if (enter && took == null && _screen == Screen.Setup) DoCreate();
    }

    private void MenuBackspace()
    {
        if (!FocusBelongs()) return;
        if (_focus == "name" && _name.Length > 0) _name = _name[..^1];
    }

    private void DoCreate()
    {
        bool created = false;
        try
        {
            _store.CreateHero(_saveSlot, _slot, _name);
            created = true; // from here a failed Save must roll the new slot back out
            _store.Save(_savesPath);
            GameLog($"created {_name}");
            _name = "";
            _focus = "";
            _page = _slot / PageSlots; // the new char stays on screen
            _screen = Screen.Chars;
            BuildCharsScene();
            _scenes.Show(_menuScene);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (created) _save!.Slots[_slot] = null;
            _error = ex.Message;
        }
    }

    // The setup preview stays code-drawn (doll pixels, like the chars
    // strip): the tree only orders it above the backdrop.
    private void DrawSetupAvatar()
    {
        // DrawAvatar speaks world pixels: park the camera while previewing on art
        int camX = _camera.X, camY = _camera.Y;
        _camera.X = 0;
        _camera.Y = 0;
        DrawAvatar(450, 735, "stand2", 0, false);
        _camera.X = camX;
        _camera.Y = camY;
    }
}
