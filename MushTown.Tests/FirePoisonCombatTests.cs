using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Fire/Poison 1st job, part 2: Energy Bolt (the rectangle, the segments, the element), Magic Guard,
// Elemental Weakness and the mob status list under it.
[TestClass]
public class FirePoisonCombatTests
{
    private const int Bolt = WzSkillData.EnergyBolt;
    private const string Floor = """[{"X1":-1000,"Y1":100,"X2":1000,"Y2":100,"Layer":0}]""";

    // Energy Bolt as 200.img writes it at level 20 (box lt/rb, 4 segments, 4 mobs, reach 340). With
    // targetBox false the box hangs on the caster (the generic WZ rule), with the asymmetric box given.
    private static WzSkillData BoltData(bool targetBox = true, string elem = "", int mobCount = 4, int segments = 4,
        int[]? lt = null, int[]? rb = null)
    {
        var data = new WzSkillData { SkillId = Bolt, MaxLevel = 20, Kind = SkillData.KindActive, ElemAttr = elem };
        data.Common["mpCon"] = SkillFormula.Levels("24", 20);
        data.Common["damage"] = SkillFormula.Levels("78", 20);
        data.Common["attackCount"] = SkillFormula.Levels(segments.ToString(), 20);
        data.Common["mobCount"] = SkillFormula.Levels(mobCount.ToString(), 20);
        data.Common["range"] = SkillFormula.Levels("340", 20);
        data.Vectors["lt"] = lt ?? new[] { -120, -75 };
        data.Vectors["rb"] = rb ?? new[] { 120, 75 };
        if (targetBox) data.Info["rectBasedOnTarget"] = "1";
        return data;
    }

    // 2000007 as 200.img writes it: +20% magic damage for 5 s, 15% chance.
    private static WzSkillData WeaknessData()
    {
        var data = new WzSkillData { SkillId = WzSkillData.ElementalWeakness, MaxLevel = 1, Kind = SkillData.KindPassive };
        data.Common["x"] = SkillFormula.Levels("20", 1);
        data.Common["prop"] = SkillFormula.Levels("15", 1);
        data.Common["time"] = SkillFormula.Levels("5", 1);
        return data;
    }

    // A Lv.200 mage's build with round numbers: INT 1000, LUK 100, magic attack 200, a short staff (1.20, mastery 20%).
    private static readonly Build Mage = new(1000, 100, 200, 0, 0, 0, Level: 200, WeaponType: (int)WeaponType.ShortStaff);

