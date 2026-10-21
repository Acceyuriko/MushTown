using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public sealed class SoundKeyTests
{
    [TestMethod]
    public void FromBgm_RealTrack_LowerStripped()
    {
        Assert.AreEqual("bgm58_thebeginnigoftheadventure",
            SoundKey.FromBgm("Bgm58/The Beginnig of The Adventure"));
    }

    [TestMethod]
    public void FromBgm_Malformed_Empty()
    {
        Assert.AreEqual("", SoundKey.FromBgm("noSlash"));
        Assert.AreEqual("", SoundKey.FromBgm("a/b/c"));
        Assert.AreEqual("", SoundKey.FromBgm("/Track"));
        Assert.AreEqual("", SoundKey.FromBgm("Bgm/"));
    }

    [TestMethod]
    public void FromBgm_CanHitTheLoginKey()
    {
        // the exporter refuses this: the login theme owns the file
        Assert.AreEqual(SoundKey.Login, SoundKey.FromBgm("Bgm/Login"));
    }

    [TestMethod]
    public void MobDie_Routes()
    {
        Assert.AreEqual("die_boss", SoundKey.MobDie(100004, true));
        Assert.AreEqual("die_orange", SoundKey.MobDie(100004, false));
        Assert.AreEqual("die_snail", SoundKey.MobDie(100000, false));
    }
}
