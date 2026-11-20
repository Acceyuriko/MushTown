using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Tests;

// Fire/Poison 3rd job actives (211.img): Explosion (a box on the caster that every mob inside takes, judged at the body action's attack
// frame, fire, through the hit pipeline like Flame Orb) and Elemental Adaptation (a cast that gives a shield of 10 layers, each blocks one abnormal status; its status and elemental resistance are always on).
// Shares the helpers of FirePoisonCombatTests.
public partial class FirePoisonCombatTests
{
    private const int Blast = WzSkillData.Explosion;
    private const int Adaptation = WzSkillData.ElementalAdaptation;

    // Explosion as 211.img writes it at level 20: fire, 2 segments, 8 mobs, the box lt(-250,-160) rb(250,140) on the caster, no ball,
    // the body action explosion with its attack frame at 390 ms.
    private static WzSkillData BlastData()
    {
        var data = new WzSkillData { SkillId = Blast, MaxLevel = 20, Kind = SkillData.KindActive, ElemAttr = "f", AttackDelayMs = 390 };
        data.Common["mpCon"] = SkillFormula.Levels("40+4*d(x/5)", 20);
        data.Common["damage"] = SkillFormula.Levels("245+8*x", 20);
        data.Common["attackCount"] = SkillFormula.Levels("2", 20);
        data.Common["mobCount"] = SkillFormula.Levels("8", 20);
        data.Vectors["lt"] = new[] { -250, -160 };
        data.Vectors["rb"] = new[] { 250, 140 };
        return data;
    }

    // Elemental Adaptation as 211.img writes it at level 20: a buff (info type 10), MP 35, cooltime 240 s, 10 blocks (y), asrR = terR = x = 20.
    private static WzSkillData AdaptationData()
    {
        var data = new WzSkillData { SkillId = Adaptation, MaxLevel = 20, Kind = SkillData.KindBuff, Name = "Elemental Adaptation (Fire, Poison)" };
        data.Common["mpCon"] = SkillFormula.Levels("20+5*u(x/7)", 20);
        data.Common["cooltime"] = SkillFormula.Levels("600-18*x", 20);
        data.Common["y"] = SkillFormula.Levels("10", 20);
        data.Common["asrR"] = SkillFormula.Levels("x", 20);
        data.Common["terR"] = SkillFormula.Levels("x", 20);
        return data;
    }

