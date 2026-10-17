using MushTown.Shared;

namespace MushTown.Tests;

// Combat clocks: the lock ticks to zero, cooldowns clamp (never removed),
// buffs tick out in order, orbs sit still, recovery fires every 10s.
[TestClass]
public sealed class FighterTests
{
    [TestMethod]
    public void Lock_TicksDownAndClampsAtZero()
    {
        var f = new Fighter { AttackLock = 800 };
        f.Tick(16.67f);
        Assert.AreEqual(800f - 16.67f, f.AttackLock, 0.01f);
        f.Tick(10000f);
        Assert.AreEqual(0f, f.AttackLock);
    }

    [TestMethod]
    public void Cooldowns_TickDownAndStayListed()
    {
        var f = new Fighter();
        f.Cooldowns[1000003] = 500;
        f.Tick(200f);
        Assert.AreEqual(300f, f.Cooldowns[1000003]);
        f.Tick(10000f);
        Assert.AreEqual(0f, f.Cooldowns[1000003]); // spent timers stay at 0, listed
    }

    [TestMethod]
    public void Buffs_ExpireInOrderAndDrain()
    {
        var f = new Fighter();
        f.Buffs[1101001] = 100;
        f.Buffs[1101002] = 10000;
        f.Tick(200f);
        CollectionAssert.AreEqual(new[] { 1101001 }, f.ExpiredBuffs);
        Assert.IsFalse(f.Buffs.ContainsKey(1101001));
        Assert.AreEqual(10000f - 200f, f.Buffs[1101002]);
        f.Tick(200f);
        Assert.AreEqual(0, f.ExpiredBuffs.Count); // outputs are fresh every tick
    }

    [TestMethod]
    public void Tick_LeavesOrbsAlone()
    {
        var f = new Fighter { Orbs = 3 };
        f.Tick(100000f);
        Assert.AreEqual(3, f.Orbs);
    }

    [TestMethod]
    public void Regen_FiresEveryTenSeconds()
    {
        var f = new Fighter();
        f.Tick(9999f);
        Assert.IsFalse(f.RegenDue);
        f.Tick(1f);
        Assert.IsTrue(f.RegenDue);
        f.Tick(9999f);
        Assert.IsFalse(f.RegenDue);
        f.Tick(1f);
        Assert.IsTrue(f.RegenDue);
    }
}