    // One world: mobs of 1,000,000,000 HP at the given (x, y) feet, level 200, DEF 10%.
    private static MobWorld World(TempAssets assets, params (int x, int y)[] mobs)
    {
        assets.AddMap(7, "[" + string.Join(",", mobs.Select(m => $$"""{"MobId":100000,"X":{{m.x}},"Cy":{{m.y}}}""")) + "]", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        return world;
    }

    private static int[] UidsHit(MobWorld world, List<MobWorld.Hit> hits) =>
        hits.Select(h => h.Uid).Distinct().Select(uid => (int)world.MobsOn(7).First(s => s.Uid == uid).X).OrderBy(x => x).ToArray();

    // The WZ box is written for a caster that faces left: a caster that faces right mirrors x. The edges are inside.
    [TestMethod]
    public void InRect_MirroredByFacing_EdgesInside()
    {
        var box = BoltData(false, lt: new[] { -200, -50 }, rb: new[] { 30, 50 });
        Assert.IsTrue(box.InRect(-200, 0, false));
        Assert.IsFalse(box.InRect(-201, 0, false));
        Assert.IsTrue(box.InRect(30, 0, false));
        Assert.IsFalse(box.InRect(31, 0, false));
        Assert.IsTrue(box.InRect(0, -50, false));
        Assert.IsFalse(box.InRect(0, -51, false));
        Assert.IsTrue(box.InRect(0, 50, false));
        Assert.IsFalse(box.InRect(0, 51, false));
        Assert.IsTrue(box.InRect(200, 0, true)); // facing right: the same box, mirrored
        Assert.IsFalse(box.InRect(201, 0, true));
        Assert.IsTrue(box.InRect(-30, 0, true));
        Assert.IsFalse(box.InRect(-31, 0, true));
        Assert.IsFalse(box.InRect(-200, 0, true)); // the left edge of a left-facing caster is not hit by a right-facing one
    }

    // A box on the caster (no rectBasedOnTarget): the mobs inside it are hit, facing left and right see mirrored sides.
    [TestMethod]
    public void WzStrike_CasterBox_FacesBothWays()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData(false, lt: new[] { -200, -50 }, rb: new[] { 30, 50 }));
        var world = World(assets, (-201, 100), (-200, 100), (-31, 100), (30, 100), (31, 100), (200, 100), (201, 100), (0, 151));
        var left = world.SkillStrike(7, 0, 100, false, new Random(1), Mage, Bolt, 78);
        CollectionAssert.AreEqual(new[] { -200, -31, 30 }, UidsHit(world, left)); // (0, 151) is 51 px below the box
        var right = world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Bolt, 78);
        CollectionAssert.AreEqual(new[] { 30, 31, 200 }, UidsHit(world, right));
    }

    // Energy Bolt: the bolt flies to the nearest mob ahead within the reach (340), then the box (240 x 150) hangs on
    // THAT mob. A mob in the box counts even when it stands behind the caster; a mob past the box edge is unhurt.
    [TestMethod]
    public void WzStrike_TargetBox_HangsOnTheNearestMobAhead()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        // caster at x 0 facing right. Target: x 100. In its box: x 220 (edge), x -20 (behind the caster).
        // Out: x 221 (one past), x 100 on another row 76 px up (one past).
        var world = World(assets, (100, 100), (220, 100), (-20, 100), (221, 100), (100, 24));
        var hits = world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Bolt, 78);
        CollectionAssert.AreEqual(new[] { -20, 100, 220 }, UidsHit(world, hits));
        Assert.AreEqual(100f, world.MobsOn(7).First(s => s.Uid == hits[0].Uid).X); // the target is hit first
    }

    // The same cast facing left: the target is the nearest mob on the left; the box hangs on it.
    [TestMethod]
    public void WzStrike_TargetBox_FacingLeft()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (-100, 100), (-220, 100), (20, 100), (-221, 100), (300, 100));
        var hits = world.SkillStrike(7, 0, 100, false, new Random(1), Mage, Bolt, 78);
        CollectionAssert.AreEqual(new[] { -220, -100, 20 }, UidsHit(world, hits)); // x 300 is on the other side: not the target, not in the box
    }

    // No mob ahead within the reach: nothing is hit (the bolt has no target). Past 340, behind the caster, and
    // above the box height all count as no target.
    [TestMethod]
    public void WzStrike_TargetBox_NoTarget_NoHits()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (341, 100), (-11, 100), (100, 24));
        Assert.AreEqual(0, world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Bolt, 78).Count);
        // the reach edge is in: a mob at exactly 340 is the target
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(BoltData());
        var world2 = World(assets2, (340, 100));
        Assert.AreEqual(4, world2.SkillStrike(7, 0, 100, true, new Random(1), Mage, Bolt, 78).Count);
    }

    // Four segments: one hit each, rolled one by one (the numbers equal the segments, each with its own roll);
    // the HP falls by each number in turn.
    [TestMethod]
    public void WzStrike_Segments_EachRolledOnItsOwn()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (100, 100));
        var hits = world.SkillStrike(7, 0, 100, true, new Random(5), Mage, Bolt, 78);
        Assert.AreEqual(4, hits.Count);
        var rng = new Random(5); // the same rolls by hand: one roll and one crit roll per segment
        long hp = 1_000_000_000;
        foreach (var h in hits)
        {
            long expected = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200);
            Assert.AreEqual(expected, h.Damage);
            hp -= expected;
            Assert.AreEqual(hp, h.HpLeft);
            Assert.AreEqual(Bolt, h.Skill);
        }
        Assert.IsTrue(hits.Select(h => h.Damage).Distinct().Count() > 1); // not four copies of one roll
    }

    // A mob that dies stops taking segments; the cap (mobCount) keeps the nearest to the target.
    [TestMethod]
    public void WzStrike_DeadStopsSegments_CapKeepsNearest()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData(mobCount: 2));
        assets.AddMap(7, """[{"MobId":100001,"X":100,"Cy":100},{"MobId":100000,"X":150,"Cy":100},{"MobId":100000,"X":200,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
        assets.AddMob(100001, 1, 0.1, 0, speed: 1, level: 200); // dies to the first segment
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hits = world.SkillStrike(7, 0, 100, true, new Random(1), Mage, Bolt, 78);
        Assert.AreEqual(1 + 4, hits.Count); // 1 segment on the dying target, 4 on the next nearest; x 200 is over the cap
        Assert.AreEqual(0, hits[0].HpLeft);
        Assert.AreEqual(1, hits.Count(h => h.Uid == hits[0].Uid));
        Assert.AreEqual(4, hits.Count(h => h.Uid != hits[0].Uid));
    }

    // The element of the skill goes into the chain: a fire bolt on a fire-weak mob (F3, x1.5) does what the chain says.
    [TestMethod]
    public void WzStrike_ElementComesFromTheSkill()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData(elem: "f"));
        assets.AddMap(7, """[{"MobId":100000,"X":100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200, elemAttr: "F3");
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        var hit = world.SkillStrike(7, 0, 100, true, new Random(2), Mage, Bolt, 78)[0];
        var rng = new Random(2);
        long fire = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, "F3", AttackElement.Fire);
        rng = new Random(2);
        long none = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, "F3", AttackElement.Physical);
        Assert.AreEqual(fire, hit.Damage);
        Assert.IsTrue(fire > none); // F3 is weak to fire: x1.5
        Assert.AreEqual(AttackElement.Physical, BoltData().Element()); // Energy Bolt: no element
        Assert.AreEqual(AttackElement.Fire, BoltData(elem: "f").Element());
        Assert.AreEqual(AttackElement.Poison, BoltData(elem: "s").Element());
        Assert.ThrowsException<InvalidDataException>(() => BoltData(elem: "i").Element()); // no case yet: loud
    }

    // The hand calculation of one Energy Bolt segment (INT 1000, LUK 100, MATT 200, short staff, Lv.200 on a Lv.200 mob
    // with 10% DEF, 78%, no crit, no bonus):
    //   stat 4 x 1000 + 100 = 4100; max actual = 1.20 x 4100 x 200 / 100 = 9840; min actual = 9840 x 20% = 1968
    //   min panel = 1 + 1968 = 1969; skill: 1969 x 0.78 = 1535.82 and 9840 x 0.78 = 7675.2
    //   x DEF 0.9 x level 1.10 (same level) = x 0.99: min 1520.46 -> 1520, max 7598.45 -> 7598
    //   Elemental Weakness +20 damage % (the damage % is 0 here, so the target zone goes 1.0 -> 1.2): min 1824.55 -> 1824, max 9118.14 -> 9118. Four segments: 4 x 1520 = 6080 to 4 x 7598 = 30392.
    [TestMethod]
    public void EnergyBolt_DamageRange_ByHand()
    {
        Assert.AreEqual(1520, Damage.SkillCalc(0.0, 1.0, 0.1, Mage, 78, 200));
        Assert.AreEqual(7598, Damage.SkillCalc(1.0, 1.0, 0.1, Mage, 78, 200));
        Assert.AreEqual(1824, Damage.SkillCalc(0.0, 1.0, 0.1, Mage, 78, 200, extraDmgPct: 20));
        Assert.AreEqual(9118, Damage.SkillCalc(1.0, 1.0, 0.1, Mage, 78, 200, extraDmgPct: 20));
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (100, 100));
        var hits = world.SkillStrike(7, 0, 100, true, new Random(9), Mage, Bolt, 78);
        Assert.IsTrue(hits.All(h => h.Damage is >= 1520 and <= 7598)); // through the real world: inside the hand range
    }

    // Elemental Weakness is damage %, not a final multiplier: it joins the additive sum with the damage % and the
    // normal-mob %. Build with damage 32%, normal mob 60% (INT 1000, LUK 100, MATT 200, short staff, Lv.200 vs Lv.200, DEF 10%, 78%):
    //   max actual 9840, min actual 1968; max panel floor(9840 x 1.32) = 12988; min panel floor(1 + 1968 x 1.32) = 2598
    //   skill: panel / 1.32 x 0.78 x (1 + (32 + 60) / 100 = 1.92) = 2947.55 .. 14735.48
    //   x 0.9 (DEF) x 1.10 (level): 2918 .. 14588
    //   with the mark: the sum is 32 + 20 + 60, so 2.12 and not 1.92 x 1.2 = 2.304: 3254.59 .. 16270.42 -> 3222 .. 16107
    //   the gain is 2.12 / 1.92 = x1.104, not x1.2
    [TestMethod]
    public void Weakness_JoinsTheAdditiveDamageSum_ByHand()
    {
        var build = Mage with { DmgPct = 32, NormalPct = 60 };
        Assert.AreEqual(2918, Damage.SkillCalc(0.0, 1.0, 0.1, build, 78, 200));
        Assert.AreEqual(14588, Damage.SkillCalc(1.0, 1.0, 0.1, build, 78, 200));
        Assert.AreEqual(3222, Damage.SkillCalc(0.0, 1.0, 0.1, build, 78, 200, extraDmgPct: 20));
        Assert.AreEqual(16107, Damage.SkillCalc(1.0, 1.0, 0.1, build, 78, 200, extraDmgPct: 20));
        // a boss takes the boss % in the same place: the mark adds the same 20 to its sum
        Assert.AreEqual(Damage.SkillCalc(0.5, 1.0, 0.1, (build with { BossPct = 100 }), 78, 200, boss: true, extraDmgPct: 20),
            Damage.SkillCalc(0.5, 1.0, 0.1, (build with { BossPct = 120 }), 78, 200, boss: true));
    }

    // The cost: mpCon of the level; the cooltime (WZ seconds) only when the skill has one; level 0 is not learned.
    [TestMethod]
    public void SkillCost_PayWz_MpAndCooldown()
    {
        var cost = new SkillCost(new Dictionary<int, SkillData>());
        var chara = new CharaSlot { Mp = 30 };
        var cooldowns = new Dictionary<int, float>();
        Assert.IsTrue(cost.Pay(chara, BoltData(), 20, cooldowns));
        Assert.AreEqual(6, chara.Mp); // 30 - 24
        Assert.AreEqual(0f, cooldowns[Bolt]); // Energy Bolt has no cooltime
        Assert.IsFalse(cost.Pay(chara, BoltData(), 20, cooldowns)); // 6 MP left: not enough
        Assert.AreEqual(6, chara.Mp);
        Assert.IsFalse(cost.Pay(new CharaSlot { Mp = 100 }, BoltData(), 0, cooldowns)); // not learned
        var slow = BoltData();
        slow.Common["cooltime"] = SkillFormula.Levels("3", 20);
        var rich = new CharaSlot { Mp = 100 };
        Assert.IsTrue(cost.Pay(rich, slow, 20, cooldowns));
        Assert.AreEqual(3000f, cooldowns[Bolt]);
        Assert.IsFalse(cost.Pay(rich, slow, 20, cooldowns)); // still cooling
        Assert.AreEqual(76, rich.Mp);
    }

    // Routing: a WZ skill with a damage line is judged; one without (a move) only relays; an unknown id fails loud.
    [TestMethod]
    public void IsDamageSkill_WzTable()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var move = new WzSkillData { SkillId = WzSkillData.Teleport, MaxLevel = 1, Kind = SkillData.KindActive };
        move.Common["mpCon"] = SkillFormula.Levels("1", 1);
        assets.AddWzSkill(move);
        var world = new MobWorld(assets.Dir);
        Assert.IsTrue(world.IsDamageSkill(Bolt));
        Assert.IsFalse(world.IsDamageSkill(WzSkillData.Teleport));
        Assert.ThrowsException<InvalidDataException>(() => world.IsDamageSkill(999999));
    }

    // ---- Mob statuses ----

    [TestMethod]
    public void MobStatuses_AddRefreshExpire()
    {
        var list = new MobStatuses();
        list.Step(100); // an empty list steps fine
        Assert.AreEqual(0, list.Value(MobStatusKind.ElementalWeakness));
        Assert.AreEqual(0, list.MagicExtraDamagePct);
        list.Add(MobStatusKind.ElementalWeakness, 20, 5000);
        Assert.AreEqual(20, list.Value(MobStatusKind.ElementalWeakness));
        Assert.AreEqual(20, list.MagicExtraDamagePct);
        list.Step(3000);
        Assert.AreEqual(20, list.Value(MobStatusKind.ElementalWeakness)); // 2000 ms left
        list.Add(MobStatusKind.ElementalWeakness, 30, 5000); // a second add refreshes: new number, full time, no stacking
        Assert.AreEqual(30, list.MagicExtraDamagePct);
        list.Step(4999); // 3000 + 4999 > 5000: only a refresh that restarted the time is still here
        Assert.AreEqual(30, list.Value(MobStatusKind.ElementalWeakness)); // 1 ms left
        list.Step(1);
        Assert.AreEqual(0, list.Value(MobStatusKind.ElementalWeakness)); // gone at 0
        Assert.AreEqual(0, list.MagicExtraDamagePct);
    }

    // ---- Elemental Weakness in the world ----

    // The mark is rolled first, before the segments: it is the first double of the seed. The segments follow.
    private static double WeaknessRoll(int seed) => new Random(seed).NextDouble();

    // The damage of segment i of a cast that rolled the mark first (one double spent): the rolls of the seed from the second on.
    private static long Segment(int seed, int i, double extraDmgPct)
    {
        var rng = new Random(seed);
        rng.NextDouble(); // the mark roll
        for (int k = 0; k < i; k++) { rng.NextDouble(); rng.NextDouble(); }
        double roll = rng.NextDouble(), crit = rng.NextDouble();
        return Damage.SkillCalc(roll, crit, 0.1, Mage, 78, 200, extraDmgPct: extraDmgPct);
    }

    // A cast rolls the mark for each mob BEFORE its segments (chance 15%: a first double under 0.15 marks). The
    // cast that marks is already marked: all its segments take +20 damage %. The mark runs out after 5 seconds.
    [TestMethod]
    public void Weakness_MarksFirst_AddsTwentyDamagePct_ExpiresAfterFiveSeconds()
    {
        int hitSeed = Enumerable.Range(0, 1000).First(s => WeaknessRoll(s) * 100 < 15);
        int missSeed = Enumerable.Range(0, 1000).First(s => WeaknessRoll(s) * 100 >= 15);
        var build = Mage with { WeaknessProp = 15 };
        var players = new List<(int id, int map, float x, float y)> { (1, 7, 900, 100) };

        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(WeaknessData());
        var world = World(assets, (100, 100));
        // a roll over 15: no mark, the cast is plain
        var plain = world.SkillStrike(7, 0, 100, true, new Random(missSeed), build, Bolt, 78);
        // a roll under 15: marked at once, the same cast already takes the +20
        var marked = world.SkillStrike(7, 0, 100, true, new Random(hitSeed), build, Bolt, 78);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(Segment(missSeed, i, 0), plain[i].Damage);
            Assert.AreEqual(Segment(hitSeed, i, 20), marked[i].Damage);
        }
        // 4999 ms later it is still on (a cast that rolls nothing, prop 0); at 5000 ms it is gone
        var rng = new Random(3);
        long expectedPlain = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200);
        rng = new Random(3);
        long expectedMarked = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraDmgPct: 20);
        world.Tick(4999, players);
        Assert.AreEqual(expectedMarked, world.SkillStrike(7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0].Damage);
        world.Tick(1, players);
        Assert.AreEqual(expectedPlain, world.SkillStrike(7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0].Damage);
    }

    // No Elemental Weakness (prop 0): no mark, and no random number is spent on it.
    [TestMethod]
    public void Weakness_NotLearned_SpendsNoRoll()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        var world = World(assets, (100, 100));
        var rng = new Random(11);
        world.SkillStrike(7, 0, 100, true, rng, Mage, Bolt, 78);
        var shadow = new Random(11);
        for (int i = 0; i < 8; i++) shadow.NextDouble(); // 4 segments x 2 rolls and nothing more
        Assert.AreEqual(shadow.NextDouble(), rng.NextDouble());
    }

    // A basic swing also rolls the mark ("on attack"), but the swing is physical: the mark does not change its damage.
    [TestMethod]
    public void Weakness_BasicSwingMarksButIsNotMagic()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(WeaknessData());
        var world = World(assets, (10, 100));
        var sure = Mage with { WeaknessProp = 100 };
        var first = world.Strike(7, 0, 100, true, new Random(4), sure);
        Assert.AreEqual(1, first.Count);
        // the bolt now feels the mark: +20 damage % on the rolls of seed 3
        var bolt = world.SkillStrike(7, 0, 100, true, new Random(3), Mage, Bolt, 78)[0];
        var rng = new Random(3);
        long expected = Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 78, 200, extraDmgPct: 20);
        Assert.AreEqual(expected, bolt.Damage);
        // a second basic swing on the marked mob: the physical chain, no +20
        var swing = world.Strike(7, 0, 100, true, new Random(4), Mage)[0];
        var shadow = new Random(4);
        Assert.AreEqual(Damage.Calc(shadow.NextDouble(), shadow.NextDouble(), 0.1, Mage, 200), swing.Damage);
    }

    // The numbers of the two skills come from their own formulas: Magic Guard 15 + 7 x level (85 at level 10), prop 15.
    [TestMethod]
    public void WzSkillStats_MagicGuardAndWeaknessNumbers()
    {
        var guard = new WzSkillData { SkillId = WzSkillData.MagicGuard, MaxLevel = 10 };
        guard.Common["x"] = SkillFormula.Levels("15+7*x", 10);
        var skills = new Dictionary<int, WzSkillData> { [guard.SkillId] = guard, [WzSkillData.ElementalWeakness] = WeaknessData() };
        Assert.AreEqual(22, guard.Value("x", 1));
        Assert.AreEqual(85, WzSkillStats.MagicGuardPct(skills));
        Assert.AreEqual(15, WzSkillStats.WeaknessProp(skills));
    }

    // ---- Magic Guard ----

    // 85% of the damage is paid by MP (rounded down), the rest by HP. Before and after: HP 1000 / MP 500, a hit of 100.
    [TestMethod]
    public void MagicGuard_SplitsTheHitBetweenMpAndHp()
    {
        var chara = new CharaSlot { Hp = 1000, Mp = 500 };
        long dealt = Progression.ApplyHurt(chara, 100, 85);
        Assert.AreEqual(100, dealt); // the number shown is the whole hit
        Assert.AreEqual(500 - 85, chara.Mp);
        Assert.AreEqual(1000 - 15, chara.Hp);
        var odd = new CharaSlot { Hp = 1000, Mp = 500 };
        Progression.ApplyHurt(odd, 7, 85); // 5.95: MP pays 5, HP pays 2
        Assert.AreEqual(495, odd.Mp);
        Assert.AreEqual(998, odd.Hp);
    }

    // MP that runs out pays what it has; the rest goes to HP. With no MP the guard does nothing; with 0% it is the old rule.
    [TestMethod]
    public void MagicGuard_MpRunsShort_RestGoesToHp()
    {
        var short1 = new CharaSlot { Hp = 1000, Mp = 30 };
        Progression.ApplyHurt(short1, 100, 85); // wants 85 from MP, has 30
        Assert.AreEqual(0, short1.Mp);
        Assert.AreEqual(930, short1.Hp); // 100 - 30
        var dry = new CharaSlot { Hp = 1000, Mp = 0 };
        Progression.ApplyHurt(dry, 100, 85);
        Assert.AreEqual(0, dry.Mp);
        Assert.AreEqual(900, dry.Hp);
        var plain = new CharaSlot { Hp = 1000, Mp = 500 };
        Progression.ApplyHurt(plain, 100);
        Assert.AreEqual(500, plain.Mp); // no Magic Guard: MP untouched
        Assert.AreEqual(900, plain.Hp);
        var dead = new CharaSlot { Hp = 10, Mp = 0 };
        Progression.ApplyHurt(dead, 100, 85);
        Assert.AreEqual(0, dead.Hp); // HP floors at 0
    }

    // The order of the checks: an invincible window blocks the touch before Magic Guard looks at it (no MP paid, no
    // new window); a dodged touch never reaches it; a landed touch starts the window after the split.
    [TestMethod]
    public void MagicGuard_OrderWithInvincibleAndDodge()
    {
        var guard = new HurtGuard();
        var chara = new CharaSlot { Hp = 1000, Mp = 500 };
        guard.Dodge(); // a dodge opens the invincible window: the next touch is blocked whole
        Assert.AreEqual(0, guard.Apply(chara, 100, 85));
        Assert.AreEqual(500, chara.Mp);
        Assert.AreEqual(1000, chara.Hp);
        guard.Clear();
        Assert.AreEqual(100, guard.Apply(chara, 100, 85)); // lands: split, then the window starts
        Assert.AreEqual(415, chara.Mp);
        Assert.AreEqual(985, chara.Hp);
        Assert.IsTrue(guard.Invincible);
        Assert.AreEqual(0, guard.Apply(chara, 100, 85)); // the window blocks the next one
        Assert.AreEqual(415, chara.Mp);
    }

    // ---- Over the wire: the online server judges with the rectangle and the segments, and the Build carries the mark chance ----

    [TestMethod]
    public void Loopback_EnergyBolt_RectangleSegmentsAndWeaknessProp()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BoltData());
        assets.AddWzSkill(WeaknessData());
        assets.AddMap(9, """[{"MobId":100000,"X":100,"Cy":100},{"MobId":100000,"X":300,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 70);
        server.Start();
        using var a = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 70);
        a.SendState(9, 0, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        WaitSpawn(a);
        a.SendBuild(Mage with { WeaknessProp = 100 }); // the 69-byte Build; every hit marks
        a.SendSkillCast(Bolt, 78);
        var first = Enumerable.Range(0, 4).Select(_ => WaitHit(a)).ToList();
        Assert.AreEqual(1, first.Select(h => h.Uid).Distinct().Count()); // only the mob at x 100: x 300 is past the box
        Assert.IsTrue(first.All(h => h.Skill == Bolt && h.Damage is >= 1824 and <= 9118)); // 4 segments, already marked by this cast (+20 damage %)
        Assert.IsFalse(a.TryTakeMob(out _)); // no fifth hit, no hit on the far mob
        // prop 100 came over the wire, so the mark is on: segments can pass the plain maximum 7598
        var later = new List<MobEvent>(first);
        for (int i = 0; i < 20; i++)
        {
            a.SendSkillCast(Bolt, 78);
            for (int k = 0; k < 4; k++) later.Add(WaitHit(a));
        }
        Assert.IsTrue(later.All(h => h.Damage is >= 1824 and <= 9118)); // +20 damage % on every segment
        Assert.IsTrue(later.Any(h => h.Damage > 7598)); // 84 segments: one over the plain maximum is all but sure (0.79^80)
    }

    private static void WaitSpawn(NetClient client) => Assert.IsTrue(WaitHit(client).Spawn);

    private static MobEvent WaitHit(NetClient client)
    {
        for (int i = 0; i < 500; i++)
        {
            if (client.TryTakeMob(out var e)) return e;
            Thread.Sleep(10);
        }
        Assert.Fail("timed out waiting for a mob event");
        throw new InvalidOperationException("unreachable");
    }
}
