using System.Text.Json;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The Vanishing Journey pair (town 450001000 + first field 450001010): the exported
// map files, their one door pair, and the Happy Erda that lives there. Like the quest
// chain sim, these read the exported assets (run the export first).
[TestClass]
public sealed class VanishingJourneyTests
{
    private const int Town = 450001000, Field = 450001010, HappyErda = 8641000;

    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static MapData LoadMap(int id)
    {
        string path = Path.Combine(Root, "assets", "maps", $"{id}.json");
        Assert.IsTrue(File.Exists(path), $"needs exported assets (run: dotnet run --project MushTown.Exporter -- export): {path}");
        return JsonSerializer.Deserialize<MapData>(File.ReadAllText(path))!;
    }

    // The only live door of the town is PV02 and the only one of the field is PV00: no door leads
    // to a map we do not have. The mirror pads inside the field (PH00/PH01) stay on the field.
    [TestMethod]
    public void Doors_TownAndFieldLinkOnlyToEachOther()
    {
        var town = LoadMap(Town);
        var field = LoadMap(Field);

        var townDoors = town.Portals.Where(p => p.Tm != PortalRules.NoTarget).ToList();
        Assert.AreEqual(1, townDoors.Count);
        Assert.AreEqual(("PV02", Field, "PV00"), (townDoors[0].Pn, townDoors[0].Tm, townDoors[0].Tn));
        Assert.IsTrue(townDoors[0].Frames.Count > 0); // a live door has art

        var fieldDoors = field.Portals.Where(p => p.Tm != PortalRules.NoTarget && p.Tm != Field).ToList();
        Assert.AreEqual(1, fieldDoors.Count);
        Assert.AreEqual(("PV00", Town, "PV02"), (fieldDoors[0].Pn, fieldDoors[0].Tm, fieldDoors[0].Tn));
        Assert.IsTrue(field.Portals.Where(p => p.Tm == Field).All(p => field.Portals.Any(q => q.Pn == p.Tn)));
    }

    // Spawn, return map, Arcane Power gate, minimap and the placements of the two maps.
    [TestMethod]
    public void Maps_CarryTheWzNumbers()
    {
        var town = LoadMap(Town);
        var field = LoadMap(Field);

        Assert.AreEqual((-272, -7), (town.Info.SpawnX, town.Info.SpawnY));
        Assert.AreEqual(0, town.Info.BarrierArc);
        Assert.AreEqual(Town, town.Info.ReturnMap);
        Assert.AreEqual(30, field.Info.BarrierArc);
        Assert.AreEqual(Town, field.Info.ReturnMap);
        Assert.IsTrue(town.MiniMap.Image != "" && field.MiniMap.Image != "");

        Assert.AreEqual(22, field.Life.Count);
        Assert.IsTrue(field.Life.All(l => l.MobId == HappyErda));
        Assert.AreEqual(0, town.Life.Count);
        Assert.IsTrue(town.Npcs.Any(n => n.NpcId == 3003100 && n.X == -991 && n.Cy == 60)); // Lento
        Assert.IsTrue(town.Npcs.Any(n => n.NpcId == 3003101 && n.X == -1458 && n.Cy == 60)); // Largo
        Assert.AreEqual(0, field.Npcs.Count);
    }

    // The real world loads the field (its drop table too), spawns 22 Happy Erda, and one
    // dies to strikes and pays mesos in the table's range.
    [TestMethod]
    public void HappyErda_SpawnsDiesAndPaysMesos()
    {
        var world = new MobWorld(Path.Combine(Root, "assets"));
        world.EnterMap(Field);
        var mobs = world.MobsOn(Field);
        Assert.AreEqual(22, mobs.Count);
        Assert.AreEqual(44_640_000, mobs[0].MaxHp);

        var stats = JsonSerializer.Deserialize<MobData>(File.ReadAllText(Path.Combine(Root, "assets", "mob", $"{HappyErda}.json")))!;
        Assert.AreEqual(200, stats.Level);
        Assert.AreEqual(44_640_000, stats.MaxHp);
        Assert.AreEqual(98_708, stats.Exp);
        Assert.AreEqual(12_825, stats.Touch);
        Assert.AreEqual(70, stats.Speed);
        Assert.IsFalse(stats.Boss);

        var big = new Build(1_000_000, 1_000_000, 1_000_000_000, 0, 0, 0, Level: 200);
        var rng = new Random(1);
        long left = mobs[0].Hp;
        for (int i = 0; i < 1000 && left > 0; i++)
            left = world.Strike(Field, mobs[0].X, mobs[0].Y, true, rng, big).First(h => h.Uid == mobs[0].Uid).HpLeft;
        Assert.AreEqual(0, left);
        var pay = world.TakeDrops().First(d => d.Kind == DropKind.Mesos);
        Assert.IsTrue(pay.Amount >= 1200 && pay.Amount <= 1800);
    }

    // The table also pays potions (the same design chance as the other mobs).
    [TestMethod]
    public void HappyErda_TablePaysPotions()
    {
        var data = JsonSerializer.Deserialize<DropData>(File.ReadAllText(Path.Combine(Root, "drops", $"{HappyErda}.json")))!;
        var table = new DropTable(data);
        var ids = Enumerable.Range(0, 200).SelectMany(seed => table.Roll(new Random(seed)))
            .Where(d => d.kind == DropKind.Item).Select(d => d.id).Distinct().ToList();
        CollectionAssert.AreEquivalent(new[] { 2000002, 2000006 }, ids);
    }
}
