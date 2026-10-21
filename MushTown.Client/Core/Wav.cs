using Microsoft.Xna.Framework.Audio;

namespace MushTown.Client;

// Our own 44-byte header (SoundExport.Wav writes it): channels at 22,
// rate at 24, PCM16 after. A missing file throws loud, like every asset.
public static class Wav
{
    public static SoundEffect Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"missing exported wav: {Path.GetFullPath(path)}", path);
        byte[] wav = File.ReadAllBytes(path);
        int channels = BitConverter.ToInt16(wav, 22);
        int rate = BitConverter.ToInt32(wav, 24);
        return new SoundEffect(wav[44..], rate, (AudioChannels)channels);
    }
}
