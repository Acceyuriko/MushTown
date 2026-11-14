using MushTown.Shared;

namespace MushTown.Tests;

// The real demo items and the window box each one fills. Tests use this one
// table so every extended slot is exercised, including the icon-only slots.
internal static class DemoSlots
{
    public static readonly (int Id, string Slot, string Box)[] All =
    {
        (ItemData.DemoHat, "Cp", "hat"),
        (ItemData.DemoGloves, "Gv", "gloves"),
        (ItemData.DemoCape, "Sr", "cape"),
        (ItemData.DemoShield, "Si", "secondary"),
        (ItemData.DemoFace, "Af", "face_acc"),
        (ItemData.DemoEye, "Ay", "eye_acc"),
        (ItemData.DemoEarring, "Ae", "earrings"),
        (ItemData.DemoPendant, "Pe", "pendant1"),
        (ItemData.DemoBelt, "Be", "belt"),
        (ItemData.DemoMedal, "Me", "medal"),
        (ItemData.DemoPocket, "Po", "pocket"),
        (ItemData.DemoBadge, "Ba", "badge"),
        (ItemData.DemoShoulder, "Sh", "shoulder"),
    };
}
