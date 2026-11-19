namespace MushTown.Client;

// A character of a damage number: a digit, the dot or the unit letter of a unit skin (K, M, B).
internal enum GlyphKind { Digit, Dot, Unit }

// One picture: its width and its WZ origin.
internal readonly record struct GlyphPic(GlyphKind Kind, int Width, int Ox, int Oy);

// Where a picture's top left corner goes: Left from the left end of the number, Top from its baseline.
internal readonly record struct GlyphPlace(float Left, float Top);

internal sealed record NumberLayout(GlyphPlace[] Places, float Width, GlyphPlace? Star);

// The layout of one damage number, apart from drawing. The characters are drawn from left to right, so a later one
// covers the right edge of the one before. Each picture stands at its pen by its origin (left = pen + width / 2 - Ox,
// top = baseline - Oy) and the pen moves on by a step:
//  - a digit steps (100 - OverlapPercent) percent of its own picture width;
//  - the dot steps its width plus dotSpace (a negative dotSpace overlaps the next character);
//  - the unit letter is last and does not step.
// Every skin uses the same OverlapPercent; the WZ numberSpace is not read.
// The critical mark is a second picture, put by its own origin on an anchor point of the first digit; it changes neither
// the steps nor the width. Two anchors, picked by where the mark's origin lies:
//  - origin inside the picture's width (1426 and the basic skin, 1606): the centre of the first cell on the baseline. This
//    matches the basic-skin reference picture (the burst's centre is at the top left of the first digit).
//  - origin right of the picture (1332, 1354: the picture hangs left of its origin): this anchor would put the mark wholly
//    left of the first digit, which the 1354 reference video does not show. There the anchor is the point a step in from the
//    top left of the first digit, 70 percent of its width across and 70 percent of its height down (the same share as the
//    step). Measured on that video (digit 5, 44 x 56 px, scale taken from the digit art): the mushroom's face centre gives
//    the mark's top left at (-79, -61) from the digit's top left, so the origin lies at (31, 39) = (0.70 x 44, 0.70 x 56).
//    This is one frame and no skin-wide rule: the basic reference does NOT fit it (there the anchor is the cell centre and
//    baseline), and an anchor at the digit's top left fits neither.
// The digits' own numbers were measured on the reference picture of the basic skin with units (34.9M, 38M, 34.3M, 35.2M):
// two digits overlap by 11 px on average (30 percent of 37), a dot and the next character by about 1 px; the numbers are
// the best fit against the reference rows, not a pixel-exact reading.
internal static class DamageNumberLayout
{
    public const int OverlapPercent = 30; // a digit's picture is overlapped by the next character by this share of its width

    public static NumberLayout Place(IReadOnlyList<GlyphPic> glyphs, int dotSpace, GlyphPic? star)
    {
        var places = new GlyphPlace[glyphs.Count];
        float pen = 0;
        for (int i = 0; i < glyphs.Count; i++)
        {
            var g = glyphs[i];
            places[i] = new GlyphPlace(pen + g.Width / 2f - g.Ox, -g.Oy);
            if (i < glyphs.Count - 1) pen += Step(g, dotSpace);
        }
        float width = pen + glyphs[^1].Width;
        GlyphPlace? starPlace = null;
        if (star is { } s)
        {
            var first = glyphs[0];
            float step = (100 - OverlapPercent) / 100f;
            bool hangs = s.Ox > s.Width; // the picture hangs left of its origin
            float anchorX = hangs ? places[0].Left + first.Width * step : first.Width / 2f;
            float anchorY = hangs ? -first.Oy * (1 - step) : 0; // a digit stands on the baseline: Oy is its height
            starPlace = new GlyphPlace(anchorX - s.Ox, anchorY - s.Oy);
        }
        return new NumberLayout(places, width, starPlace);
    }

    private static float Step(GlyphPic g, int dotSpace) => g.Kind switch
    {
        GlyphKind.Digit => g.Width * (100 - OverlapPercent) / 100f,
        GlyphKind.Dot => g.Width + dotSpace,
        _ => 0,
    };
}
