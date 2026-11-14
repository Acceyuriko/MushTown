using MushTown.Shared;

namespace MushTown.Tests;

// The doll merge: the body bake carries the default hair and face, and an
// exported hair/face part replaces those baked parts. Gear draws over the
// body, and every list keeps the back-to-front z order.
[TestClass]
public sealed class AvatarMergeTests
{
    private static CharaPart Part(string img, int z) => new() { Image = "", Img = img, Z = z };

    [TestMethod]
    public void Parts_ReplacesBakedLook_AndSortsBackToFront()
    {
        var body = new List<CharaPart>
        {
            Part("00030020", 42), // default hair, replaced
            Part("00002000", 97),
            Part("01060026", 94),
            Part("00012000", 59),
            Part("00020000", 55), // default face, replaced
        };
        var gear = new[] { Part("01040015", 65) };
        var hair = new[] { Part("00065370", 42), Part("00065370", 103) };
        var face = new[] { Part("00020053", 55) };

        var parts = AvatarMerge.Parts(body, gear, hair, face, "00030020", "00020000");

        CollectionAssert.AreEqual(
            new[] { "00065370", "00002000", "01060026", "01040015", "00012000", "00020053", "00065370" },
            parts.Select(p => p.Img).ToArray());
        CollectionAssert.AreEqual(
            new[] { 103, 97, 94, 65, 59, 55, 42 },
            parts.Select(p => p.Z).ToArray());
    }

    [TestMethod]
    public void Parts_HatHidesStand1HairOverHead()
    {
        var body = new List<CharaPart>
        {
            new() { Image = "chara/body/stand1/0_00030020_hairBelowBody.png", Img = "00030020", Z = 103 },
            new() { Image = "chara/body/stand1/0_00002000_body.png", Img = "00002000", Z = 97 },
            new() { Image = "chara/body/stand1/0_00030020_hairOverHead.png", Img = "00030020", Z = 42 },
            new() { Image = "chara/body/stand1/0_00030020_hair.png", Img = "00030020", Z = 41 },
            new() { Image = "chara/body/stand1/0_00030020_backHairOverCape.png", Img = "00030020", Z = 50 },
            new() { Image = "chara/body/stand1/0_00030020_backHairBelowCap.png", Img = "00030020", Z = 39 },
            new() { Image = "chara/body/stand1/0_00030020_backHair.png", Img = "00030020", Z = 40 },
        };
        var gear = new[]
        {
            new CharaPart { Image = "chara/gear/1000025/stand1/0_01000025_cap.png", Img = "01000025", Z = 45 },
        };

        var parts = AvatarMerge.Parts(body, gear, null, null, "00030020", "00020000", hairCover: true);

        Assert.IsFalse(parts.Any(p => p.Image.EndsWith("_hairOverHead.png", StringComparison.Ordinal)));
        Assert.IsFalse(parts.Any(p => p.Image.EndsWith("_backHairOverCape.png", StringComparison.Ordinal)));
        Assert.IsFalse(parts.Any(p => p.Image.EndsWith("_backHair.png", StringComparison.Ordinal)));
        Assert.IsTrue(parts.Any(p => p.Image.EndsWith("_hair.png", StringComparison.Ordinal)));
        Assert.IsTrue(parts.Any(p => p.Image.EndsWith("_backHairBelowCap.png", StringComparison.Ordinal)));
        Assert.IsTrue(parts.Any(p => p.Image.EndsWith("_hairBelowBody.png", StringComparison.Ordinal)));
        Assert.IsTrue(parts.Any(p => p.Img == "01000025"));
    }

    // 00033580's stand1.json used to contain only hairOverHead after export.
    // A hat must keep the newly exported hair-under-cap part, or this hair
    // style becomes bald under the cap.
    [TestMethod]
    public void Parts_HatKeepsUnderCapHair_ForHairStyle33580()
    {
        var hair = new[]
        {
            new CharaPart { Image = "chara/hair/00033580/stand1/0_00033580_hairOverHead.png", Img = "00033580", Z = 42 },
            new CharaPart { Image = "chara/hair/00033580/stand1/0_00033580_hair.png", Img = "00033580", Z = 41 },
        };
        var gear = new[]
        {
            new CharaPart { Image = "chara/gear/1000025/stand1/0_01000025_cap.png", Img = "01000025", Z = 45 },
        };

        var parts = AvatarMerge.Parts(Array.Empty<CharaPart>(), gear, hair, null, "00030020", "00020000", hairCover: true);

        Assert.IsFalse(parts.Any(p => p.Image.EndsWith("_hairOverHead.png", StringComparison.Ordinal)));
        Assert.IsTrue(parts.Any(p => p.Image.EndsWith("_hair.png", StringComparison.Ordinal)));
        Assert.IsTrue(parts.Any(p => p.Img == "01000025"));
    }

    [TestMethod]
    public void Parts_NoHatKeepsOverHeadAndDropsUnderCapHair()
    {
        var body = new List<CharaPart>
        {
            new() { Image = "chara/body/stand1/0_00030020_hairOverHead.png", Img = "00030020", Z = 42 },
            new() { Image = "chara/body/stand1/0_00030020_hair.png", Img = "00030020", Z = 41 },
            new() { Image = "chara/body/stand1/0_00030020_backHairBelowCap.png", Img = "00030020", Z = 39 },
        };

        var parts = AvatarMerge.Parts(body, Array.Empty<CharaPart>(), null, null, "00030020", "00020000");

        Assert.IsTrue(parts.Any(p => p.Image.EndsWith("_hairOverHead.png", StringComparison.Ordinal)));
        Assert.IsFalse(parts.Any(p => p.Image.EndsWith("_hair.png", StringComparison.Ordinal)));
        Assert.IsFalse(parts.Any(p => p.Image.EndsWith("_backHairBelowCap.png", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Parts_KeepsBakedLook_WhenNoPartSetIsGiven()
    {
        var body = new List<CharaPart>
        {
            Part("00030020", 42),
            Part("00002000", 97),
            Part("00012000", 59),
            Part("00020000", 55),
        };

        var parts = AvatarMerge.Parts(body, Array.Empty<CharaPart>(), null, null, "00030020", "00020000");

        CollectionAssert.AreEqual(
            new[] { "00002000", "00012000", "00020000", "00030020" },
            parts.Select(p => p.Img).ToArray());
    }

    [TestMethod]
    public void Parts_EmptyFaceFrame_StillReplacesTheBakedFace()
    {
        var body = new List<CharaPart>
        {
            Part("00030020", 42),
            Part("00002000", 97),
            Part("00020000", 55),
        };

        var parts = AvatarMerge.Parts(body, Array.Empty<CharaPart>(), null, Array.Empty<CharaPart>(), "00030020", "00020000");

        CollectionAssert.AreEqual(
            new[] { "00002000", "00030020" },
            parts.Select(p => p.Img).ToArray());
    }
}
