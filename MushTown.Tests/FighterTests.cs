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

    // A skill with a buff and a cooldown at once: the buff leaves the list when it ends and the cooldown is still running, so the countdown of the icon
    // (HudSkill.TimerOf: the buff time while the buff is listed, else the cooldown) shows the buff first and the cooldown after it.
    [TestMethod]
    public void BuffEnds_CooldownKeepsRunning()
    {
        var f = new Fighter();
        f.Buffs[1000003] = 1000;
        f.Cooldowns[1000003] = 5000;
        f.Tick(600f);
        Assert.IsTrue(f.Buffs[1000003] > 0 && f.Cooldowns[1000003] > 0);
        f.Tick(600f);
        Assert.IsFalse(f.Buffs.ContainsKey(1000003));
        Assert.AreEqual(3800f, f.Cooldowns[1000003]);
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

    // A swing of 800 ms at stage 4 (scale 1.0) and at stage 8 (scale 0.75): the lock runs 800 and 600 ms, counted in 60 Hz frames (within one frame).
    [TestMethod]
    public void StartAttack_LockFollowsTheStage_Stage4Against8()
    {
        static double MsLocked(int stage)
        {
            var f = new Fighter();
            f.StartAttack(800, (float)StatTables.DelayScale(stage));
            int frames = 0;
            while (f.AttackLock > 0)
            {
                f.Tick(1000f / 60f);
                frames++;
            }
            return frames * 1000.0 / 60.0;
        }
        Assert.AreEqual(800, MsLocked(4), 17); // within one frame
        Assert.AreEqual(600, MsLocked(8), 17);
        Assert.AreEqual(500, MsLocked(10), 17);
        Assert.AreEqual(950, MsLocked(1), 17);
    }

    [TestMethod]
    public void StartAttack_RemembersTheScaleForTheSwingFrames()
    {
        var f = new Fighter();
        f.StartAttack(800, 0.75f);
        Assert.AreEqual(600f, f.AttackLock, 0.001f);
        Assert.AreEqual(0.75f, f.AttackScale);
    }
}
