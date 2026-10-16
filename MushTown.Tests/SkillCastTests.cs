using MushTown.Shared;

namespace MushTown.Tests;

// Probe P1: Cost and Damage read defs live by id; math stays in Progression.
[TestClass]
public class SkillCastTests
{
    private static SkillData Active(int id = 1001005) => new()
    {
        SkillId = id,
        Name = "Slash Blast",
        ReqLevel = 1,
        MaxLevel = 20,
        Kind = SkillData.KindActive,
        Mp = 10,
        CooldownMs = 1000,
        DmgLo = 100,
        DmgHi = 200,
    };

    private static Dictionary<int, SkillData> Defs(params SkillData[] ss) =>
        ss.ToDictionary(s => s.SkillId);

    // Pay gates: no chara, unlearned, too young, too dry, cooling. Pays MP
    // and sets the cooldown only when all pass.
    [TestMethod]
    public void Pay_GatesAndPays()
    {
        var defs = Defs(Active());
        var cost = new SkillCost(defs);
        var cd = new Dictionary<int, float>();
        Assert.IsFalse(cost.Pay(null, 1001005, 1, cd));
        Assert.IsFalse(cost.Pay(new CharaSlot { Level = 5, Mp = 10 }, 1001005, 0, cd)); // unlearned
        Assert.IsFalse(cost.Pay(new CharaSlot { Level = 0, Mp = 10 }, 1001005, 1, cd)); // too young
        var dry = new CharaSlot { Level = 5, Mp = 4 };
        Assert.IsFalse(cost.Pay(dry, 1001005, 1, cd)); // too dry
        Assert.AreEqual(4, dry.Mp); // failed pays spend nothing
        cd[1001005] = 500;
        Assert.IsFalse(cost.Pay(new CharaSlot { Level = 5, Mp = 10 }, 1001005, 1, cd)); // cooling
        cd.Clear();
        var chara = new CharaSlot { Level = 5, Mp = 10 };
        Assert.IsTrue(cost.Pay(chara, 1001005, 1, cd));
        Assert.AreEqual(0, chara.Mp);
        Assert.AreEqual(1000, cd[1001005]);
    }

    // Pct: level curve ends plus orb bonus (combo rate + synergy rate).
    [TestMethod]
    public void Pct_CurvePlusOrbs()
    {
        var defs = Defs(Active(),
            new SkillData { SkillId = SkillData.ComboAttack, OrbDmg = 2 },
            new SkillData { SkillId = SkillData.ComboSynergy, SynDmg = 3 });
        var dmg = new SkillDamage(defs);
        Assert.AreEqual(100, dmg.Pct(1001005, 1, 0, 0, 0)); // lv1: DmgLo
        Assert.AreEqual(200, dmg.Pct(1001005, 20, 0, 0, 0)); // max: DmgHi
        Assert.AreEqual(100 + 2 * (2 * 5 + 3 * 4), dmg.Pct(1001005, 1, 2, 5, 4));
    }

    // No orbs: combo/synergy defs never read (missing rows must not throw).
    [TestMethod]
    public void Pct_NoOrbsSkipsComboDefs()
    {
        var dmg = new SkillDamage(Defs(Active()));
        Assert.AreEqual(100, dmg.Pct(1001005, 1, 0, 0, 0));
    }
}
