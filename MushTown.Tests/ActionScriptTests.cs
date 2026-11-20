using MushTown.Shared;

namespace MushTown.Tests;

// The body action scripts (WZ Character 00002000.img/<action>): Poison Breath (poisonBreath: alert 2 for 180 ms, swingO2 0 for 240 ms, swingO2 2 for 360 ms), and the three
// of the actions ticket T-20261120212041: Energy Bolt, Poison Mist and Elemental Adaptation, and Flame Orb and Explosion of T-20261119195811 (the numbers below are the WZ steps; a negative WZ delay is before the attack frame).
[TestClass]
public class ActionScriptTests
{
    // Every step of every body script is an action the game knows: the avatar art is loaded per Net.Actions and the net sends the code (Net.ActionCode), so a step
    // with an action missing from Net.Actions would throw at the cast (Flame Orb and Explosion use stabO2). The Client checks the exported steps at start (Menu).
    [TestMethod]
    public void EveryScriptStep_IsAKnownAction()
    {
        var scripts = new[] { PoisonBreath, EnergyBolt, PoisonMist, ElementalAdaptation, FlameOrb, Explosion };
        foreach (var step in scripts.SelectMany(script => script))
        {
            Assert.IsTrue(Net.Actions.Contains(step.Action), step.Action);
            Assert.AreEqual(step.Action, Net.ActionName(Net.ActionCode(step.Action)));
        }
        Assert.AreEqual(WzSkillData.ActionScripts.Count, scripts.Length);
    }

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

    // Flame Orb (flameOrb) and Explosion (explosion): the WZ steps of ticket T-20261119195811. The judge time is the time of the steps before the attack frame (the first positive WZ delay).
    private static readonly List<ActionStep> FlameOrb = new()
    {
        new() { Action = "alert", Frame = 2, DelayMs = 90 },
        new() { Action = "swingO2", Frame = 0, DelayMs = 210 },
        new() { Action = "stabO2", Frame = 1, DelayMs = 420 },
    };

    private static readonly List<ActionStep> Explosion = new()
    {
        new() { Action = "alert", Frame = 2, DelayMs = 30 },
        new() { Action = "swingO2", Frame = 1, DelayMs = 60 },
        new() { Action = "swingO2", Frame = 0, DelayMs = 270 },
        new() { Action = "stabO2", Frame = 1, DelayMs = 30 },
        new() { Action = "stabO2", Frame = 1, DelayMs = 480 },
    };

    // The scripts do not move the damage: Flame Orb judges at 300 ms (90 + 210), Explosion at 390 ms (30 + 60 + 270 + 30), the same times WzSkillData.AttackDelayFrom gave
    // for their actions before the scripts; the whole cast is 720 and 870 ms.
    [TestMethod]
    public void FlameOrb_And_Explosion_TotalsAndAttackFrames()
    {
        Assert.AreEqual(300, WzSkillData.AttackDelayFrom(new int?[] { -90, -210, 420 }));
        Assert.AreEqual(300, FlameOrb.Take(2).Sum(s => s.DelayMs));
        Assert.AreEqual(720, FlameOrb.Sum(s => s.DelayMs));
        Assert.AreEqual(390, WzSkillData.AttackDelayFrom(new int?[] { -30, -60, -270, -30, 480 }));
        Assert.AreEqual(390, Explosion.Take(4).Sum(s => s.DelayMs));
        Assert.AreEqual(870, Explosion.Sum(s => s.DelayMs));
    }

    [TestMethod]
    public void FlameOrb_And_Explosion_FollowTheirClocks()
    {
        var script = new ActionScript();
        void At(List<ActionStep> steps, float ms, string action, int frame)
        {
            script.Start(steps);
            script.Tick(ms);
            Assert.AreEqual((action, frame), (script.Current.Action, script.Current.Frame), $"at {ms} ms");
        }
        At(FlameOrb, 89, "alert", 2);
        At(FlameOrb, 90, "swingO2", 0);
        At(FlameOrb, 299, "swingO2", 0);
        At(FlameOrb, 300, "stabO2", 1); // the attack frame
        At(FlameOrb, 5000, "stabO2", 1);
        At(Explosion, 0, "alert", 2);
        At(Explosion, 30, "swingO2", 1);
        At(Explosion, 90, "swingO2", 0);
        At(Explosion, 360, "stabO2", 1);
        At(Explosion, 389, "stabO2", 1);
        At(Explosion, 390, "stabO2", 1); // the attack frame is the second stabO2 step: the pose is the same frame as the step before
        At(Explosion, 5000, "stabO2", 1);
    }

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
