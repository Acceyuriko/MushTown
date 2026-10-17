using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The Loot act: the table rolls once, bonus lines append, the world places
// every payout. The hook takes (mob, attacker) so quest-scripted drops can
// pay the killer; v1 sets no hook (no quest items yet).
[TestClass]
public sealed class LootTests
{
    private const string Flat = """[{"X1":0,"Y1":100,"X2":200,"Y2":100,"Layer":0}]""";

    // Bonus lines land after the table rolls, placed like them (clamped +
    // grounded at corpse + dx).
    [TestMethod]
    public void Bonus_LinesLandAfterTableRolls()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.BonusDrops = (_, _) => new List<(int kind, int id, int count, float dx)>
            { (DropKind.Item, 2000000, 3, 35) };

        var hits = world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        Assert.AreEqual(0, hits[0].HpLeft);
        var fresh = world.TakeDrops();
        Assert.AreEqual(DropKind.Mesos, fresh[0].Kind); // the table rolls first
        var bonus = fresh[^1];
        Assert.AreEqual(DropKind.Item, bonus.Kind);
        Assert.AreEqual(2000000, bonus.Id);
        Assert.AreEqual(3, bonus.Count);
        Assert.AreEqual(135f, bonus.X);
        Assert.AreEqual(100f, bonus.Y);
    }

    // The hook hears who killed what: the wire carries (mob, attacker).
    [TestMethod]
    public void Bonus_ReceivesMobAndAttacker()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        (int mob, int attacker) heard = (-1, -1);
        world.BonusDrops = (mob, attacker) =>
        {
            heard = (mob, attacker);
            return new List<(int kind, int id, int count, float dx)>();
        };

        world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner, attacker: 7);
        Assert.AreEqual((100000, 7), heard);
    }

    // The skill path carries the killer too (Slash always lands >= 1: the
    // 1-hp snail dies and the hook hears it through HitOne).
    [TestMethod]
    public void Bonus_SkillPathCarriesAttacker()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        assets.AddSkill(SkillData.SlashBlast, 100, 6);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        (int mob, int attacker) heard = (-1, -1);
        world.BonusDrops = (mob, attacker) =>
        {
            heard = (mob, attacker);
            return new List<(int kind, int id, int count, float dx)>();
        };

        var hits = world.SkillStrike(7, 100, 100, true, new Random(0), Progression.Beginner, SkillData.SlashBlast, 150, attacker: 7);
        Assert.AreEqual(0, hits[0].HpLeft);
        Assert.AreEqual((100000, 7), heard);
    }

    // No attacker passed (solo, shots): the hook hears the local player 0.
    [TestMethod]
    public void Bonus_DefaultAttackerIsLocalZero()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        int heard = -1;
        world.BonusDrops = (_, attacker) =>
        {
            heard = attacker;
            return new List<(int kind, int id, int count, float dx)>();
        };

        world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        Assert.AreEqual(0, heard);
    }
}
