namespace MushTown.Shared;

// The doll parts of one frame: the body bake, the worn gear, and the current
// hair and face. The body bake carries the default hair and face; an exported
// hair/face part replaces those baked parts. A hat swaps the hair-over-head
// parts for the hair-under-cap parts. One order for the game and tests.
public static class AvatarMerge
{
    public static List<CharaPart> Parts(IReadOnlyList<CharaPart> body, IEnumerable<CharaPart> gear,
        IReadOnlyList<CharaPart>? hair, IReadOnlyList<CharaPart>? face,
        string defaultHairImg, string defaultFaceImg, bool hairCover = false)
    {
        var parts = new List<CharaPart>(body);
        if (hair != null) parts.RemoveAll(p => p.Img == defaultHairImg);
        if (face != null) parts.RemoveAll(p => p.Img == defaultFaceImg);
        parts.AddRange(gear);
        if (hair != null) parts.AddRange(hair);
        if (face != null) parts.AddRange(face);
        parts.RemoveAll(hairCover ? IsHairOverHead : IsHairUnderCap);
        return parts.OrderByDescending(p => p.Z).ToList();
    }

    private static bool IsHairOverHead(CharaPart part) =>
        part.Image.EndsWith("_hairOverHead.png", StringComparison.Ordinal) ||
        part.Image.EndsWith("_backHairOverCape.png", StringComparison.Ordinal) ||
        part.Image.EndsWith("_backHair.png", StringComparison.Ordinal);

    private static bool IsHairUnderCap(CharaPart part) =>
        part.Image.EndsWith("_hair.png", StringComparison.Ordinal) ||
        part.Image.EndsWith("_backHairBelowCap.png", StringComparison.Ordinal);
}
