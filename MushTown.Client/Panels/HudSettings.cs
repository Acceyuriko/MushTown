using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The options window: the official option art with the two volume
// sliders and the button that opens the key settings.
public partial class MushTownGame
{
    private const int SettingsW = 460; // the option window art is 460 wide
    private const int SettingsBodyH = 633 - 16; // the art under the title strip

    // Every position of this window lives in layouts/panel-settings.json.
    private static readonly PanelLayout _settingsLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-settings.json"));

    private int _bgmAtOpen, _sfxAtOpen; // the volumes when the window opened
    private readonly List<Slider> _sliders = new(); // the volume sliders: each owns its own drag

    // Open the options window. The volumes are snapshotted here, so Cancel
    // can put them back even after the window moved.
    internal void OpenSettings()
    {
        _showSettings = true;
        _dragSettings = null;
        RaisePanel(PanelId(Feature.Settings));
        _bgmAtOpen = _bgm.Volume;
        _sfxAtOpen = _sfxVol;
    }

    // Close the options window.
    internal void CloseSettings()
    {
        foreach (var slider in _sliders) slider.Release(); // a drag dies with the window
        _showSettings = false;
        _dragSettings = null;
    }

    // The key settings window (the game made it, the window list keeps it).
    private static KeyConfigWindow? KeyWindow => UiWindow.List.OfType<KeyConfigWindow>().FirstOrDefault();

    private Rectangle SettingsRect() => new(0, 0, SettingsW, _settingsLay.Get("titleH", 16) + SettingsBodyH);

    private Rectangle SettingsTitleRect() => new(0, 0, SettingsW, _settingsLay.Get("titleH", 16));
    private Rectangle SettingsXRect() => new(_settingsLay.Get("closeX", 443), _settingsLay.Get("closeY", 3), 11, 11);

    // The spot the layout file gives the options window.
    // Static so the field initialiser can use it too.
    internal static Point SettingsLayoutPos() => new(_settingsLay.Get("x", 453), _settingsLay.Get("y", 67));

    private void BuildSettingsScene()
    {
        LoadTexture("ui/opt_bg.png");
        LoadTexture("ui/opt_sound.png");
        LoadTexture("ui/opt_tab.png");
        LoadTexture(Slider.KnobArt);
        LoadTexture("ui/opt_shortcut.png");
        LoadTexture("ui/hud_statx.png");
        foreach (string stem in new[] { "ui/opt_ok", "ui/opt_cancel" })
            foreach (string state in new[] { "_n.png", "_h.png", "_p.png" })
                LoadTexture(stem + state);

        _settingsHost.Clear();
        _sliders.Clear(); // the rebuild makes new ones
        var body = new GameObject();
        body.Add(new Rect(SettingsRect()));
        body.Add(new Picture("ui/opt_bg.png")); // the official option window
        body.Add(new Picture("ui/opt_sound.png", _settingsLay.Get("soundX", 139), _settingsLay.Get("soundY", 67)));
        body.Add(new Picture("ui/opt_tab.png", _settingsLay.Get("tabX", 21), _settingsLay.Get("tabY", 153)));

        AddVolumeSlider(body, true);
        AddVolumeSlider(body, false);

        // The key settings button opens the key window.
        var keyBtn = new GameObject();
        keyBtn.Add(new Rect(new Rectangle(_settingsLay.Get("keyX", 21), _settingsLay.Get("keyY", 580), 54, 34)));
        keyBtn.Add(new Button { Kind = ButtonKind.Click, OnClick = () => KeyWindow?.Open() });
        keyBtn.Add(new Picture("ui/opt_shortcut.png"));
        body.AddChild(keyBtn);
        body.Add(new Label("KEY SETTINGS", _settingsLay.Get("keyLabelX", 82), _settingsLay.Get("keyLabelY", 589))
        {
            Tint = new Color(60, 60, 70),
        });

        // Confirm keeps the volumes, Cancel puts them back and saves the old ones.
        body.AddChild(SettingsArtButton("ui/opt_ok", _settingsLay.Get("okX", 144), _settingsLay.Get("okY", 584), 83, 26, CloseSettings));
        body.AddChild(SettingsArtButton("ui/opt_cancel", _settingsLay.Get("cancelX", 233), _settingsLay.Get("cancelY", 584), 83, 26, () =>
        {
            bool changed = _bgm.Volume != _bgmAtOpen || _sfxVol != _sfxAtOpen;
            SetVolume(true, _bgmAtOpen);
            SetVolume(false, _sfxAtOpen);
            if (changed) SaveSettings(); // the sliders save on let go; Cancel undoes and saves that undo
            CloseSettings();
        }));

        body.AddChild(PanelButton(SettingsTitleRect(), "", () => _dragSettings = _uiScene.LastClick - _settingsPos)); // silent, like legacy
        var x = PanelButton(SettingsXRect(), "x4", CloseSettings, ButtonKind.Click);
        x.Add(new Picture("ui/hud_statx.png"));
        body.AddChild(x);

        _settingsHost.AddChild(body);
    }

    // One volume slider: the object's Rect is the knob's box (as wide as the track,
    // starting at knobDy), so it is the hit box; the component draws and drags.
    private void AddVolumeSlider(GameObject body, bool bgm)
    {
        int tx = bgm ? _settingsLay.Get("bgmX", 225) : _settingsLay.Get("sfxX", 225);
        int ty = (bgm ? _settingsLay.Get("bgmY", 208) : _settingsLay.Get("sfxY", 273)) + _settingsLay.Get("knobDy", -9);
        int tw = bgm ? _settingsLay.Get("bgmW", 193) : _settingsLay.Get("sfxW", 193);
        var slider = new Slider(() => bgm ? _bgm.Volume : _sfxVol, value => SetVolume(bgm, value), SaveSettings);
        _sliders.Add(slider);
        var track = new GameObject();
        track.Add(new Rect(new Rectangle(tx, ty, tw, 22)));
        track.Add(new Button { Kind = ButtonKind.Click, OnClick = () => slider.Grab(_uiScene.LastClick.X) });
        track.Add(slider);
        body.AddChild(track);
    }

    // The options window slider sets the volume (0-100: the caller passes a clamped
    // value). The save waits for the slider to be let go, or a drag would write the
    // file once per step.
    private void SetVolume(bool bgm, int value)
    {
        if (bgm) _bgm.Volume = value;
        else _sfxVol = value;
    }

    // One art button (the art has normal, hover and pressed states).
    private GameObject SettingsArtButton(string stem, int dx, int dy, int w, int h, Action onClick)
    {
        var o = new GameObject();
        o.Add(new Rect(new Rectangle(dx, dy, w, h)));
        o.Add(new Button { Kind = ButtonKind.Click, OnClick = onClick });
        o.Add(new Image(stem));
        return o;
    }
}
