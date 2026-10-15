namespace MushTown.Shared;

// Export manifest (assets/manifest.json): the ship list for scan-loaded defs.
// Boot proves every listed id has a def; hand-added defs beyond the list still load.
public sealed class ManifestData
{
    public List<int> Potions { get; set; } = new();
    public List<int> Equips { get; set; } = new();
    public List<int> Skills { get; set; } = new();
}
