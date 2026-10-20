using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Setup screen: name the new hero, then confirm.
public partial class MushTownGame
{
    // The doll on the card plays the stand animation (the WZ stand1 frames).
    private int _setupFrame;
    private float _setupTimer;

    // Every number of this page lives in layouts/setup-layout.json: the board
    // place, the boxes inside it, the text sizes. The offsets are relative to
    // the board (the top card plus the lower card under it); the doll's feet
    // are an absolute screen spot. The official CustomizeChar/12200 ones are
    // the name box 27,90 and the buttons 12,231 and 186,231. className is the
    // box the class-name art centres in, and the art keeps its own size.
    private static Rectangle PanelRect() =>
        new(SetupLayout.Panel.X, SetupLayout.Panel.Y, SetupLayout.CardSize.W, SetupLayout.CardSize.H);
    private static Rectangle PanelBotRect() =>
        new(SetupLayout.Panel.X + SetupLayout.CardBot.X, SetupLayout.Panel.Y + SetupLayout.CardBot.Y,
            SetupLayout.CardBotSize.W, SetupLayout.CardBotSize.H);
    private static Rectangle NameFieldRect() =>
        new(SetupLayout.Panel.X + SetupLayout.NameField.X, SetupLayout.Panel.Y + SetupLayout.NameField.Y,
            SetupLayout.NameFieldSize.W, SetupLayout.NameFieldSize.H);
    private static Rectangle ConfirmBtnRect() =>
        new(SetupLayout.Panel.X + SetupLayout.Confirm.X, SetupLayout.Panel.Y + SetupLayout.Confirm.Y,
            SetupLayout.ConfirmSize.W, SetupLayout.ConfirmSize.H);
    private static Rectangle SetupCancelRect() =>
        new(SetupLayout.Panel.X + SetupLayout.Cancel.X, SetupLayout.Panel.Y + SetupLayout.Cancel.Y,
            SetupLayout.CancelSize.W, SetupLayout.CancelSize.H);

    private void MenuTextInput(char c)
    {
        if (!FocusBelongs() || c is < ' ' or > '~') return;
        if (_focus == "name" && _name.Length < 12 && SaveStore.IsIdChar(c)) _name += c;
    }

    // Typing and backspace only reach the focused field when it belongs to this screen.
    private bool FocusBelongs() => _screen == Screen.Setup && _focus == "name";

    private void BuildSetupScene()
    {
        // Shots never run Update, so the doll gets its frame from --frame there.
        _setupFrame = _shotPath != null ? _shotFrame : 0;
        _setupTimer = 0;
        _menuScene = new Scene();
        var bg = new GameObject { NoHit = true };
        bg.Add(new Rect(new Rectangle(0, 0, GameWidth, GameHeight)));
        bg.Add(new Picture("ui/setupbg.png", 0, 0));
        _menuScene.Add(bg);
        var preview = new GameObject { NoHit = true };
        preview.Add(new Invoke(_ => DrawSetupAvatar()));
        _menuScene.Add(preview);
        var panel = new GameObject { NoHit = true };
        panel.Add(new Rect(PanelRect()));
        panel.Add(new Picture("ui/setup_panel.png"));
        _menuScene.Add(panel);
        var panelBot = new GameObject { NoHit = true };
        panelBot.Add(new Rect(PanelBotRect()));
        panelBot.Add(new Picture("ui/setup_panel_bot.png"));
        _menuScene.Add(panelBot);
        // The picked class name sits where the layout puts it: the art keeps
        // its own size and centres in the className box (the Job row).
        var job = Picked;
        LoadTexture(job.Name);
        var jtex = _textures[job.Name];
        var jobO = new GameObject { NoHit = true };
        jobO.Add(new Rect(new Rectangle(
            SetupLayout.Panel.X + SetupLayout.ClassName.X + (SetupLayout.ClassNameSize.W - jtex.Width) / 2,
            SetupLayout.Panel.Y + SetupLayout.ClassName.Y + (SetupLayout.ClassNameSize.H - jtex.Height) / 2,
            jtex.Width, jtex.Height)));
        jobO.Add(new Picture(job.Name, 0, 0) { Tint = new Color(SetupLayout.ClassTint.R, SetupLayout.ClassTint.G, SetupLayout.ClassTint.B) });
        _menuScene.Add(jobO);
        var field = new GameObject();
        field.Add(new Rect(NameFieldRect()));
        field.Add(new Button { OnClick = () => _focus = "name" }); // the name field is input, not a button: silent
        field.Add(new Invoke(DrawNameField));
        _menuScene.Add(field);
        var confirm = MenuButton(ConfirmBtnRect(), "setup:confirm", () => DoCreate(), ButtonKind.Click);
        confirm.Add(new Image("ui/setup_ok"));
        _menuScene.Add(confirm);
        var cancel = MenuButton(SetupCancelRect(), "setup:cancel", () => SetupToJob(), ButtonKind.Click);
        cancel.Add(new Image("ui/setup_cancel"));
        _menuScene.Add(cancel);
        var err = new GameObject { NoHit = true };
        err.Add(new Invoke(DrawSetupError));
        _menuScene.Add(err);
    }

    // Typed name over the official box, with a caret while the field has focus.
    private void DrawNameField(UiGfx gfx)
    {
        var r = NameFieldRect();
        var tex = gfx.Ui.GetSetupName(_name == "" ? "1-12: A-Z a-z 0-9 _ -" : _name);
        int tx = r.X + SetupLayout.NameText.X;
        int ty = r.Y + (r.Height - tex.Height) / 2 + SetupLayout.NameText.Y;
        gfx.Sb.Draw(tex, new Vector2(tx, ty), _name == "" ? Hint : Ink);
        if (_focus == "name")
        {
            int cx = _name == "" ? tx + SetupLayout.CaretGap : tx + tex.Width + SetupLayout.CaretGap;
            int cy = r.Y + (r.Height - SetupLayout.Caret.H) / 2;
            gfx.Sb.Draw(gfx.Pixel, new Rectangle(cx, cy, SetupLayout.Caret.W, SetupLayout.Caret.H), Ink);
        }
    }

    // Error line under the card, centred on the layout anchor.
    private void DrawSetupError(UiGfx gfx)
    {
        if (_error == "") return;
        var tex = gfx.Ui.GetSetupError(_error);
        gfx.Sb.Draw(tex, new Vector2(SetupLayout.Panel.X + SetupLayout.Error.X - tex.Width / 2,
            SetupLayout.Panel.Y + SetupLayout.Error.Y), ErrRed);
    }

    private void SetupToJob()
    {
        _screen = Screen.Job;
        _focus = "";
        BuildJobScene();
        _scenes.Show(_menuScene);
    }

    private void UpdateSetup(bool click, bool enter, bool esc, int gx, int gy, float dtMs)
    {
        // the doll on the card plays the stand1 frames, like the in-game character
        (_setupFrame, _setupTimer) = SceneMath.AdvanceFrame(_setupFrame, _setupTimer, dtMs, _delays["stand1"]);
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
        DrawAvatar(SetupLayout.Avatar.X, SetupLayout.Avatar.Y, "stand1", _setupFrame, true);
        _camera.X = camX;
        _camera.Y = camY;
    }
}
