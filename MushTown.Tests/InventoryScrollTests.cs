using MushTown.Shared;

namespace MushTown.Tests;

[TestClass]
public sealed class InventoryScrollTests
{
    [TestMethod]
    public void UsedRows_StopsAtTheLastFilledCell()
    {
        var cells = new int[Inventory.Capacity];
        for (int i = 0; i < 40; i++) cells[i] = 1;

        Assert.AreEqual(10, InventoryScroll.UsedRows(4, i => cells[i]));
        Assert.AreEqual(2, InventoryScroll.MaxRow(10, 8));
        Assert.AreEqual(0, InventoryScroll.UsedRows(4, _ => 0));
    }

    [TestMethod]
    public void FullBag_StopsAtRow24()
    {
        Assert.AreEqual(32, InventoryScroll.UsedRows(4, _ => 1));
        Assert.AreEqual(24, InventoryScroll.MaxRow(32, 8));
    }

    [TestMethod]
    public void Wheel_MovesThreeRowsAndClamps()
    {
        Assert.AreEqual(2, InventoryScroll.WheelRow(0, -1, 2));
        Assert.AreEqual(0, InventoryScroll.WheelRow(0, 1, 2));
        Assert.AreEqual(0, InventoryScroll.WheelRow(0, -1, 0));
    }

    [TestMethod]
    public void CellIndex_UsesTheScrolledRow()
    {
        Assert.AreEqual(8, InventoryScroll.CellIndex(2, 4, 0));
        Assert.AreEqual(9, InventoryScroll.CellIndex(2, 4, 1));
        Assert.AreEqual(96, InventoryScroll.CellIndex(24, 4, 0));
    }
}
