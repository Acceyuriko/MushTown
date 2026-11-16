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

    [TestMethod]
    public void KnockDirectionFollowsTheSourceNotTheFacing()
    {
        Assert.AreEqual(1, HurtGuard.KnockDir(200, 150)); // source on the left: pushed right
        Assert.AreEqual(-1, HurtGuard.KnockDir(200, 250)); // source on the right: pushed left
    }

    [TestMethod]
    public void FacingTheSourceStillGetsPushedAway()
    {
        var map = new MapData
        {
            Info = new MapInfo { VrLeft = 0, VrTop = 0, VrRight = 1000, VrBottom = 600 },
            Footholds = new List<FootholdItem> { new() { X1 = 0, Y1 = 300, X2 = 1000, Y2 = 300 } },
        };
        var m = new PlayerMover { Map = map, X = 500, Y = 300, FacingRight = true };
        m.Ground = map.Footholds[0];
        m.Knock(HurtGuard.KnockDir(m.X, 600)); // the mob is on the right, and so is the facing
        m.Input = new PlayerInput { Locked = true, Walk = PlayerMover.WalkSpeed, Jump = PlayerMover.JumpV0 };
        for (int i = 0; i < 120 && m.Ground == null; i++) m.Update(1f / 60f);
        Assert.IsTrue(m.X < 500, "moved away from the mob");
    }
}
