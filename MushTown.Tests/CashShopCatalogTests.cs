using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public class CashShopCatalogTests
{
    [TestMethod]
    public void TabIndex_UsesTheOfficialSubTabOrder()
    {
        Assert.AreEqual(2, CashShopCatalog.TabIndex("Utility", "Convenience"));
        Assert.AreEqual(0, CashShopCatalog.TabIndex("Fashion", "Weapons"));
        Assert.AreEqual(2, CashShopCatalog.TabIndex("Fashion", "Outfits"));
        Assert.AreEqual(1, CashShopCatalog.TabIndex("Fashion", "Hats"));
        Assert.AreEqual(-1, CashShopCatalog.TabIndex("Fashion", "Unknown"));
    }

    [TestMethod]
    public void Validate_RejectsBadPriceCountGroupAndTab()
    {
        var items = new Dictionary<int, ItemData> { [1] = new ItemData { ItemId = 1, Name = "x" } };
        var catalog = new CashShopCatalog
        {
            Items =
            {
                new CashShopItem { Id = 1, Price = 1, Count = 1, Group = "Utility", Tab = "Convenience" },
            },
        };
        Assert.IsTrue(catalog.Validate(items, out _));

        catalog.Items[0].Price = 0;
        Assert.IsFalse(catalog.Validate(items, out _));
        catalog.Items[0].Price = 1;
        catalog.Items[0].Count = 0;
        Assert.IsFalse(catalog.Validate(items, out _));
        catalog.Items[0].Count = 1;
        catalog.Items[0].Group = "Beauty";
        Assert.IsFalse(catalog.Validate(items, out _));
        catalog.Items[0].Group = "Utility";
        catalog.Items[0].Tab = "Unknown";
        Assert.IsFalse(catalog.Validate(items, out _));
    }

    [TestMethod]
    public void Validate_RejectsMissingItemData()
    {
        var catalog = new CashShopCatalog
        {
            Items =
            {
                new CashShopItem { Id = 7, Price = 1, Count = 1, Group = "Fashion", Tab = "Weapons" },
            },
        };
        Assert.IsFalse(catalog.Validate(new Dictionary<int, ItemData>(), out string error));
        StringAssert.Contains(error, "7");
    }
}
