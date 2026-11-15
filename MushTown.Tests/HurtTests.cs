using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class HurtTests
{
    [TestMethod]
    public void SecondHurtInsideTheWindowDoesNoDamage()
    {
        var chara = new CharaSlot { Hp = 100 };
        var guard = new HurtGuard();

        Assert.AreEqual(10L, guard.Apply(chara, 10, 0));
        Assert.AreEqual(90L, chara.Hp);

        Assert.AreEqual(0L, guard.Apply(chara, 10, 0));
        Assert.AreEqual(90L, chara.Hp, "the second touch inside the window must not change HP");

        guard.Tick(HurtGuard.InvincibleMs);
        Assert.AreEqual(10L, guard.Apply(chara, 10, 0));
        Assert.AreEqual(80L, chara.Hp);
    }

    [TestMethod]
    public void ZeroKnockbackChanceNeverPushes()
    {
        for (int roll = 0; roll < 100; roll++)
            Assert.IsFalse(HurtGuard.KnocksBack(0, roll));
    }

    [TestMethod]
    public void HundredKnockbackChanceAlwaysPushes()
    {
        for (int roll = 0; roll < 100; roll++)
            Assert.IsTrue(HurtGuard.KnocksBack(100, roll));
    }
}
