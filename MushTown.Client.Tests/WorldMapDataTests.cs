using System.Text.Json;
using MushTown.Client;

namespace MushTown.Client.Tests;

// The exported world map file (assets/ui/worldmap.json): the worlds of the first combo and the
// Arcane River pages. Like the quest chain sim, this reads the exported assets (run the export first).
[TestClass]
public class WorldMapDataTests
{
    private static WorldMapData Load()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string path = Path.Combine(root, "assets", "ui", "worldmap.json");
        Assert.IsTrue(File.Exists(path), $"needs exported assets (run: dotnet run --project MushTown.Exporter -- export): {path}");
        return JsonSerializer.Deserialize<WorldMapData>(File.ReadAllText(path))!;
    }

    // The first combo lists only the worlds we have a map in, by the official names and in the official order.
    [TestMethod]
    public void Worlds_AreMapleWorldAndArcaneRiver()
    {
        var worlds = Load().Worlds;
        CollectionAssert.AreEqual(new[] { "Maple World", "Arcane River" }, worlds.Select(w => w.Text).ToArray());
        CollectionAssert.AreEqual(new[] { "WorldMap", "WorldMap082" }, worlds.Select(w => w.Page).ToArray());
    }

    // Both world roots have no parent; every other page hangs off one of them.
    [TestMethod]
    public void Roots_HaveNoParent()
    {
        var data = Load();
        var byName = data.Maps.ToDictionary(m => m.Name);
        foreach (var world in data.Worlds) Assert.AreEqual("", byName[world.Page].Parent);
        Assert.AreEqual(2, data.Maps.Count(m => m.Parent == ""));
    }

    // Arcane River lists Vanishing Journey, and that page holds our two maps as the only nodes that open.
    [TestMethod]
    public void ArcaneRiver_VanishingJourneyHoldsOurTwoMaps()
    {
        var data = Load();
        var byName = data.Maps.ToDictionary(m => m.Name);
        var link = byName["WorldMap082"].Links.Single(l => l.ToolTip == "Vanishing Journey");
        Assert.AreEqual("WorldMap0821", link.Target);
        var page = byName["WorldMap0821"];
        Assert.AreEqual("WorldMap082", page.Parent);
        var ours = page.Nodes.Where(n => n.Ours).ToList();
        CollectionAssert.AreEqual(new[] { "Nameless Town", "Weathered Land of Happiness" }, ours.Select(n => n.Name).ToArray());
        CollectionAssert.AreEqual(new[] { 450001000, 450001010 }, ours.Select(n => n.Maps.Single()).ToArray());
    }

    // The Maple World pages are the ones the file carried before the second world came.
    [TestMethod]
    public void MapleWorld_KeepsItsPages()
    {
        var names = Load().Maps.Select(m => m.Name).ToHashSet();
        foreach (string page in new[] { "WorldMap", "WorldMap000", "WorldMap010", "WorldMap020", "WorldMap030", "WorldMap040",
            "WorldMap050", "WorldMap060", "WorldMap070", "WorldMap080", "WorldMap090", "WorldMap100", "WorldMap110",
            "WorldMap170", "WorldMap160", "WorldMap178" })
            Assert.IsTrue(names.Contains(page), page);
    }
}
