using MushTown.Shared;

namespace MushTown.Tests;

// The body action scripts (WZ Character 00002000.img/<action>): Poison Breath (poisonBreath: alert 2 for 180 ms, swingO2 0 for 240 ms, swingO2 2 for 360 ms), and the three
// of the actions ticket T-20261120212041: Energy Bolt, Poison Mist and Elemental Adaptation (the numbers below are the WZ steps; a negative WZ delay is before the attack frame).
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

    private static readonly List<ActionStep> EnergyBolt = new()
    {
        new() { Action = "alert", Frame = 2, DelayMs = 60 },
        new() { Action = "swingO3", Frame = 0, DelayMs = 330 },
        new() { Action = "swingO3", Frame = 1, DelayMs = 270 },
    };

    private static readonly List<ActionStep> PoisonMist = new()
    {
        new() { Action = "alert", Frame = 2, DelayMs = 60 },
        new() { Action = "alert", Frame = 1, DelayMs = 60 },
        new() { Action = "alert", Frame = 0, DelayMs = 360 },
        new() { Action = "swingO1", Frame = 2, DelayMs = 180 },
    };

    private static readonly List<ActionStep> ElementalAdaptation = new()
    {
        new() { Action = "alert", Frame = 2, DelayMs = 120 },
        new() { Action = "alert", Frame = 1, DelayMs = 120 },
        new() { Action = "alert", Frame = 0, DelayMs = 300 },
        new() { Action = "swingO1", Frame = 2, DelayMs = 360 },
        new() { Action = "alert", Frame = 2, DelayMs = 90 },
    };

    // Each script follows its clock like Poison Breath's, its total is the cast lock, and the attack frame (the first positive WZ delay: the time of the steps before it) is
    // where the exporter and the judge already put it (WzSkillData.AttackDelayFrom): Energy Bolt's ball flies at 390 ms, Poison Mist's zone starts at 480 ms.
    [TestMethod]
    public void NewScripts_TotalsAndAttackFrames()
    {
        Assert.AreEqual(660, EnergyBolt.Sum(s => s.DelayMs));
        Assert.AreEqual(390, WzSkillData.AttackDelayFrom(new int?[] { -60, -330, 270 }));
        Assert.AreEqual(390, EnergyBolt.Take(2).Sum(s => s.DelayMs));
        Assert.AreEqual(660, PoisonMist.Sum(s => s.DelayMs));
        Assert.AreEqual(480, WzSkillData.AttackDelayFrom(new int?[] { -60, -60, -360, 180 }));
        Assert.AreEqual(480, PoisonMist.Take(3).Sum(s => s.DelayMs));
        Assert.AreEqual(990, ElementalAdaptation.Sum(s => s.DelayMs));
        Assert.AreEqual(540, ElementalAdaptation.Take(3).Sum(s => s.DelayMs));
    }

    [TestMethod]
    public void ElementalAdaptation_PlaysAllFiveSteps_ThenHoldsTheLast()
    {
        var script = new ActionScript();
        void At(float ms, string action, int frame)
        {
            script.Start(ElementalAdaptation);
            script.Tick(ms);
            Assert.AreEqual((action, frame), (script.Current.Action, script.Current.Frame), $"at {ms} ms");
        }
        At(0, "alert", 2);
        At(120, "alert", 1);
        At(240, "alert", 0);
        At(539, "alert", 0);
        At(540, "swingO1", 2);
        At(899, "swingO1", 2);
        At(900, "alert", 2);
        At(5000, "alert", 2);
    }

    [TestMethod]
    public void PoisonMist_And_EnergyBolt_FollowTheirClocks()
    {
        var script = new ActionScript();
        void At(List<ActionStep> steps, float ms, string action, int frame)
        {
            script.Start(steps);
            script.Tick(ms);
            Assert.AreEqual((action, frame), (script.Current.Action, script.Current.Frame), $"at {ms} ms");
        }
        At(PoisonMist, 59, "alert", 2);
        At(PoisonMist, 60, "alert", 1);
        At(PoisonMist, 120, "alert", 0);
        At(PoisonMist, 479, "alert", 0);
        At(PoisonMist, 480, "swingO1", 2);
        At(PoisonMist, 2000, "swingO1", 2);
        At(EnergyBolt, 0, "alert", 2);
        At(EnergyBolt, 60, "swingO3", 0);
        At(EnergyBolt, 389, "swingO3", 0);
        At(EnergyBolt, 390, "swingO3", 1);
    }
}