    // The box is on the caster and has both sides: a mob 250 px away on either side is hit, 251 is not, whatever the facing. Up, the
    // box ends 160 px over the feet (edge inside); down it reaches 140 px under them (the body height is 0 here). Two segments each.
    [TestMethod]
    public void Explosion_BoxOnTheCaster_BothSides_EdgesInside()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BlastData());
        var world = World(assets, (-251, 100), (-250, 100), (-1, 100), (0, 100), (250, 100), (251, 100), (100, -60), (100, -61), (100, 240), (100, 241));
        var left = Cast(world, 7, 0, 100, false, new Random(1), Mage, Blast, 245 + 8 * 20);
        CollectionAssert.AreEqual(new[] { -250, -1, 0, 100, 100, 250 }, UidsHit(world, left)); // the two at x 100 are the y -60 and y 240 ones
        Assert.AreEqual(12, left.Count);
        Assert.AreEqual(6, left.Count(h => h.Settle));
        using var assets2 = new TempAssets();
        assets2.AddWzSkill(BlastData());
        var world2 = World(assets2, (-251, 100), (-250, 100), (250, 100), (251, 100));
        var right = Cast(world2, 7, 0, 100, true, new Random(1), Mage, Blast, 405);
        CollectionAssert.AreEqual(new[] { -250, 250 }, UidsHit(world2, right)); // the same box facing right
    }

    // At most mobCount (8) mobs are hit: the nearest ones.
    [TestMethod]
    public void Explosion_AtMostEightMobs_TheNearest()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BlastData());
        var world = World(assets, (-10, 100), (20, 100), (-30, 100), (40, 100), (-50, 100), (60, 100), (-70, 100), (80, 100), (-90, 100), (100, 100));
        var hits = Cast(world, 7, 0, 100, false, new Random(1), Mage, Blast, 405);
        CollectionAssert.AreEqual(new[] { -70, -50, -30, -10, 20, 40, 60, 80 }, UidsHit(world, hits));
    }

    // No ball: the cast returns nothing and no launch goes out; the box judges at the attack frame (390 ms), on the first server tick
    // after it (500 ms) and never before, once only.
    [TestMethod]
    public void Explosion_JudgesAtTheAttackFrame_NotBefore()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BlastData());
        var world = World(assets, (-100, 100), (150, 100));
        Assert.AreEqual(0, world.SkillStrike(7, 0, 100, false, new Random(1), Mage, Blast, 405).Count);
        Assert.AreEqual(0, world.TakeLaunches().Count);
        Assert.AreEqual(0, world.Tick(400, new()).Strikes.Count); // 390 ms is the attack frame: the first tick after it is the one that judges
        var late = world.Tick(100, new()).Strikes;
        Assert.AreEqual(4, late.Count); // 2 mobs, 2 segments
        Assert.IsTrue(late.All(s => s.attacker == 0 && s.hit.Skill == Blast));
        Assert.AreEqual(0, world.Tick(1000, new()).Strikes.Count);
    }

    // Fire: a fire-weak mob (F3, x1.5) takes more than a plain one, a fire-resist one (F2, x0.5) less; the same rolls.
    [TestMethod]
    public void Explosion_Fire_WeakTakesMore_ResistTakesLess()
    {
        long Hit(string elem)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(BlastData());
            assets.AddMap(7, """[{"MobId":100000,"X":-100,"Cy":100}]""", Floor);
            assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200, elemAttr: elem);
            var world = new MobWorld(assets.Dir);
            world.EnterMap(7);
            return Cast(world, 7, 0, 100, false, new Random(5), Mage, Blast, 405)[0].Damage;
        }
        long weak = Hit("F3"), plain = Hit(""), resist = Hit("F2");
        var rng = new Random(5);
        Assert.AreEqual(Damage.SkillCalc(rng.NextDouble(), rng.NextDouble(), 0.1, Mage, 405, 200, "F3", AttackElement.Fire), weak);
        Assert.IsTrue(weak > plain && plain > resist);
        Assert.AreEqual(weak / 1.5, plain, plain * 0.001);
        Assert.AreEqual(plain * 0.5, resist, plain * 0.001);
    }

    // Explosion takes Element Amplification (the test only asserts that the damage gets bigger).
    [TestMethod]
    public void Explosion_TakesThePipelineBonuses_LikeAnyMagicSkill()
    {
        long Hit(Build build)
        {
            using var assets = new TempAssets();
            assets.AddWzSkill(BlastData());
            assets.AddMap(7, """[{"MobId":100000,"X":-100,"Cy":100}]""", Floor);
            assets.AddMob(100000, 1_000_000_000, 0.1, 0, speed: 1, level: 200);
            var world = new MobWorld(assets.Dir);
            world.EnterMap(7);
            return Cast(world, 7, 0, 100, false, new Random(5), build, Blast, 405)[0].Damage;
        }
        long plain = Hit(Mage with { DmgPct = 10, NormalPct = 10 });
        long amplified = Hit(Mage with { DmgPct = 10, NormalPct = 10, MagicDmgPct = 50 });
        Assert.IsTrue(amplified > plain);
    }

    // Elemental Adaptation's resistances are always on (cast or not): its two records come from WzSkillStats.Modifiers (Skill source, named after the
    // skill), status resistance +20 and elemental resistance +20 at max level. The status resistance is an added number of the panel; the elemental resistance cuts an element
    // hit taken by half of it (10%), a plain hit is not cut.
    [TestMethod]
    public void ElementalAdaptation_Resistances_AlwaysOn()
    {
        var mods = WzSkillStats.Modifiers(WzSkillTests.FirePoisonPassives(), 200, 0).ToList();
        var own = mods.Where(m => m.Label == "Elemental Adaptation (Fire, Poison)").ToList();
        CollectionAssert.AreEqual(new[]
        {
            new StatModifier(StatKey.STATUS_RESISTANCE, StatValueType.FLAT, 20, StatSource.Skill, "Elemental Adaptation (Fire, Poison)"),
            new StatModifier(StatKey.ELEMENT_RESISTANCE, StatValueType.FLAT, 20, StatSource.Skill, "Elemental Adaptation (Fire, Poison)"),
        }, own);
        var snap = StatAggregator.Aggregate(mods);
        Assert.AreEqual(20, snap.IntValue(StatKey.STATUS_RESISTANCE));
        Assert.AreEqual(20, snap.IntValue(StatKey.ELEMENT_RESISTANCE));
        Assert.AreEqual(900, DamageChain.TakenDamage(snap, 1000, elemental: true)); // 1000 x (1 - 20 / 200)
        Assert.AreEqual(1000, DamageChain.TakenDamage(snap, 1000)); // a plain hit is not cut
    }

    // The cast pays 35 MP at once (the MP cost percent of Element Amplification does not raise it) and starts the 240 s cooldown; a second cast is
    // refused while it runs (no MP paid), and when it is over the cast is paid again.
    [TestMethod]
    public void ElementalAdaptation_PaysMpAndCooldown_RefusedWhileCooling()
    {
        var wz = AdaptationData();
        var chara = new CharaSlot { Mp = 100 };
        var cost = new SkillCost(new Dictionary<int, SkillData>());
        var cooldowns = new Dictionary<int, float>();
        Assert.IsTrue(cost.Pay(chara, wz, wz.MaxLevel, cooldowns, mpCostPct: 50));
        Assert.AreEqual(65, chara.Mp); // 100 - 35, the percent does not apply
        Assert.AreEqual(240_000f, cooldowns[Adaptation]);
        Assert.IsFalse(cost.Pay(chara, wz, wz.MaxLevel, cooldowns));
        Assert.AreEqual(65, chara.Mp);
        cooldowns[Adaptation] = 0; // the cooldown ran out
        Assert.IsTrue(cost.Pay(chara, wz, wz.MaxLevel, cooldowns));
        Assert.AreEqual(30, chara.Mp);
    }

    // The shield: the layers are WZ y (10) at max level; each block spends one (and only a block spends anything: no MP, no time); the 11th is not
    // blocked; the shield has no timer (Fighter.Tick leaves it alone). A new cast fills it again.
    [TestMethod]
    public void StatusShield_TenBlocks_TheEleventhPasses_NoTimer()
    {
        var wz = AdaptationData();
        var fighter = new Fighter();
        Assert.IsFalse(fighter.TryBlockStatus()); // nothing cast yet: not blocked
        fighter.StatusShield = (int)wz.Value("y", wz.MaxLevel);
        Assert.AreEqual(10, fighter.StatusShield);
        fighter.Tick(1_000_000);
        Assert.AreEqual(10, fighter.StatusShield);
        for (int i = 1; i <= 10; i++)
        {
            Assert.IsTrue(fighter.TryBlockStatus(), $"block {i}");
            Assert.AreEqual(10 - i, fighter.StatusShield);
        }
        Assert.IsFalse(fighter.TryBlockStatus());
        Assert.AreEqual(0, fighter.StatusShield);
        fighter.StatusShield = (int)wz.Value("y", wz.MaxLevel); // the next cast
        Assert.AreEqual(10, fighter.StatusShield);
    }

    // A new session or character starts clean: the shield, the cooldowns and the buffs are gone, so nothing is blocked.
    [TestMethod]
    public void Fighter_Reset_ClearsShieldCooldownsAndBuffs()
    {
        var fighter = new Fighter { StatusShield = 10, Orbs = 3 };
        fighter.Cooldowns[Adaptation] = 240_000f;
        fighter.Buffs[WzSkillData.Meditation] = 1000f;
        fighter.Reset();
        Assert.IsFalse(fighter.TryBlockStatus());
        Assert.AreEqual(0, fighter.StatusShield);
        Assert.AreEqual(0, fighter.Cooldowns.Count);
        Assert.AreEqual(0, fighter.Buffs.Count);
        Assert.AreEqual(0, fighter.Orbs);
    }

    // The status interface is empty and nothing implements it yet (a status will implement it and ask TryBlockStatus before it is put on).
    // Delete this test when the first abnormal status is implemented.
    [TestMethod]
    public void StatusEffect_InterfaceIsEmpty_NoImplementation()
    {
        Assert.AreEqual(0, typeof(IStatusEffect).GetMembers().Length);
        var implementations = typeof(IStatusEffect).Assembly.GetTypes().Where(t => t != typeof(IStatusEffect) && typeof(IStatusEffect).IsAssignableFrom(t));
        Assert.AreEqual(0, implementations.Count());
    }

    // Page III of the Fire/Poison job: the five passives, then the two actives (the skill window and the quick slots read the page).
    [TestMethod]
    public void FirePoison_PageThree_HasExplosionAndElementalAdaptation()
    {
        var page = JobSkills.Pages(StatTables.FirePoisonMage)[2];
        CollectionAssert.AreEqual(new[] { WzSkillData.BurningMagic, WzSkillData.ElementAmplification, WzSkillData.MagicCritical, WzSkillData.ElementalDecrease,
            WzSkillData.TeleportBoost, Blast, Adaptation }, page);
        Assert.IsTrue(JobSkills.Has(StatTables.FirePoisonMage, Blast));
        Assert.IsTrue(JobSkills.Has(StatTables.FirePoisonMage, Adaptation));
        Assert.IsFalse(JobSkills.Has(StatTables.Hero, Blast));
    }

    // Over the wire: Explosion is judged by the server (the Build rides the wire): nothing at the cast, then both clients get the two hits of each
    // mob in the box, after the attack frame (390 ms) and a tick; the mob 300 px away is outside the box.
    [TestMethod]
    public void Loopback_Explosion_ServerJudges_BothSeeTheHits()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(BlastData());
        assets.AddMap(9, """[{"MobId":100000,"X":-100,"Cy":100},{"MobId":100000,"X":200,"Cy":100},{"MobId":100000,"X":-300,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 77);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 77);
        b.Connect("127.0.0.1", Net.Port + 77);
        a.SendState(9, 0, 100, Net.ActStand, 0, false);
        b.SendState(9, 600, 100, Net.ActStand, 0, true);
        for (int i = 0; i < 3; i++) { WaitSpawn(a); WaitSpawn(b); }
        a.SendBuild(Mage);
        var cast = System.Diagnostics.Stopwatch.StartNew();
        a.SendSkillCast(Blast, 405);
        var hits = Enumerable.Range(0, 4).Select(_ => WaitHit(a)).ToList(); // mobs at x -100 and 200, 2 segments each
        cast.Stop();
        Assert.IsTrue(cast.ElapsedMilliseconds >= 390); // never before the attack frame
        Assert.AreEqual(2, hits.Select(h => h.Uid).Distinct().Count());
        Assert.IsTrue(hits.All(h => h.Skill == Blast && h.Damage > 0));
        var seen = Enumerable.Range(0, 4).Select(_ => WaitHit(b)).ToList();
        CollectionAssert.AreEqual(hits.Select(h => h.Damage).ToList(), seen.Select(h => h.Damage).ToList());
        for (int i = 0; i < 200 && !b.TryTakeFx(out _); i++) Thread.Sleep(5); // the Explosion effect of a, shown to b (drain it)
        Thread.Sleep(200);
        Assert.IsFalse(a.TryTakeMob(out _));
        Assert.IsFalse(b.TryTakeMob(out _));
    }

    // A buff cast is only relayed: the server judges nothing (no damage line), the other client gets the cast effect message and no hit.
    [TestMethod]
    public void Loopback_ElementalAdaptation_OnlyRelaysTheEffect()
    {
        using var assets = new TempAssets();
        assets.AddWzSkill(AdaptationData());
        assets.AddMap(9, """[{"MobId":100000,"X":-100,"Cy":100}]""", Floor);
        assets.AddMob(100000, 2_000_000_000, 0.1, 0, speed: 1, level: 200);
        using var server = new GameServer(_ => { }, assets.Dir, Net.Port + 78);
        server.Start();
        using var a = new NetClient(_ => { });
        using var b = new NetClient(_ => { });
        a.Connect("127.0.0.1", Net.Port + 78);
        b.Connect("127.0.0.1", Net.Port + 78);
        a.SendState(9, 0, 100, Net.ActStand, 0, false);
        b.SendState(9, 600, 100, Net.ActStand, 0, true);
        WaitSpawn(a);
        WaitSpawn(b);
        a.SendBuild(Mage);
        a.SendSkillCast(Adaptation, 0);
        SkillFxEvent fx = default;
        for (int i = 0; i < 200 && !b.TryTakeFx(out fx); i++) Thread.Sleep(5);
        Assert.AreEqual(Adaptation, fx.Skill);
        Thread.Sleep(600);
        Assert.IsFalse(a.TryTakeMob(out _)); // no hit for anyone
        Assert.IsFalse(b.TryTakeMob(out _));
    }
}
