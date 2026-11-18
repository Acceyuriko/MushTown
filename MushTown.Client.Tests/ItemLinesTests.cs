using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The detail lines of an item: the stat window's detail box and the item
// tooltip both show them, so they come from one place.
[TestClass]
public class ItemLinesTests
{
    [TestMethod]
    public void Detail_ReadsTheRequirementThenTheBonuses()
    {
        var item = new ItemData { ReqLevel = 10, Atk = 5, Def = 2, Str = 1, Dex = 3 };
        var lines = ItemLines.Detail(item);
        CollectionAssert.AreEqual(
            new[] { "Required Level Lv. 10", "Attack +5", "Defense +2", "STR +1", "DEX +3" },
            lines.ToArray());
    }

    [TestMethod]
    public void Detail_ShowsTheJobAndTheMagicStats()
    {
        var wand = new ItemData { ReqLevel = 140, ReqJob = 2, Atk = 93, Mad = 153, Int = 16, Luk = 15, MaxHp = 150, MaxMp = 60, Mdd = 70, Acc = 100, Eva = 50, Speed = 5, Jump = 4 };
        CollectionAssert.AreEqual(
            new[]
            {
                "Required Level Lv. 140", "Job: Magician", "Attack +93", "Magic Attack +153", "Magic Defense +70",
                "INT +16", "LUK +15", "Max HP +150", "Max MP +60", "Accuracy +100", "Avoidability +50", "Speed +5", "Jump +4",
            },
            ItemLines.Detail(wand).ToArray());
    }

    [TestMethod]
    public void Detail_LeavesOutWhatTheItemDoesNotGive()
    {
        var potion = new ItemData { Hp = 50, Mp = 0 }; // a potion's own description says what it restores
        Assert.AreEqual(0, ItemLines.Detail(potion).Count);
    }
}
