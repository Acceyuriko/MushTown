using System.Text.Json;
using Microsoft.Xna.Framework.Audio;
using MushTown.Shared;

namespace MushTown.Client;

// Sound: short WAV effects plus one looped music track per map. Volumes
// live in data/settings.json (0-100 each); a missing or broken file falls
// back to the defaults (prefs must never block boot).
public partial class MushTownGame
{
    private readonly Dictionary<string, SoundEffect> _sfx = new();
    private SoundEffect? _bgmEffect;
    private SoundEffectInstance? _bgm;
    private string _playingBgm = "";
    private int _bgmVol = 80, _sfxVol = 80;
    private string _settingsPath = "";

    private void LoadAudio()
    {
        _settingsPath = Path.Combine(Path.GetDirectoryName(_accountsPath)!, "settings.json");
        LoadSettings();
        foreach (string key in SoundKey.Sfx)
            _sfx[key] = LoadWav(Path.Combine(_assetsDir, "sound", key + ".wav"));
        _bgm!.Volume = _bgmVol / 100f; // Initialize loads the map first: the boot track plays, retune it to the saved volume
    }

    private void LoadSettings()
    {
        try
        {
            var json = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath));
            if (json == null) return;
            _bgmVol = Math.Clamp(json.Bgm, 0, 100);
            _sfxVol = Math.Clamp(json.Sfx, 0, 100);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); // fresh checkouts have no data/ yet (AccountStore.Save does the same)
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new Settings { Bgm = _bgmVol, Sfx = _sfxVol }));
    }

    private sealed class Settings
    {
        public int Bgm { get; set; } = 80;
        public int Sfx { get; set; } = 80;
    }

    // Our own 44-byte header (SoundExport.ToWav writes it): channels at 22,
    // rate at 24, PCM16 after. A missing file throws loud, like every asset.
    private static SoundEffect LoadWav(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing exported wav: {Path.GetFullPath(path)}", path);
        byte[] wav = File.ReadAllBytes(path);
        int channels = BitConverter.ToInt16(wav, 22);
        int rate = BitConverter.ToInt32(wav, 24);
        return new SoundEffect(wav[44..], rate, (AudioChannels)channels);
    }

    // The map's track, looped. Same track across a switch keeps playing.
    private void PlayBgm()
    {
        string key = SoundKey.FromBgm(_map.Info.Bgm);
        if (key == "")
            throw new InvalidDataException($"map {_mapId} has a bad bgm '{_map.Info.Bgm}'");
        if (key == _playingBgm) return;
        _playingBgm = key;
        _bgm?.Stop();
        _bgm?.Dispose();
        _bgmEffect?.Dispose(); // the instance does not own its buffer: drop it too
        GameLog($"bgm: {_map.Info.Bgm}");
        _bgmEffect = LoadWav(Path.Combine(_assetsDir, "sound", key + ".wav"));
        _bgm = _bgmEffect.CreateInstance();
        _bgm.IsLooped = true;
        _bgm.Volume = _bgmVol / 100f;
        _bgm.Play();
    }

    private void PlaySfx(string key) => _sfx[key].Play(_sfxVol / 100f, 0f, 0f);

    // +/- buttons in the settings window (step 10). The click confirms the
    // press; the SFX row also previews its new volume.
    private void BumpVolume(bool bgm, int d)
    {
        if (bgm) _bgmVol = Math.Clamp(_bgmVol + d, 0, 100);
        else _sfxVol = Math.Clamp(_sfxVol + d, 0, 100);
        _bgm!.Volume = _bgmVol / 100f; // the settings window needs a map: its track plays
        SaveSettings();
        PlaySfx("click");
    }

    // The mop whooshes like a polearm; bare beginner hands swing a sword.
    private string SwingKey() =>
        _account?.Slots[_slot]?.Equips?.Contains(ItemData.Mop) == true ? "swing_pole" : "swing_sword";
}
