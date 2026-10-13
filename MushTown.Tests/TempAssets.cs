namespace MushTown.Tests;

using System.Globalization;

// A throwaway assets dir with minimal map/mob json (the server reads
// Life/Footholds/VR plus mob stats, so Actions and art stay out). Deleted on dispose.
public sealed class TempAssets : IDisposable
{
    public string Dir { get; }

    public TempAssets()
    {
        Dir = Path.Combine(Path.GetTempPath(), "mushtown-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(Dir, "maps"));
        Directory.CreateDirectory(Path.Combine(Dir, "mob"));
    }

    public void AddMap(int map, string lifeJson = "[]", string fhJson = "[]",
        int vrLeft = -1000, int vrTop = -1000, int vrRight = 1000, int vrBottom = 1000) =>
        File.WriteAllText(Path.Combine(Dir, "maps", $"{map}.json"),
            $"{{\"MapId\":{map},\"Life\":{lifeJson},\"Footholds\":{fhJson}," +
            $"\"Info\":{{\"VrLeft\":{vrLeft},\"VrTop\":{vrTop},\"VrRight\":{vrRight},\"VrBottom\":{vrBottom}}}}}");

    public void AddMob(int mob, int hp, double def = 0, int exp = 0,
        int touch = 5, int speed = 40, int aggro = 200) =>
        File.WriteAllText(Path.Combine(Dir, "mob", $"{mob}.json"),
            $"{{\"MobId\":{mob},\"MaxHp\":{hp},\"Def\":{def.ToString(CultureInfo.InvariantCulture)},\"Exp\":{exp}," +
            $"\"Touch\":{touch},\"Speed\":{speed},\"Aggro\":{aggro},\"Actions\":[]}}");

    // The server only reads Kind/DmgLo/MobCount/Range, so names and art stay out.
    public void AddSkill(int skill, int range, int mobCount, int kind = 0, int dmgLo = 1)
    {
        Directory.CreateDirectory(Path.Combine(Dir, "skill"));
        File.WriteAllText(Path.Combine(Dir, "skill", $"{skill}.json"),
            $"{{\"SkillId\":{skill},\"Kind\":{kind},\"DmgLo\":{dmgLo},\"Range\":{range},\"MobCount\":{mobCount}}}");
    }

    public void Dispose()
    {
        Directory.Delete(Dir, true);
    }
}
