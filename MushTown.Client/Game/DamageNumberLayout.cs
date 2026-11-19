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
//  - a digit steps (100 - numberOverlap) percent of its own picture width (30 in the basic skin, the WZ numberSpace of
//    skin 1426 NoCustom);
//  - the dot steps its width plus dotSpace (a negative dotSpace overlaps the next character);
//  - the unit letter is last and does not step.
// The critical mark is a second picture in the cell of the first character, put there by its own origin; it changes
// neither the steps nor the width. Measured on the reference picture of the basic skin with units (34.9M, 38M, 34.3M,
// 35.2M): two digits overlap by 11 px on average (30 percent of 37), a dot and the next character by about 1 px; the
// numbers are the best fit against the reference rows, not a pixel-exact reading.
internal static class DamageNumberLayout
{
    public static NumberLayout Place(IReadOnlyList<GlyphPic> glyphs, int numberOverlap, int dotSpace, GlyphPic? star)
    {
        var places = new GlyphPlace[glyphs.Count];
        float pen = 0;
        for (int i = 0; i < glyphs.Count; i++)
        {
            var g = glyphs[i];
            places[i] = new GlyphPlace(pen + g.Width / 2f - g.Ox, -g.Oy);
            if (i < glyphs.Count - 1) pen += Step(g, numberOverlap, dotSpace);
        }
        float width = pen + glyphs[^1].Width;
        GlyphPlace? starPlace = star is { } s ? new GlyphPlace(glyphs[0].Width / 2f - s.Ox, -s.Oy) : null;
        return new NumberLayout(places, width, starPlace);
    }

    private static float Step(GlyphPic g, int numberOverlap, int dotSpace) => g.Kind switch
    {
        GlyphKind.Digit => g.Width * (100 - numberOverlap) / 100f,
        GlyphKind.Dot => g.Width + dotSpace,
        _ => 0,
    };
}
