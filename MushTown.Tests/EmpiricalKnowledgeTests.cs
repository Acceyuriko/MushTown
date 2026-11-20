using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Empirical Knowledge (the Explorer Magician Link, WZ 80002762 / body 0000255): the per-attacker mark stacks, their proc/refresh/
// expire and the damage % / DEF-ignored % they add to that attacker's own hits.
[TestClass]
public class EmpiricalKnowledgeTests
{
    private const string Floor = """[{"X1":-1000,"Y1":100,"X2":1000,"Y2":100,"Layer":0}]""";
    private const int SkillId = 2111003; // a plain one-segment damage skill for the mark tests

    // 80002762 at max level 9: stack cap 3 (x), time 10 s, chance 13+2*level = 31% (prop), damage % per stack and
    // DEF-ignored % per stack u(level/2) = 5 (y for damage, w for DEF ignored). The same numbers the exporter writes into
    // skill/wz/80002762.json.
    private static WzSkillData MarkData()
    {
        var d = new WzSkillData { SkillId = WzSkillData.EmpiricalKnowledge, MaxLevel = 9, Kind = SkillData.KindPassive };
        d.Common["x"] = SkillFormula.Levels("3", 9);
        d.Common["time"] = SkillFormula.Levels("10", 9);
        d.Common["prop"] = SkillFormula.Levels("13+2*x", 9);
        d.Common["y"] = SkillFormula.Levels("u(x/2)", 9);
        d.Common["w"] = SkillFormula.Levels("u(x/2)", 9);
        return d;
    }

    // A no-ball, no-action damage skill (100%, 1 segment, 1 mob): it judges in the cast, so the test needs no tick.
    private static WzSkillData StrikeData()
    {
        var d = new WzSkillData { SkillId = SkillId, MaxLevel = 10, Kind = SkillData.KindActive };
        d.Common["damage"] = SkillFormula.Levels("100", 10);
        d.Common["attackCount"] = SkillFormula.Levels("1", 10);
        d.Common["mobCount"] = SkillFormula.Levels("1", 10);
        d.Vectors["lt"] = new[] { -200, -200 };
        d.Vectors["rb"] = new[] { 200, 120 };
        return d;
    }

    private static Build Mage(int markProp) =>
        new(1000, 100, 200, 0, 0, 0, Level: 200, WeaponType: (int)WeaponType.ShortStaff, MarkProp: markProp);

