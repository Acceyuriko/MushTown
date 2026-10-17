using MushTown.Shared;
using NLayer;
using WzComparerR2.WzLib;

namespace MushTown.Exporter;

// Writes assets/sound/*.wav: short effects (fixed table) plus one music
// track per map BGM (deduplicated). The WZ keeps MP3, which MonoGame
// DesktopGL cannot play, so NLayer decodes to PCM WAV at export time.
public static class SoundExport
{
    // (file key, path under Sound\). Mob Die sources match the Mobs art
    // twins: snails and the spore link to 0100100's, orange has 1210102's,
    // the boss has its own.
    private static readonly (string Key, string Path)[] Sfx = new[]
    {
        ("swing_sword", @"Weapon.img\swordL\Attack"),
        ("swing_pole", @"Weapon.img\poleArm\Attack"),
        ("hit", @"Mob.img\0100100\Damage"),
        ("die_snail", @"Mob.img\0100100\Die"),
        ("die_orange", @"Mob.img\1210102\Die"),
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
    public static bool Run(WzRoot root, Dictionary<int, MapData> maps, string outDir)
    {
        Directory.CreateDirectory(Path.Combine(outDir, "sound"));
        foreach ((string key, string path) in Sfx)
            if (!Save(root, path, Path.Combine(outDir, "sound", key + ".wav"), true)) return false;
        foreach (string bgm in maps.Values.Select(m => m.Info.Bgm).Distinct().OrderBy(b => b))
        {
            var parts = bgm.Split('/');
            string key = SoundKey.FromBgm(bgm);
            if (parts.Length != 2 || key == "")
            {
                Console.WriteLine($"map bgm '{bgm}' is malformed");
                return false;
            }
            if (!Save(root, $@"{parts[0]}.img\{parts[1]}", Path.Combine(outDir, "sound", key + ".wav"), false)) return false;
        }
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
        byte[] wav = ToWav(mp3, trim);
        File.WriteAllBytes(file, wav);
        Console.WriteLine($"sound {Path.GetFileName(file)} {wav.Length / 1024}KB");
        return true;
    }

    // NLayer hands back signed 16-bit PCM at the MP3's own rate/channels;
    // the 44-byte header makes it a WAV the client plays as-is.
    private static byte[] ToWav(byte[] mp3, bool trim)
    {
        using var input = new MemoryStream(mp3);
        using var mpeg = new MpegFile(input);
        using var pcm = new MemoryStream();
        byte[] buf = new byte[64 * 1024];
        int n;
        while ((n = mpeg.ReadSamplesInt16(buf, 0, buf.Length)) > 0) pcm.Write(buf, 0, n);
        byte[] data = pcm.ToArray();
        if (trim) data = TrimLeadingSilence(data, mpeg.Channels);
        using var wav = new MemoryStream();
        using (var w = new BinaryWriter(wav, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // "RIFF"
            w.Write(data.Length + 36);
            w.Write(new byte[] { 0x57, 0x41, 0x56, 0x45 }); // "WAVE"
            w.Write(new byte[] { 0x66, 0x6d, 0x74, 0x20 }); // "fmt "
            w.Write(16);
            w.Write((short)1); // PCM
            w.Write((short)mpeg.Channels);
            w.Write(mpeg.SampleRate);
            w.Write(mpeg.SampleRate * mpeg.Channels * 2);
            w.Write((short)(mpeg.Channels * 2));
            w.Write((short)16);
            w.Write(new byte[] { 0x64, 0x61, 0x74, 0x61 }); // "data"
            w.Write(data.Length);
            w.Write(data);
        }
        return wav.ToArray();
    }

    // MP3 encoder padding decodes to ~60ms of near-silence up front. One-shot
    // effects feel late with it (hover especially), so trim leading frames
    // under -44dB. Music keeps its bytes (loop timing stays untouched).
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
