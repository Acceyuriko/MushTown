using MushTown.Client;

namespace MushTown.Client.Tests;

// One keeper's buy-back rows: the same id sold from two bags stays two rows.
[TestClass]
public class ShopBuybackTests
{
    [TestMethod]
    public void Add_SameIdAndTab_MergesIntoOneRow()
    {
        var list = new ShopBuybackList();
        list.Add(100, 2, "cash");
        list.Add(100, 3, "cash");
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(5, list.Rows[0].count);
    }

    [TestMethod]
    public void Add_SameIdDifferentTabs_KeepsTwoRows()
    {
        var list = new ShopBuybackList();
        list.Add(100, 2, "cash");
        list.Add(100, 1, "dec");
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("cash", list.Rows[0].tab);
        Assert.AreEqual("dec", list.Rows[1].tab);
    }

    [TestMethod]
    public void Take_OnlyDecrementsItsOwnTab()
    {
        var list = new ShopBuybackList();
        list.Add(100, 2, "cash");
        list.Add(100, 1, "dec");
        list.Take(100, "cash");
        Assert.AreEqual(1, list.Rows[0].count);
        Assert.AreEqual(1, list.Rows[1].count);
    }

    [TestMethod]
    public void Take_LastCopy_RemovesTheRow()
    {
        var list = new ShopBuybackList();
        list.Add(100, 1, "cash");
        list.Take(100, "cash");
        Assert.AreEqual(0, list.Count);
    }
}
