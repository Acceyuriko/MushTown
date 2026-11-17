using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class SkillTextTests
{
    [TestMethod]
    public void Effect_Active_ShowsCostCooldownDamageTargets()
    {
        var skill = new SkillData { Kind = SkillData.KindActive, MaxLevel = 10, Mp = 5, CooldownMs = 3000, DmgLo = 150, DmgHi = 250, MobCount = 6 };
        Assert.AreEqual("MP Cost: 5, Cooldown: 3 sec, Damage: 250%, Max Targets: 6", SkillText.Effect(skill, 10));
    }

    [TestMethod]
    public void Effect_Passive_ShowsOnlyStatsAtLevel()
    {
        var skill = new SkillData { Kind = SkillData.KindPassive, MaxLevel = 10, HpPct = 2, MovePct = 1 };
        Assert.AreEqual("Max HP: +20%, Movement Speed and Jump: +10%", SkillText.Effect(skill, 10));
    }

    [TestMethod]
    public void Effect_Buff_ShowsDuration()
    {
        var skill = new SkillData { Kind = SkillData.KindBuff, MaxLevel = 10, Mp = 10, CooldownMs = 10000, BuffMs = 120000, HpPct = 3 };
        Assert.AreEqual("MP Cost: 10, Cooldown: 10 sec, Duration: 120 sec, Max HP: +30%", SkillText.Effect(skill, 10));
    }
}
