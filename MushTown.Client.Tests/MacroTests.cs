using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// A macro casts its skills one after another; a skill that cannot cast is skipped; a macro key
// while one runs does nothing. The bound key reads back as a Macro target.
[TestClass]
public class MacroTests
{
    // The pose of a test character: a cast of a castable skill locks it for three frames.
    private sealed class Pose
    {
        public int Lock;
        public readonly HashSet<int> Castable;
        public readonly List<(int Frame, int Skill)> Casts = new();
        public Pose(params int[] castable) { Castable = castable.ToHashSet(); }

        public void Frame(MacroRun run, int frame)
        {
            run.Step(() => Lock > 0, id =>
            {
                if (!Castable.Contains(id)) return;
                Casts.Add((frame, id));
                Lock = 3;
            });
            if (Lock > 0) Lock--;
        }
    }

    private static List<(int Frame, int Skill)> Run(Pose pose, MacroRun run, int frames)
    {
        for (int f = 0; f < frames; f++) pose.Frame(run, f);
        return pose.Casts;
    }

    [TestMethod]
    public void Step_CastsTheSkillsInOrder_EachAfterThePoseIsFree()
    {
        var run = new MacroRun();
        run.Start(new[] { 11, 12, 13 });
        var casts = Run(new Pose(11, 12, 13), run, 20);
        CollectionAssert.AreEqual(new[] { (0, 11), (3, 12), (6, 13) }, casts);
    }

    [TestMethod]
    public void Step_ASkillThatCannotCastIsSkippedAtOnce()
    {
        var run = new MacroRun();
        run.Start(new[] { 11, 12, 13 });
        var casts = Run(new Pose(11, 13), run, 20); // 12: no MP, cooling down or wrong place
        CollectionAssert.AreEqual(new[] { (0, 11), (3, 13) }, casts);
    }

    [TestMethod]
    public void Step_EmptySlotsCastNothing()
    {
        var run = new MacroRun();
        run.Start(new[] { 0, 12, 0 });
        var casts = Run(new Pose(12), run, 20);
        CollectionAssert.AreEqual(new[] { (0, 12) }, casts);
    }

    [TestMethod]
    public void Start_WhileRunning_IsIgnored()
    {
        var run = new MacroRun();
        var pose = new Pose(11, 12, 13, 21);
        run.Start(new[] { 11, 12 });
        pose.Frame(run, 0);
        run.Start(new[] { 21 }); // the macro key again, mid-run
        for (int f = 1; f < 20; f++) pose.Frame(run, f);
        CollectionAssert.AreEqual(new[] { (0, 11), (3, 12) }, pose.Casts);
    }

    [TestMethod]
    public void Start_AfterTheRunEnded_RunsAgain()
    {
        var run = new MacroRun();
        var pose = new Pose(11);
        run.Start(new[] { 11 });
        for (int f = 0; f < 10; f++) pose.Frame(run, f);
        run.Start(new[] { 11 });
        for (int f = 10; f < 20; f++) pose.Frame(run, f);
        CollectionAssert.AreEqual(new[] { (0, 11), (10, 11) }, pose.Casts);
    }

    [TestMethod]
    public void Stop_EndsTheRun()
    {
        var run = new MacroRun();
        var pose = new Pose(11, 12);
        run.Start(new[] { 11, 12 });
        pose.Frame(run, 0);
        run.Stop();
        for (int f = 1; f < 20; f++) pose.Frame(run, f);
        CollectionAssert.AreEqual(new[] { (0, 11) }, pose.Casts);
    }

    private static SkillData Active(int id, int cooldownMs) =>
        new() { SkillId = id, Kind = SkillData.KindActive, Mp = 5, CooldownMs = cooldownMs, CastLockMs = 600 };

