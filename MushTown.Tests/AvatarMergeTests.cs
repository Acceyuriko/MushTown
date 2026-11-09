using MushTown.Shared;

namespace MushTown.Tests;

// The doll merge: the body bake carries the default hair and face, and an
// exported hair/face part replaces those baked parts. Gear draws over the
// body, and every list keeps the back-to-front z order.
[TestClass]
public sealed class AvatarMergeTests
{
    private static CharaPart Part(string img, int z) => new() { Img = img, Z = z };

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
