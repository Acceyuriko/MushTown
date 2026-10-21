using Microsoft.Xna.Framework.Audio;
using MushTown.Shared;

namespace MushTown.Client;

// The music: one looped track at a time. It is a component on the audio
// object, so the game only hands it a track key; the track keeps playing
// until another one comes. No per-frame work: the world tree only ticks
// in the game screen, so a fade would need another home.
public sealed class BgmPlayer : Component
{
    private readonly string _soundDir;
    private readonly bool _silent; // shots pose silent: no music blast
    private SoundEffect? _effect;
    private SoundEffectInstance? _instance;
    private string _playing = "";
    private int _volume = 80;

    public BgmPlayer(string soundDir, bool silent)
    {
        _soundDir = soundDir;
        _silent = silent;
    }

    // The music volume (0-100). The options window drives it.
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            if (_instance != null) _instance.Volume = _volume / 100f;
        }
    }

    // The track for a key. The same key keeps playing, a new key replaces
    // the old one. True when the track changed.
    public bool Play(string key)
    {
        if (key == _playing) return false;
        _playing = key;
        _instance?.Stop();
        _instance?.Dispose();
        _effect?.Dispose(); // the instance does not own its buffer: drop it too
        _instance = null;
        _effect = null;
        if (_silent) return true;
        _effect = Wav.Load(Path.Combine(_soundDir, key + ".wav"));
        _instance = _effect.CreateInstance();
        _instance.IsLooped = true;
        _instance.Volume = _volume / 100f;
        _instance.Play();
        return true;
    }
}
