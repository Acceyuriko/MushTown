using Microsoft.Xna.Framework;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The folded chat reads the newest line out of the log every frame.
[TestClass]
public sealed class ChatLogTests
{
    // The caller draws every frame with the same list: the runs of one call
    // must not pile up on the ones before it.
    [TestMethod]
    public void NewestRuns_ReplacesTheRuns()
    {
        var log = new ChatLog(new PanelLayout());
        log.AddLine("Guide", "hello");
        var runs = new List<ChatLog.Run>();
        log.NewestRuns(runs, 6);
        log.NewestRuns(runs, 6);
        Assert.AreEqual(2, runs.Count);
        Assert.AreEqual("Guide", runs[0].Text);
        Assert.AreEqual("hello", runs[1].Text);
    }

    // The tab in use does not hide the last message from the folded bar: the
    // bar shows the last message, whatever page the log shows.
    [TestMethod]
    public void NewestRuns_IgnoresTheTabInUse()
    {
        var log = new ChatLog(new PanelLayout()) { Current = ChatLog.Tab.Etc };
        log.AddSystem("a system line");
        log.AddLine("Guide", "the newest line");
        var runs = new List<ChatLog.Run>();
        log.NewestRuns(runs, 6);
        Assert.AreEqual(2, runs.Count);
        Assert.AreEqual(ChatLog.NameColor, runs[0].Color);
        Assert.AreEqual(6, runs[1].Gap);
        Assert.AreEqual(Color.White, runs[1].Color);
        Assert.AreEqual("the newest line", runs[1].Text);
    }

    // A system line is one gold run, and it owns the whole bar.
    [TestMethod]
    public void NewestRuns_SystemLineIsOneGoldRun()
    {
        var log = new ChatLog(new PanelLayout());
        log.AddLine("Guide", "a player line");
        log.AddSystem("a system line");
        var runs = new List<ChatLog.Run>();
        log.NewestRuns(runs, 6);
        Assert.AreEqual(1, runs.Count);
        Assert.AreEqual(ChatLog.SystemColor, runs[0].Color);
        Assert.AreEqual(0, runs[0].Gap);
    }
}