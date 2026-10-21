using System.Text.Json;
using Microsoft.Xna.Framework.Audio;
using MushTown.Shared;

namespace MushTown.Client;

// Sound: short WAV effects plus the music (the BgmPlayer component owns
// the track). Volumes live in data/settings.json (0-100 each); a missing
// or broken file falls back to the defaults (prefs must never block boot).
public partial class MushTownGame
{
    private readonly Dictionary<string, SoundEffect> _sfx = new();
    private int _sfxVol = 80;
    private string _settingsPath = "";

    private void LoadAudio()
    {
        _settingsPath = Path.Combine(Path.GetDirectoryName(_savesPath)!, "settings.json");
        LoadSettings();
        foreach (string key in SoundKey.Sfx)
            _sfx[key] = Wav.Load(Path.Combine(_assetsDir, "sound", key + ".wav"));
    }

    private void LoadSettings()
    {
        try
        {
            var json = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath));
            if (json == null) return;
            _bgm.Volume = json.Bgm;
            _sfxVol = Math.Clamp(json.Sfx, 0, 100);
            LoadHotkeys(json.Keys);
            LoadHotkeyPresets(json.Presets);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); // fresh checkouts have no data/ yet (SaveStore.Save does the same)
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new Settings { Bgm = _bgm.Volume, Sfx = _sfxVol, Keys = SaveHotkeys(), Presets = SaveHotkeyPresets() }));
    }

    private sealed class Settings
    {
        public int Bgm { get; set; } = 80;
        public int Sfx { get; set; } = 80;
        public Dictionary<string, string>? Keys { get; set; } // the panel hotkeys (action name -> key name)
        public List<Dictionary<string, string>>? Presets { get; set; } // the three saved binding sets
    }

    // The track for the screen: the login theme over the menus, the map's
    // own track in the game.
    private void PlayScreenBgm()
    {
        bool inGame = _screen == Screen.Game;
        string key = inGame ? SoundKey.FromBgm(_map.Info.Bgm) : SoundKey.Login;
        if (key == "")
            throw new InvalidDataException($"map {_mapId} has a bad bgm '{_map.Info.Bgm}'");
        if (!_bgm.Play(key)) return;
        GameLog($"bgm: {(inGame ? _map.Info.Bgm : "Sound/BgmUI.img/Title")}");
    }

    private void PlaySfx(string key)
    {
        if (_shotPath != null) return; // shots pose silent: no sfx blast (the music is guarded in the player)
        _sfx[key].Play(_sfxVol / 100f, 0f, 0f);
    }

    // The options window slider sets the volume; the change saves right away.
    private void BumpVolume(bool bgm, int d)
    {
        if (bgm) _bgm.Volume = _bgm.Volume + d;
        else _sfxVol = Math.Clamp(_sfxVol + d, 0, 100);
        SaveSettings();
    }

    // The mop whooshes like a polearm; bare beginner hands swing a sword.
    private string SwingKey() =>
        _save?.Slots[_slot]?.Equips?.Contains(ItemData.Mop) == true ? "swing_pole" : "swing_sword";
}
