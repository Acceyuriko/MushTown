using MushTown.Shared;

namespace MushTown.Tests;

// The body action script of Poison Breath (WZ Character 00002000.img/poisonBreath: alert 2 for 180 ms, swingO2 0 for 240 ms, swingO2 2 for 360 ms).
[TestClass]
public class ActionScriptTests
{
    private static readonly List<ActionStep> PoisonBreath = new()
    {
        new() { Action = "alert", Frame = 2, DelayMs = 180 },
        new() { Action = "swingO2", Frame = 0, DelayMs = 240 },
        new() { Action = "swingO2", Frame = 2, DelayMs = 360 },
    };

    // The step on show follows the clock: a step lasts its time, the edges belong to the next step, and after the end the last step stays.
    [TestMethod]
    public void Current_FollowsTheClock_LastStepStays()
    {
        var script = new ActionScript();
        Assert.IsFalse(script.Active);
        script.Start(PoisonBreath);
        Assert.IsTrue(script.Active);
        void At(float ms, string action, int frame)
        {
            script.Start(PoisonBreath);
            script.Tick(ms);
            Assert.AreEqual((action, frame), (script.Current.Action, script.Current.Frame), $"at {ms} ms");
        }
        At(0, "alert", 2);
        At(179, "alert", 2);
        At(180, "swingO2", 0);
        At(419, "swingO2", 0);
        At(420, "swingO2", 2); // the attack frame: 180 + 240 ms (the same time WzSkillData.AttackDelayFrom gives)
        At(779, "swingO2", 2);
        At(5000, "swingO2", 2);
        Assert.AreEqual(420, WzSkillData.AttackDelayFrom(new int?[] { -180, -240, 360 }));
        Assert.AreEqual(780, PoisonBreath.Sum(s => s.DelayMs));
    }

    // The Client ticks it by the real time divided by the attack speed scale, so the whole script lasts as long as the cast lock (780 ms x scale).
    [TestMethod]
    public void Scaled_LastsAsLongAsTheLock()
    {
        var script = new ActionScript();
        var fighter = new Fighter();
        float scale = 0.5f;
        script.Start(PoisonBreath);
        fighter.StartAttack(PoisonBreath.Sum(s => s.DelayMs), scale);
        Assert.AreEqual(390f, fighter.AttackLock);
        float elapsed = 0;
        while (fighter.AttackLock > 0)
        {
            fighter.Tick(10);
            script.Tick(10 / fighter.AttackScale);
            elapsed += 10;
        }
        Assert.AreEqual(390f, elapsed);
        Assert.AreEqual(("swingO2", 2), (script.Current.Action, script.Current.Frame));
    }

    // A plain swing or a basic attack clears it; a new cast starts it from the first step.
    [TestMethod]
    public void Clear_And_Restart()
    {
        var script = new ActionScript();
        script.Start(PoisonBreath);
        script.Tick(500);
        script.Clear();
        Assert.IsFalse(script.Active);
        script.Start(PoisonBreath);
        Assert.AreEqual(("alert", 2), (script.Current.Action, script.Current.Frame));
    }
}
