using System.Runtime.InteropServices;
using MushTown.Shared;
using NAudio.Wave;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Writes assets/sound/*.wav: short effects (fixed table), one music track
// per map BGM (deduplicated) and the login theme. The WZ keeps MP3, which
// MonoGame DesktopGL cannot play, so the Windows Media Foundation decoder
// turns it into PCM WAV at export time.
public static class SoundExport
{
    // (file key, path under Sound\). The die sources match the Mobs art
    // twins: the snail uses 0100100's Die node, the orange mushroom 1210102's,
    // the pig 1210100's and the boss its own 6130101. The blue snail 0100101,
    // spore 0120100, stump 0130100, red snail 0130101 and slime 0210100 have
    // no Die node, so they fall back to the snail's.
    private static readonly (string Key, string Path)[] Sfx = new[]
    {
        ("swing_sword", @"Weapon.img\swordL\Attack"),
        ("swing_pole", @"Weapon.img\poleArm\Attack"),
        ("hit", @"Mob.img\0100100\Damage"),
        ("die_snail", @"Mob.img\0100100\Die"),
        ("die_orange", @"Mob.img\1210102\Die"),
        ("die_pig", @"Mob.img\1210100\Die"),
        ("die_boss", @"Mob.img\6130101\Die"),
        ("pickup", @"Game.img\PickUpItem"),
        ("levelup", @"Game.img\LevelUp"),
        ("portal", @"Game.img\Portal"),
        ("jump", @"Game.img\Jump"),
        ("click", @"UI.img\BtMouseClick"),
        ("over", @"UI.img\BtMouseOver"),
        ("shopbuy", @"UI.img\BuyShopItem"),
        ("questclear", @"Game.img\QuestClear"),
        ("buff", @"Game.img\Buff"),
    };

    // The theme over the login flow: Sound\BgmUI.img\Title.
    private const string LoginPath = @"BgmUI.img\Title";

    public static bool Run(WzRoot root, IEnumerable<string> bgms, string outDir)
    {
        Directory.CreateDirectory(Path.Combine(outDir, "sound"));
        foreach ((string key, string path) in Sfx)
            if (!Save(root, path, Path.Combine(outDir, "sound", key + ".wav"), true)) return false;
        var tracks = new Dictionary<string, string>(); // file key -> the WZ track
        foreach (string bgm in bgms.Distinct().OrderBy(b => b))
        {
            string key = SoundKey.FromBgm(bgm);
            if (key == "")
            {
                Console.WriteLine($"map bgm '{bgm}' is malformed");
                return false;
            }
            if (key == SoundKey.Login)
            {
                Console.WriteLine($"map bgm '{bgm}' wants the file '{key}.wav', the login theme has it");
                return false;
            }
            var parts = bgm.Split('/');
            string path = $@"{parts[0]}.img\{parts[1]}";
            if (tracks.TryGetValue(key, out string? other))
            {
                if (!string.Equals(other, path, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"the tracks '{other}' and '{path}' both want the file '{key}.wav'");
                    return false;
                }
                continue; // the same song written two ways: one file
            }
            tracks[key] = path;
        }
        foreach ((string key, string path) in tracks)
            if (!Save(root, path, Path.Combine(outDir, "sound", key + ".wav"), false)) return false;
        // The menus before the game start play the login theme.
        if (!Save(root, LoginPath, Path.Combine(outDir, "sound", SoundKey.Login + ".wav"), false)) return false;
        return true;
    }

    private static bool Save(WzRoot root, string path, string file, bool trim)
    {
        var node = WzNodeUtil.ResolveUol(root.Find(@"Sound\" + path));
        var sound = node?.Value as Wz_Sound;
        if (sound == null)
        {
            Console.WriteLine($"sound {path} not found");
            return false;
        }
        if (sound.SoundType != Wz_SoundType.Mp3)
        {
            Console.WriteLine($"sound {path} is {sound.SoundType}, not Mp3");
            return false;
        }
        byte[]? mp3 = sound.ExtractSound();
        if (mp3 == null || mp3.Length == 0)
        {
            Console.WriteLine($"sound {path} has no data");
            return false;
        }
        byte[] wav;
        try
        {
            wav = ToWav(mp3, trim);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or InvalidDataException) // the decoder failed on this file
        {
            Console.WriteLine($"sound {path} cannot be decoded: {ex.Message}");
            return false;
        }
        File.WriteAllBytes(file, wav);
        Console.WriteLine($"sound {Path.GetFileName(file)} {wav.Length / 1024}KB");
        return true;
    }

    // The Windows Media Foundation decoder turns the MP3 into PCM. The
    // managed NLayer decoder was wrong on these files: it lost the bit
    // reservoir and filled parts of a track with noise.
    private static byte[] ToWav(byte[] mp3, bool trim)
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"mushtown-{Guid.NewGuid():N}.mp3");
        try
        {
            File.WriteAllBytes(tmp, mp3);
            using var reader = new MediaFoundationReader(tmp);
            using var pcm = new MemoryStream();
            reader.CopyTo(pcm);
            byte[] data = pcm.ToArray();
            if (trim) data = TrimLeadingSilence(data, reader.WaveFormat.Channels);
            return Wav(data, reader.WaveFormat.SampleRate, reader.WaveFormat.Channels);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    // Our own 44-byte header: the client reads the channels and the rate
    // from it and plays the rest as PCM16.
    private static byte[] Wav(byte[] data, int rate, int channels)
    {
        using var wav = new MemoryStream();
        using (var w = new BinaryWriter(wav, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // "RIFF"
            w.Write(data.Length + 36);
            w.Write(new byte[] { 0x57, 0x41, 0x56, 0x45 }); // "WAVE"
            w.Write(new byte[] { 0x66, 0x6d, 0x74, 0x20 }); // "fmt "
            w.Write(16);
            w.Write((short)1); // PCM
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(new byte[] { 0x64, 0x61, 0x74, 0x61 }); // "data"
            w.Write(data.Length);
            w.Write(data);
        }
        return wav.ToArray();
    }

    // MP3 encoder padding decodes to ~25ms of near-silence up front (1105
    // samples). One-shot effects feel late with it (hover especially), so
    // trim leading frames under -44dB. Music keeps its bytes (loop timing
    // stays untouched).
    private static byte[] TrimLeadingSilence(byte[] data, int channels)
    {
        int frame = channels * 2, at = 0;
        while (at + frame <= data.Length)
        {
            bool quiet = true;
            for (int c = 0; c < channels; c++)
                if (Math.Abs(BitConverter.ToInt16(data, at + c * 2)) >= 200) { quiet = false; break; }
            if (!quiet) break;
            at += frame;
        }
        return at == 0 ? data : data[at..];
    }
}
