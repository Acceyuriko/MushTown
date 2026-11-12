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

    [TestMethod]
    public void Validate_RequiresOneFreeDefaultPerLookList()
    {
        var items = new Dictionary<int, ItemData>
        {
            [1] = new ItemData { ItemId = 1, Name = "hair" },
            [2] = new ItemData { ItemId = 2, Name = "face" },
            [3] = new ItemData { ItemId = 3, Name = "spare hair" },
            [4] = new ItemData { ItemId = 4, Name = "extra hair" },
        };
        var catalog = new CashShopCatalog
        {
            Hairs =
            {
                new CashShopLook { ItemId = 1, Cosmetic = 30020, Name = "Messy Hair", Price = 0, Default = true },
                new CashShopLook { ItemId = 2, Cosmetic = 65370, Name = "Dark Lyrk Hair", Price = 1800 },
                new CashShopLook { ItemId = 3, Cosmetic = 33390, Name = "Messy Hair", Price = 0 },
            },
            Faces =
            {
                new CashShopLook { ItemId = 2, Cosmetic = 20000, Name = "Defiant Face", Price = 0, Default = true },
            },
        };
        Assert.IsTrue(catalog.Validate(items, out string error), error);

        catalog.Hairs.Add(new CashShopLook { ItemId = 4, Cosmetic = 36680, Name = "Spade Hair", Price = 0 });
        Assert.IsFalse(catalog.Validate(items, out _));
        catalog.Hairs.RemoveAt(3);

        catalog.Hairs[0].Default = false;
        Assert.IsFalse(catalog.Validate(items, out _));
        catalog.Hairs[0].Default = true;
        catalog.Hairs[0].Price = 1;
        Assert.IsFalse(catalog.Validate(items, out _));
    }

    [TestMethod]
    public void ShippedCashShopList_HasNineSellableHairs()
    {
        string? path = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "MushTown.Exporter", "Export", "lists", "cashshop.json")))
            {
                path = Path.Combine(d.FullName, "MushTown.Exporter", "Export", "lists", "cashshop.json");
                break;
            }
        if (path == null) { Assert.Inconclusive("no cashshop list above the test output: this check needs the repo"); return; }

        var catalog = System.Text.Json.JsonSerializer.Deserialize<CashShopCatalog>(File.ReadAllText(path))!;
        Assert.AreEqual(1, catalog.Hairs.Count(hair => hair.Default));
        Assert.AreEqual(9, catalog.Hairs.Count(hair => !hair.Default && hair.Price > 0));
        Assert.IsTrue(catalog.ValidateShape(out string error), error);
    }
}
