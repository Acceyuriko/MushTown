namespace MushTown.Shared;

// The doll parts of one frame: the body bake, the worn gear, and the current
// hair and face. The body bake carries the default hair and face; an exported
// hair/face part replaces those baked parts. One order for the game and tests.
public static class AvatarMerge
{
    public static List<CharaPart> Parts(IReadOnlyList<CharaPart> body, IEnumerable<CharaPart> gear,
        IReadOnlyList<CharaPart>? hair, IReadOnlyList<CharaPart>? face,
        string defaultHairImg, string defaultFaceImg)
    {
        var parts = new List<CharaPart>(body);
        if (hair != null) parts.RemoveAll(p => p.Img == defaultHairImg);
        if (face != null) parts.RemoveAll(p => p.Img == defaultFaceImg);
        parts.AddRange(gear);
        if (hair != null) parts.AddRange(hair);
        if (face != null) parts.AddRange(face);
        return parts.OrderByDescending(p => p.Z).ToList();
    }
}