    // The real cast checks (SkillCost: MP, cooldown) behind the run, 60 frames a second. A Lv.1
    // character with plenty of MP: the level does not matter.
    private static List<int> RunWithCosts(int[] macro, params SkillData[] defs)
    {
        var byId = defs.ToDictionary(d => d.SkillId);
        var cost = new SkillCost(byId);
        var chara = new CharaSlot { Level = 1, Mp = 100 };
        var cooldowns = new Dictionary<int, float>();
        var casts = new List<int>();
        float lockMs = 0;
        var run = new MacroRun();
        run.Start(macro);
        for (int frame = 0; frame < 600; frame++)
        {
            run.Step(() => lockMs > 0, id =>
            {
                if (!cost.Pay(chara, id, 10, cooldowns)) return;
                casts.Add(id);
                lockMs = byId[id].CastLockMs;
            });
            lockMs = Math.Max(0, lockMs - 1000f / 60);
            foreach (int id in cooldowns.Keys.ToList()) cooldowns[id] = Math.Max(0, cooldowns[id] - 1000f / 60);
        }
        return casts;
    }

    // Three of the same skill with a cooldown: the first cast starts the cooldown (3 s), the pose
    // lock (0.6 s) ends long before it, so the second and the third are skipped as "cooling down".
    [TestMethod]
    public void ThreeOfTheSameSkill_WithACooldown_OnlyTheFirstCasts()
    {
        var skill = Active(1101011, 3000);
        CollectionAssert.AreEqual(new[] { 1101011 }, RunWithCosts(new[] { 1101011, 1101011, 1101011 }, skill));
    }

    // Slash Blast has no cooldown in the WZ: three of it cast three times, each after the pose.
    [TestMethod]
    public void ThreeOfTheSameSkill_WithNoCooldown_AllCast()
    {
        var skill = Active(1001005, 0);
        CollectionAssert.AreEqual(new[] { 1001005, 1001005, 1001005 }, RunWithCosts(new[] { 1001005, 1001005, 1001005 }, skill));
    }

    [TestMethod]
    public void ThreeDifferentSkills_AllCastInOrder()
    {
        var defs = new[] { Active(1001005, 3000), Active(1101011, 4000), Active(1111010, 5000) };
        CollectionAssert.AreEqual(new[] { 1001005, 1101011, 1111010 }, RunWithCosts(new[] { 1001005, 1101011, 1111010 }, defs));
    }

    [TestMethod]
    public void TryParse_MacroTarget()
    {
        Assert.IsTrue(BindTarget.TryParse("Macro:3", out var target));
        Assert.AreEqual(new BindTarget(BindKind.Macro, 3), target);
        Assert.IsFalse(BindTarget.TryParse("Macro:x", out _));
    }

    [TestMethod]
    public void ApplyMacros_BindsAKey_AndTheKeyMovesBetweenTargets()
    {
        var table = new Dictionary<string, string> { ["I"] = "Inventory" };
        KeyBindings.ApplyMacros(table, new() { [2] = "D1" });
        Assert.AreEqual("Macro:2", table["D1"]);
        KeyBindings.ApplyMacros(table, new() { [4] = "D1" }); // another macro takes the key
        Assert.AreEqual("Macro:4", table["D1"]);
        Assert.IsFalse(table.ContainsValue("Macro:2"));
        KeyBindings.ApplyMacros(table, new() { [4] = "" }); // a blank key unbinds it
        Assert.IsFalse(table.ContainsKey("D1"));
    }

    [TestMethod]
    public void Install_MacroKeyFiresWhileHeldWithoutThrottle()
    {
        var c = new InputController();
        c.Install(new() { ["D1"] = "Macro:0" });
        var target = new BindTarget(BindKind.Macro, 0);
        for (int i = 0; i < 3; i++)
        {
            c.Poll(new Microsoft.Xna.Framework.Input.KeyboardState(Microsoft.Xna.Framework.Input.Keys.D1), default, false, false, 0);
            Assert.IsTrue(c.Fire(target)); // the macro itself ignores a key while it runs
        }
    }
}
