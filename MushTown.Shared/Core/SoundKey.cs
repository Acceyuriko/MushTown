namespace MushTown.Shared;

// File keys under assets/sound/ (*.wav). The exporter writes them, the
// client loads them; a missing file fails loud at client boot.
public static class SoundKey
{
    // Every short effect the client preloads.
    public static readonly string[] Sfx = new[]
    {
        "swing_sword", "swing_pole", "hit", "die_snail", "die_orange",
        "die_boss", "pickup", "levelup", "portal", "jump", "click", "over",
        "shopbuy", "questclear", "buff",
    };

    // Map BGM: the WZ info/bgm looks like "Bgm58/The Beginnig of The
    // Adventure". "" means malformed (the exporter rejects it).
    public static string FromBgm(string bgm)
    {
        var parts = bgm.Split('/');
        if (parts.Length != 2) return "";
        string img = Keep(parts[0]), track = Keep(parts[1]);
        if (img == "" || track == "") return "";
        return img + "_" + track;
    }

    private static string Keep(string s) =>
        new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    // Which death cry a mob plays. Orange mushrooms and the boss have own
    // Die nodes; every other mob links to the snail's (0100100, verified W18).
    public static string MobDie(int mobId, bool boss) =>
        boss ? "die_boss" : mobId == 100004 ? "die_orange" : "die_snail";
}