    // One mob of huge HP at (x, 100), level 200, DEF 50% (so the DEF-ignored % matters).
    private static MobWorld World(TempAssets assets, int x = 100)
    {
        assets.AddMap(7, $$"""[{"MobId":100000,"X":{{x}},"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.5, 0, speed: 1, level: 200);
        assets.AddWzSkill(StrikeData());
        assets.AddWzSkill(MarkData());
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        return world;
    }

    // The same world, but the damage skill is a plain warrior skill (<id>.json), so SkillStrike takes the warrior skill path.
    private static MobWorld PlainWorld(TempAssets assets, int x = 100)
    {
        assets.AddMap(7, $$"""[{"MobId":100000,"X":{{x}},"Cy":100}]""", Floor);
        assets.AddMob(100000, 1_000_000_000, 0.5, 0, speed: 1, level: 200);
        assets.AddSkill(SkillId, range: 200, mobCount: 1);
        assets.AddWzSkill(MarkData());
        var world = new MobWorld(assets.Dir);
        world.EnterMap(7);
        return world;
    }

    private static int Uid(MobWorld world) => world.MobsOn(7)[0].Uid;

    // MobStatuses: a proc adds a stack up to the cap and refreshes; a non-proc refreshes only; the clock drops them all; each
    // source has its own stacks; the changes report to the owner.
    [TestMethod]
    public void MobStatuses_Marks_ProcCapPerSourceRefreshAndExpire()
    {
        var s = new MobStatuses();
        Assert.AreEqual(0, s.MarkStacks(5));
        s.HitMark(5, maxStacks: 3, ms: 1000, proc: true);
        s.HitMark(5, 3, 1000, proc: true);
        s.HitMark(5, 3, 1000, proc: true);
        Assert.AreEqual(3, s.MarkStacks(5)); // at the cap
        s.HitMark(5, 3, 1000, proc: true);
        Assert.AreEqual(3, s.MarkStacks(5)); // still 3: no fourth stack
        s.HitMark(5, 3, 1000, proc: false);
        Assert.AreEqual(3, s.MarkStacks(5)); // a non-proc hit refreshes, it does not remove
        s.HitMark(6, 3, 1000, proc: true);
        Assert.AreEqual(1, s.MarkStacks(6)); // another source: its own stack
        Assert.AreEqual(3, s.MarkStacks(5)); // and it does not touch the first source
        var changes = s.TakeMarkChanges();
        Assert.IsTrue(changes.Contains((5, 1)) && changes.Contains((5, 2)) && changes.Contains((5, 3)) && changes.Contains((6, 1)));
        s.Step(1000); // the time runs out
        Assert.AreEqual(0, s.MarkStacks(5));
        Assert.AreEqual(0, s.MarkStacks(6));
        var expired = s.TakeMarkChanges();
        Assert.AreEqual(2, expired.Count);
        Assert.IsTrue(expired.Contains((5, 0)) && expired.Contains((6, 0)));
    }

    // MobWorld: two attackers each proc their own stack on the same mob, and only seen for that attacker. A 100% chance is used so
    // the proc is certain.
    [TestMethod]
    public void MobWorld_TwoAttackers_HaveIndependentStacks()
    {
        using var assets = new TempAssets();
        var world = World(assets, x: 50); // in reach of a basic swing from x 0 facing right
        int uid = Uid(world);
        var build = Mage(markProp: 100);
        world.Strike(7, 0, 100, true, new Random(1), build, attacker: 5);
        world.Strike(7, 0, 100, true, new Random(2), build, attacker: 6);
        Assert.AreEqual(1, world.MarkStacks(uid, 5));
        Assert.AreEqual(1, world.MarkStacks(uid, 6));
    }

    // CancelDoTs is the disconnect / map-change / cash-shop path: it clears only the leaving source's marks, another source's stay.
    [TestMethod]
    public void CancelDoTs_OneSourceLeaves_OnlyItsMarksGo()
    {
        using var assets = new TempAssets();
        var world = World(assets, x: 50); // in reach of a basic swing from x 0 facing right
        int uid = Uid(world);
        var build = Mage(markProp: 100); // 100%: each source certainly adds one stack
        world.Strike(7, 0, 100, true, new Random(1), build, attacker: 5);
        world.Strike(7, 0, 100, true, new Random(2), build, attacker: 6);
        Assert.AreEqual(1, world.MarkStacks(uid, 5));
        Assert.AreEqual(1, world.MarkStacks(uid, 6));

        world.CancelDoTs(5); // source 5 leaves
        Assert.AreEqual(0, world.MarkStacks(uid, 5), "the leaving source's marks are gone");
        Assert.AreEqual(1, world.MarkStacks(uid, 6), "another source's marks stay");
    }

    // The mark on the mob adds damage % and DEF-ignored % to THAT attacker's hits, and nothing to another attacker's hit.
    [TestMethod]
    public void MobWorld_MarkStacks_RaiseOnlyTheOwnersDamage()
    {
        using var marked = new TempAssets();
        using var plain = new TempAssets();
        var wm = World(marked);
        var wp = World(plain);
        var build = Mage(markProp: 1); // a chance low enough that neither hit procs, so the only difference is the preset stacks
        wm.SetMarkStacks(Uid(wm), 0, 3, 600000);

        long ownMarked = wm.SkillStrike(7, 0, 100, true, new Random(0), build, SkillId, 100)[0].Damage;
        long ownPlain = wp.SkillStrike(7, 0, 100, true, new Random(0), build, SkillId, 100)[0].Damage;
        Assert.IsTrue(ownMarked > ownPlain, $"3 own stacks should raise the hit ({ownMarked} vs {ownPlain})");

        // Another attacker (id 6) on the same marked mob gets no bonus: its hit equals the unmarked world's hit.
        using var other = new TempAssets();
        var wo = World(other);
        wo.SetMarkStacks(Uid(wo), 0, 3, 600000); // the stacks belong to attacker 0
        long otherOnMarked = wo.SkillStrike(7, 0, 100, true, new Random(0), build, SkillId, 100, attacker: 6)[0].Damage;
        Assert.AreEqual(ownPlain, otherOnMarked, "attacker 6 must not get attacker 0's mark");
    }

    // A plain warrior skill (the warrior skill path, not a WZ file) also gets the attacker's mark bonus.
    [TestMethod]
    public void SkillStrike_PlainSkill_AlsoGetsTheMarkBonus()
    {
        using var marked = new TempAssets();
        using var plain = new TempAssets();
        var wm = PlainWorld(marked);
        var wp = PlainWorld(plain);
        var build = Mage(markProp: 1); // a chance low enough that neither hit procs, so the only difference is the preset stacks
        wm.SetMarkStacks(Uid(wm), 0, 3, 600000);
        long ownMarked = wm.SkillStrike(7, 0, 100, true, new Random(0), build, SkillId, 100)[0].Damage;
        long ownPlain = wp.SkillStrike(7, 0, 100, true, new Random(0), build, SkillId, 100)[0].Damage;
        Assert.IsTrue(ownMarked > ownPlain, $"3 own stacks should raise a plain skill hit ({ownMarked} vs {ownPlain})");
    }

    // Statuses first: with a certain chance (100) the first hit rolls its stack in step 1 and already takes that stack's damage % and DEF ignored
    // (5 % each at one stack), a skill hit and a basic swing alike; the stack then lands on the mob.
    [TestMethod]
    public void FirstHit_AlreadyCarriesTheStackItAdds_ThenTheStackLands()
    {
        var build = Mage(markProp: 100);
        using var skillAssets = new TempAssets();
        var skillWorld = World(skillAssets);
        long skillHit = skillWorld.SkillStrike(7, 0, 100, true, new Random(1), build, SkillId, 100)[0].Damage;
        var rng = new Random(1);
        rng.NextDouble(); // the stack roll comes first
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.5, build, 100, 200, extraDmgPct: 5, extraIedPct: 5), skillHit);
        Assert.AreEqual(1, skillWorld.MarkStacks(Uid(skillWorld), 0));
        using var swingAssets = new TempAssets();
        var swingWorld = World(swingAssets, x: 50);
        long swing = swingWorld.Strike(7, 0, 100, true, new Random(1), build)[0].Damage;
        rng = new Random(1);
        rng.NextDouble();
        Assert.AreEqual(Damage.Calc(rng.NextDouble(), rng.NextDouble(), 0.5, build, 200, extraDmgPct: 5, extraIedPct: 5), swing);
        Assert.AreEqual(1, swingWorld.MarkStacks(Uid(swingWorld), 0));
    }

    // The two terms the mark feeds the chain: additive damage % and DEF-ignored % for this hit only.
    [TestMethod]
    public void DamageChain_MarkTerms_AreAdditiveDamageAndDefIgnored()
    {
        var build = new Build(1000, 100, 200, 0, 0, 0, Level: 200, WeaponType: (int)WeaponType.ShortStaff);
        var none = DamageChain.Calculate(build, 0.5, 1.0, 0.5, 200);
        var dmgOnly = DamageChain.Calculate(build, 0.5, 1.0, 0.5, 200, extraDmgPct: 15);
        var both = DamageChain.Calculate(build, 0.5, 1.0, 0.5, 200, extraDmgPct: 15, extraIedPct: 15);
        Assert.IsTrue(dmgOnly.TotalDamage > none.TotalDamage, "the per-stack damage % must raise the hit");
        Assert.IsTrue(both.TotalDamage > dmgOnly.TotalDamage, "the per-stack DEF-ignored % must raise the hit");
    }

    // The proc chance read from the WZ at max level is 31% (13 + 2 x level), the number the marker uses.
    [TestMethod]
    public void EmpiricalKnowledgeProp_IsTheWzChanceAtMaxLevel()
    {
        var skills = new Dictionary<int, WzSkillData> { [WzSkillData.EmpiricalKnowledge] = MarkData() };
        Assert.AreEqual(31, WzSkillStats.EmpiricalKnowledgeProp(skills));
    }
}
