using MushTown.Shared;

namespace MushTown.Tests;

// The projectile component: a straight flight at 600 px/s, whole milliseconds, one arrival.
[TestClass]
public class ProjectileTests
{
    private static Projectile Launched(float fromX, float fromY, float toX, float toY)
    {
        var node = new GameObject();
        var p = node.Add(new Projectile());
        p.Launch(fromX, fromY, toX, toY);
        return p;
    }

    // 300 px at 600 px/s = 500 ms. Halfway at 250 ms; it arrives at 500 ms, ends its node, and stays at the end point.
    [TestMethod]
    public void Flight_StraightLine_ArrivesOnTime()
    {
        var p = Launched(40, 60, 340, 60);
        Assert.AreEqual(500, p.DurationMs);
        p.Update(0.25f);
        Assert.AreEqual(190f, p.X, 0.01f);
        Assert.AreEqual(60f, p.Y);
        Assert.IsFalse(p.Arrived);
        p.Update(0.249f);
        Assert.IsFalse(p.Arrived);
        Assert.IsFalse(p.Owner.Dead);
        p.Update(0.001f);
        Assert.IsTrue(p.Arrived);
        Assert.IsTrue(p.Owner.Dead);
        Assert.AreEqual(340f, p.X);
        p.Update(1f); // a later step keeps the end point
        Assert.AreEqual(340f, p.X);
    }

    // The 3-4-5 flight (60 px across, 80 px down = 100 px) takes 166.67 ms, so 167. A reused node comes back clean.
    [TestMethod]
    public void Flight_Diagonal_RoundsUp_AndRelaunchResets()
    {
        var p = Launched(0, 0, 60, 80);
        Assert.AreEqual(167, p.DurationMs);
        p.Update(1f);
        Assert.IsTrue(p.Arrived);
        p.Launch(0, 0, 60, 80);
        Assert.IsFalse(p.Arrived);
        Assert.IsFalse(p.Owner.Dead);
        Assert.AreEqual(0f, p.X);
    }

    // No distance: it arrives on the first step.
    [TestMethod]
    public void Flight_NoDistance_ArrivesAtOnce()
    {
        var p = Launched(5, 5, 5, 5);
        Assert.AreEqual(0, p.DurationMs);
        p.Update(0.016f);
        Assert.IsTrue(p.Arrived);
    }

    // The attack-frame delay keeps the ball at its start until it ends, then the flight runs normally.
    [TestMethod]
    public void Delay_WaitsForTheAttackFrame_ThenFlies()
    {
        var p = Launched(40, 60, 340, 60);
        p.Launch(40, 60, 340, 60, 100);
        Assert.IsFalse(p.Started);
        p.Update(0.05f);
        Assert.IsFalse(p.Started);
        Assert.AreEqual(40f, p.X);
        p.Update(0.05f);
        Assert.IsTrue(p.Started);
        Assert.AreEqual(40f, p.X);
        p.Update(0.25f);
        Assert.AreEqual(190f, p.X, 0.01f);
        Assert.IsFalse(p.Arrived);
    }
}
