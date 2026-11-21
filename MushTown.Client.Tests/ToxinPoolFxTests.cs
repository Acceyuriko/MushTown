using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The look of Creeping Toxin's pool (GroundZoneFx.StartPool) must match the server's pools in time: every pool comes up at ToxinFirstPoolMs plus its steps of the spreading, and a pool that
// blew up (Explode) comes up again after the wait of WZ y and the steps from the totem. Both are worked out by ToxinPoolLayout, the code the server uses too. The art here is dummy (one
// frame of 100 ms for every phase): only the times are looked at.
[TestClass]
public class ToxinPoolFxTests
{
    private const int StepMs = 900;
    private const int StepPx = 200;

    private static WzSkillData Toxin()
    {
        var wz = new WzSkillData { SkillId = WzSkillData.CreepingToxin, MaxLevel = 14 };
        wz.Common["y"] = SkillFormula.Levels("120", 14);
        wz.Common["u"] = SkillFormula.Levels(StepPx.ToString(), 14);
        for (int kind = 0; kind < WzSkillData.ToxinTileKinds; kind++)
            foreach (var phase in WzSkillData.ToxinPhases)
                for (int layer = 0; layer < WzSkillData.ToxinLayers; layer++)
                    wz.Art[WzSkillData.ToxinTile(kind, phase, layer)] = new WzArt { Origins = new[] { new[] { 0, 0 } }, Delays = new[] { phase == "end" ? 50 : 100 }, Frames = { "frame" } };
        return wz;
    }

    // A pool of nine pools (the totem at 0, the edges at -932 and 932), just started.
    private static GroundZoneFx Start(WzSkillData wz)
    {
        var fx = new GameObject().Add(new GroundZoneFx());
        fx.Start(new ZoneEvent(1, WzSkillData.CreepingToxin, 7, -932, 932, 0, 60000, 0, false, 0, StepMs, 0), wz);
        return fx;
    }

    // Steps the pool 10 ms at a time and says, for every pool, the first time it was shown.
    private static Dictionary<int, int> FirstShown(GroundZoneFx fx, int fromMs, int toMs)
    {
        var first = new Dictionary<int, int>();
        for (int ms = fromMs + 10; ms <= toMs; ms += 10)
        {
            fx.Update(0.01f);
            for (int i = 0; i < fx.Count; i++)
                if (fx.Shown[i] && !first.ContainsKey(fx.Piece[i])) first[fx.Piece[i]] = ms;
        }
        return first;
    }

    [TestMethod]
    public void EveryPoolComesUpAtTheTimeTheLayoutSays()
    {
        var fx = Start(Toxin());
        var pieces = ToxinPoolLayout.Pieces(0, -932, 932);
        var first = FirstShown(fx, 0, 6000);
        Assert.AreEqual(pieces.Count, first.Count);
        for (int p = 0; p < pieces.Count; p++)
        {
            int expected = WzSkillData.ToxinFirstPoolMs + ToxinPoolLayout.SpreadMs(pieces[p].Center, 0, StepMs, StepPx);
            Assert.IsTrue(first[p] >= expected && first[p] <= expected + 10, $"pool {p} at {pieces[p].Center}: shown at {first[p]}, expected {expected}");
        }
    }

    // In the last ms of the zone every pool plays its end phase (50 ms here) and is gone, so no pool is left when the zone ends.
    [TestMethod]
    public void EveryPoolIsGoneWhenTheZoneEnds()
    {
        var fx = Start(Toxin());
        FirstShown(fx, 0, 59000);
        Assert.IsTrue(Enumerable.Range(0, fx.Count).All(i => fx.Shown[i]), "all pools stand before the end");
        while (!fx.Owner.Dead) fx.Update(0.01f);
        Assert.IsTrue(Enumerable.Range(0, fx.Count).All(i => !fx.Shown[i]), "no pool is left");
    }

    // The farthest pool (index 0) blows up at 5000 ms: it plays its end2 art (100 ms), is gone, and comes up again y x 10 ms (1200 ms) plus the steps to it (4 x 900 ms) after the blast.
    [TestMethod]
    public void APoolThatBlewUpComesUpAgainAfterTheWaitAndTheStepsFromTheTotem()
    {
        var wz = Toxin();
        var fx = Start(wz);
        FirstShown(fx, 0, 5000);
        fx.Explode(0);
        var pieces = ToxinPoolLayout.Pieces(0, -932, 932);
        int expected = 5000 + (int)wz.Value("y", 14) * WzSkillData.ToxinWzUnitMs + ToxinPoolLayout.SpreadMs(pieces[0].Center, 0, StepMs, StepPx);
        Assert.AreEqual(1200 + 4 * StepMs, expected - 5000);
        bool gone = false;
        int back = 0;
        for (int ms = 5010; back == 0 && ms <= 12000; ms += 10)
        {
            fx.Update(0.01f);
            bool shown = Enumerable.Range(0, fx.Count).Any(i => fx.Piece[i] == 0 && fx.Shown[i]);
            if (!shown) gone = true;
            else if (gone) back = ms;
        }
        Assert.IsTrue(gone, "the pool is gone after its end2 art");
        Assert.IsTrue(back >= expected - 11 && back <= expected + 11, $"back at {back}, expected {expected}");
        Assert.IsTrue(Enumerable.Range(0, fx.Count).Where(i => fx.Piece[i] == 4).All(i => fx.Shown[i]), "the other pools stay");
    }
}
