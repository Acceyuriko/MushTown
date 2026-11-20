using MushTown.Client;

namespace MushTown.Client.Tests;

[TestClass]
public class TimerLabelTests
{
    [TestMethod]
    public void UnderOneMinute_WholeSecondsRoundedDown()
    {
        Assert.AreEqual((59, false), TimerOverlay.Label(59900));
        Assert.AreEqual((1, false), TimerOverlay.Label(1900));
        Assert.AreEqual((0, false), TimerOverlay.Label(500)); // number 0 is not drawn
        Assert.AreEqual((0, false), TimerOverlay.Label(480)); // Teleport cooldown
    }

    [TestMethod]
    public void FromOneMinute_WholeMinutesRoundedDown()
    {
        Assert.AreEqual((1, true), TimerOverlay.Label(60000));
        Assert.AreEqual((1, true), TimerOverlay.Label(119000));
        Assert.AreEqual((2, true), TimerOverlay.Label(120000));
    }

    // Buff and cooldown at once: the buff time shows; once the buff is over (left 0) the cooldown shows; with neither, nothing. Only the shown one asks its whole time.
    [TestMethod]
    public void PickTimer_BuffFirst_ThenCooldown_ThenNothing()
    {
        Assert.AreEqual(new SkillTimer(30000, 120000), MushTownGame.PickTimer(30000, () => 120000, 50000, () => throw new InvalidOperationException()));
        Assert.AreEqual(new SkillTimer(50000, 60000), MushTownGame.PickTimer(0, () => throw new InvalidOperationException(), 50000, () => 60000));
        Assert.IsFalse(MushTownGame.PickTimer(0, () => throw new InvalidOperationException(), 0, () => throw new InvalidOperationException()).Active);
    }
}
