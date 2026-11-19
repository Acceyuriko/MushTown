using MushTown.Client;

namespace MushTown.Client.Tests;

// The layout of a damage number: steps, overlap, the dot and the unit letter, the critical mark in the first cell.
[TestClass]
public class DamageNumberLayoutTests
{
    private static GlyphPic Digit(int width, int ox = 0, int oy = 0) => new(GlyphKind.Digit, width, ox, oy);

    [TestMethod]
    public void Digits_TouchWhenNothingOverlaps()
    {
        var layout = DamageNumberLayout.Place(new[] { Digit(30, 15), Digit(20, 10), Digit(20, 10) }, 0, 0, null);
        Assert.AreEqual(0f, layout.Places[0].Left);
        Assert.AreEqual(30f, layout.Places[1].Left);
        Assert.AreEqual(50f, layout.Places[2].Left);
        Assert.AreEqual(70f, layout.Width);
    }

    [TestMethod]
    public void Digits_StepByTheShareOfTheirOwnWidth()
    {
        var layout = DamageNumberLayout.Place(new[] { Digit(40, 20), Digit(30, 15), Digit(30, 15) }, 30, 0, null);
        Assert.AreEqual(0f, layout.Places[0].Left);
        Assert.AreEqual(28f, layout.Places[1].Left); // 70 percent of 40
        Assert.AreEqual(49f, layout.Places[2].Left); // plus 70 percent of 30
        Assert.AreEqual(79f, layout.Width); // the last picture is whole
    }

    [TestMethod]
    public void OneCharacter_IsItsWholePicture()
    {
        var layout = DamageNumberLayout.Place(new[] { Digit(30, 10, 25) }, 30, 0, null);
        Assert.AreEqual(5f, layout.Places[0].Left); // 0 + 30 / 2 - 10
        Assert.AreEqual(-25f, layout.Places[0].Top);
        Assert.AreEqual(30f, layout.Width);
    }

    [TestMethod]
    public void DotAndUnit_FollowTheDigits()
    {
        var glyphs = new[]
        {
            Digit(40, 20),
            new GlyphPic(GlyphKind.Dot, 16, 8, 11),
            new GlyphPic(GlyphKind.Unit, 41, 20, 37),
        };
        var layout = DamageNumberLayout.Place(glyphs, 30, -2, null);
        Assert.AreEqual(28f, layout.Places[1].Left); // after the digit's step
        Assert.AreEqual(42.5f, layout.Places[2].Left); // the dot steps 16 - 2: pen 42, then 41 / 2 - 20
        Assert.AreEqual(-37f, layout.Places[2].Top);
        Assert.AreEqual(83f, layout.Width);
    }

    [TestMethod]
    public void CriticalMark_StandsInTheFirstCellAndChangesNothingElse()
    {
        var glyphs = new[] { Digit(40, 20, 45), Digit(30, 15, 37) };
        var plain = DamageNumberLayout.Place(glyphs, 30, 0, null);
        var crit = DamageNumberLayout.Place(glyphs, 30, 0, Digit(62, 41, 70));
        Assert.IsNull(plain.Star);
        Assert.AreEqual(-21f, crit.Star!.Value.Left); // 40 / 2 - 41
        Assert.AreEqual(-70f, crit.Star.Value.Top);
        CollectionAssert.AreEqual(plain.Places, crit.Places);
        Assert.AreEqual(plain.Width, crit.Width);
    }
}
