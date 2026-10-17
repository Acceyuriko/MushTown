namespace MushTown.Tests;

using System.Globalization;

// A throwaway assets dir with minimal map/mob json (the server reads
// Life/Footholds/VR plus mob stats, so Actions and art stay out). Deleted on dispose.
public sealed class TempAssets : IDisposable
{
    public string Dir { get; }

    public TempAssets()
    {
        Dir = Path.Combine(Path.GetTempPath(), "mushtown-test-" + Guid.NewGuid(), "assets");
        Directory.CreateDirectory(Path.Combine(Dir, "maps"));
        Directory.CreateDirectory(Path.Combine(Dir, "mob"));
    }

    public void AddMap(int map, string lifeJson = "[]", string fhJson = "[]",
        int vrLeft = -1000, int vrTop = -1000, int vrRight = 1000, int vrBottom = 1000) =>
        File.WriteAllText(Path.Combine(Dir, "maps", $"{map}.json"),
            $"{{\"MapId\":{map},\"Life\":{lifeJson},\"Footholds\":{fhJson}," +
            $"\"Info\":{{\"VrLeft\":{vrLeft},\"VrTop\":{vrTop},\"VrRight\":{vrRight},\"VrBottom\":{vrBottom}}}}}");

    public void AddMob(int mob, int hp, double def = 0, int exp = 0,
        int touch = 5, int speed = 40, int aggro = 200, bool boss = false)
    {
        File.WriteAllText(Path.Combine(Dir, "mob", $"{mob}.json"),
            $"{{\"MobId\":{mob},\"MaxHp\":{hp},\"Def\":{def.ToString(CultureInfo.InvariantCulture)},\"Exp\":{exp}," +
            $"\"Touch\":{touch},\"Speed\":{speed},\"Aggro\":{aggro},\"Boss\":{(boss ? "true" : "false")},\"Actions\":[]}}");
        if (boss) // spawned mobs need a table: the boss purse or plain potions
            AddDrops(mob, """[{"Item":2000000,"Chance":1,"Count":2},{"Item":2000003,"Chance":1,"Count":1},{"Item":1442004,"Chance":1,"Count":1}]""", 150, 300);
        else
            AddDrops(mob, """[{"Item":2000000,"Chance":0.1,"Count":1},{"Item":2000003,"Chance":0.04,"Count":1}]""");
    }

    // Drop tables live beside assets/ (repo data, like quests/), so the temp
    // world nests assets one deeper and each test owns its drops sibDir.
    public void AddDrops(int mob, string itemsJson, int mesoMin = 10, int mesoMax = 30)
    {
        string dir = Path.Combine(Path.GetDirectoryName(Dir)!, "drops");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{mob}.json"),
            $"{{\"MobId\":{mob},\"MesoMin\":{mesoMin},\"MesoMax\":{mesoMax},\"Items\":{itemsJson}}}");
    }

    // The server only reads ids, level band, kills, and rewards, so text stays out.
    public void AddQuest(int quest, int mob, int count, int lvMin = 1, int lvMax = 0, int rewardExp = 0)
    {
        Directory.CreateDirectory(Path.Combine(Dir, "quest"));
        File.WriteAllText(Path.Combine(Dir, "quest", $"{quest}.json"),
            $"{{\"QuestId\":{quest},\"Npc0\":1,\"Npc1\":1,\"LvMin\":{lvMin},\"LvMax\":{lvMax}," +
            $"\"Kills\":[{{\"Mob\":{mob},\"Count\":{count}}}],\"RewardExp\":{rewardExp}}}");
    }

    // The server only reads Kind/DmgLo/MobCount/Range, so names and art stay out.
    public void AddSkill(int skill, int range, int mobCount, int kind = 0, int dmgLo = 1)
    {
        Directory.CreateDirectory(Path.Combine(Dir, "skill"));
        File.WriteAllText(Path.Combine(Dir, "skill", $"{skill}.json"),
            $"{{\"SkillId\":{skill},\"Kind\":{kind},\"DmgLo\":{dmgLo},\"Range\":{range},\"MobCount\":{mobCount}}}");
    }

    public void Dispose()
    {
        Directory.Delete(Path.GetDirectoryName(Dir)!, true); // the guid root: assets plus its drops sibDir
    }
}
