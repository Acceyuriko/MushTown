using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// The Loot act: the table rolls once, bonus lines append, the world places
// every payout as one row. The hook takes (mob, attacker) so quest-scripted
// drops can pay the killer; v1 sets no hook (no quest items yet).
[TestClass]
public sealed class LootTests
{
    private const string Flat = """[{"X1":0,"Y1":100,"X2":200,"Y2":100,"Layer":0}]""";

    // Bonus lines land after the table rolls, in the same row.
    [TestMethod]
    public void Bonus_LinesLandAfterTableRolls()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        assets.AddDrops(100000, "[]");
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.BonusDrops = (_, _) => new List<(int kind, int id, int count, long amount)>
            { (DropKind.Item, 2000000, 3, 0) };

        var hits = world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        Assert.AreEqual(0, hits[0].HpLeft);
        var fresh = world.TakeDrops();
        Assert.AreEqual(DropKind.Mesos, fresh[0].Kind); // the table rolls first
        var bonus = fresh[^1];
        Assert.AreEqual(DropKind.Item, bonus.Kind);
        Assert.AreEqual(2000000, bonus.Id);
        Assert.AreEqual(3, bonus.Count);
        Assert.AreEqual(111.5f, bonus.X); // 23px copper meso + 24px item, centred on 100
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
            return new List<(int kind, int id, int count, long amount)>();
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
            return new List<(int kind, int id, int count, long amount)>();
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
            return new List<(int kind, int id, int count, long amount)>();
        };

        world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        Assert.AreEqual(0, heard);
    }

    // A player drop uses the same ground path: one uid, one count, at the feet.
    [TestMethod]
    public void DropItem_CreatesOneGroundDropAtFeet()
    {
        using var assets = new TempAssets();
        assets.AddMap(7);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var drop = world.DropItem(7, 100, 120, "use", DropKind.Item, 2000002, 5);
        Assert.AreEqual(100f, drop.X);
        Assert.AreEqual(120f, drop.Y); // no foothold: lands at the feet height
        Assert.AreEqual(100f, drop.FromX);
        Assert.AreEqual(120f, drop.FromY);
        Assert.AreEqual("use", drop.Tab);
        Assert.AreEqual(5, drop.Count);
        Assert.AreEqual(1, world.DropsOn(7).Count);
        Assert.AreEqual(drop.Uid, world.TakeDrops()[0].Uid);
    }

    // A dropped meso pile keeps its long amount and has no bag to return to.
    [TestMethod]
    public void DropMesos_CreatesOneGroundDropWithAmount()
    {
        using var assets = new TempAssets();
        assets.AddMap(7);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var drop = world.DropItem(7, 100, 120, "", DropKind.Mesos, 0, 0, 10_000_000_000L);
        Assert.AreEqual(DropKind.Mesos, drop.Kind);
        Assert.AreEqual(10_000_000_000L, drop.Amount);
        Assert.AreEqual("", drop.Tab);
        Assert.AreEqual(1, world.DropsOn(7).Count);
        Assert.AreEqual(drop.Uid, world.TakeDrops()[0].Uid);
    }

    // Meso art hangs right of its anchor by the WZ origin; at the right wall
    // the picture edge is clamped too, not just the anchor.
    [TestMethod]
    public void DropMesos_AtRightVrEdge_KeepsPictureInside()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, vrLeft: 0, vrRight: 100);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);

        var bag = world.DropItem(7, 100, 120, "", DropKind.Mesos, 0, 0, 1000);
        var paper = world.DropItem(7, 100, 120, "", DropKind.Mesos, 0, 0, 100);
        var gold = world.DropItem(7, 100, 120, "", DropKind.Mesos, 0, 0, 50);
        var copper = world.DropItem(7, 100, 120, "", DropKind.Mesos, 0, 0, 10);

        Assert.AreEqual(68f, bag.X); // 32-wide picture: 68..100
        Assert.AreEqual(68f, paper.X); // 31-wide picture at x + 1: 69..100
        Assert.AreEqual(72f, gold.X); // 23-wide picture at x + 5: 77..100
        Assert.AreEqual(72f, copper.X);
    }

    // This only pins Inventory.Remove + MobWorld.DropItem together; it does not
    // claim to run the client's quantity popup or DropFromBag UI wiring.
    [TestMethod]
    public void DropItem_FiveToThreeLeavesTwoAndOnePile()
    {
        using var assets = new TempAssets();
        assets.AddMap(7);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var chara = new CharaSlot { Use = new Dictionary<int, int> { [2000002] = 5 } };
        Inventory.Ensure(chara);

        Assert.IsTrue(Inventory.Remove(chara, "use", 2000002, 3));
        var drop = world.DropItem(7, 100, 120, "use", DropKind.Item, 2000002, 3);

        Assert.AreEqual(2, Inventory.Count(chara, "use", 2000002));
        var drops = world.DropsOn(7);
        Assert.AreEqual(1, drops.Count);
        Assert.AreEqual(drop.Uid, drops[0].Uid);
        Assert.AreEqual(3, drops[0].Count);
    }

    // The row is centred on the monster and every icon keeps its own width.
    [TestMethod]
    public void DropLayout_SpreadsByIconWidth()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Flat);
        assets.AddMob(100000, 1);
        assets.AddDrops(100000, "[]");
        assets.AddItem(2000002, 20);
        assets.AddItem(2000006, 30);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.BonusDrops = (_, _) => new List<(int kind, int id, int count, long amount)>
            { (DropKind.Item, 2000002, 1, 0), (DropKind.Item, 2000006, 1, 0) };

        world.Strike(7, 60, 100, true, new Random(0), Progression.Beginner);
        var fresh = world.TakeDrops();
        var a = fresh.Find(d => d.Id == 2000002)!;
        var b = fresh.Find(d => d.Id == 2000006)!;
        Assert.AreEqual(96.5f, a.X); // row 73 wide: meso 23, then 20 and 30
        Assert.AreEqual(121.5f, b.X); // second centre 96.5 + 10 + 15
        Assert.AreEqual(100f, a.Y);
    }

    [TestMethod]
    public void DropLayout_ClampsRowToLeftVrEdge()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":10,"Cy":100}]""", Flat, vrLeft: 0, vrRight: 1000);
        assets.AddMob(100000, 1);
        assets.AddDrops(100000, "[]");
        assets.AddItem(2000002, 20);
        assets.AddItem(2000006, 30);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.BonusDrops = (_, _) => new List<(int kind, int id, int count, long amount)>
            { (DropKind.Item, 2000002, 1, 0), (DropKind.Item, 2000006, 1, 0) };

        world.Strike(7, 10, 100, true, new Random(0), Progression.Beginner);
        var fresh = world.TakeDrops();
        var a = fresh.Find(d => d.Id == 2000002)!;
        var b = fresh.Find(d => d.Id == 2000006)!;
        // The table rolls a 23-wide meso first. Its art hangs 5px left of the
        // anchor, so the row starts at vrLeft + 5 and the anchor lands at 0.
        Assert.AreEqual(0f, fresh[0].X); // the meso anchor clamps at vrLeft
        Assert.AreEqual(38f, a.X); // 2000002 follows the 23-wide meso
        Assert.AreEqual(63f, b.X);
    }

    [TestMethod]
    public void DropLayout_ClampsRowToRightVrEdge()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":1000,"Cy":100}]""", vrLeft: 0, vrRight: 1000);
        assets.AddMob(100000, 1);
        assets.AddDrops(100000, "[]");
        assets.AddItem(2000002, 20);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.BonusDrops = (_, _) => new List<(int kind, int id, int count, long amount)>
            { (DropKind.Mesos, 0, 1, 100), (DropKind.Mesos, 0, 1, 1000), (DropKind.Item, 2000002, 1, 0) };

        world.Strike(7, 1000, 100, true, new Random(0), Progression.Beginner);
        var fresh = world.TakeDrops();
        Assert.AreEqual(4, fresh.Count); // table copper + paper, bag, item
        Assert.AreEqual(889f, fresh[0].X); // copper art 894..917
        Assert.AreEqual(916f, fresh[1].X); // paper art 917..948
        Assert.AreEqual(948f, fresh[2].X); // bag art 948..980
        Assert.AreEqual(990f, fresh[3].X); // item art 980..1000
        foreach (var d in fresh)
        {
            if (d.Kind != DropKind.Mesos) continue;
            var (left, right) = MesoArt(d);
            Assert.IsTrue(left >= 0f && right <= 1000f);
        }
    }

    [TestMethod]
    public void DropLayout_WideRowStaysInsideVr()
    {
        using var assets = new TempAssets();
        assets.AddMap(7, """[{"MobId":100000,"X":50,"Cy":100}]""", Flat, vrLeft: 0, vrRight: 100);
        assets.AddMob(100000, 1);
        assets.AddDrops(100000, "[]");
        assets.AddItem(2000002, 60);
        assets.AddItem(2000006, 60);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        world.BonusDrops = (_, _) => new List<(int kind, int id, int count, long amount)>
            { (DropKind.Item, 2000002, 1, 0), (DropKind.Item, 2000006, 1, 0) };

        world.Strike(7, 50, 100, true, new RightEdgeRandom(), Progression.Beginner);
        var fresh = world.TakeDrops();
        Assert.AreEqual(3, fresh.Count); // meso + two items, none dropped
        Assert.IsTrue(fresh.All(d => d.X >= 0f && d.X <= 100f));
        var meso = fresh.Find(d => d.Kind == DropKind.Mesos)!;
        var (left, right) = MesoArt(meso);
        Assert.IsTrue(left >= 0f && right <= 100f);
    }

    private static (float left, float right) MesoArt(MobWorld.Drop d)
    {
        float w = d.Amount >= 1000 ? 32f : d.Amount >= 100 ? 31f : 23f;
        float ox = d.Amount >= 1000 ? 0f : d.Amount >= 100 ? -1f : -5f;
        return (d.X - ox, d.X - ox + w);
    }

    // Every NextDouble lands at the right edge, so the wide-row test always
    // exercises the picture clamp instead of depending on a lucky seed.
    private sealed class RightEdgeRandom : Random
    {
        public override double NextDouble() => 0.99;
    }
}
