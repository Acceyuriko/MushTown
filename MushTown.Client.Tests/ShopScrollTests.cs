using Microsoft.Xna.Framework;
using MushTown.Client;
using MushTown.Shared;

namespace MushTown.Client.Tests;

// The shop keeps one scroll row per tab: Sell / Buy Back on the left and the
// six bag tabs on the right.
[TestClass]
public class ShopScrollTests
{
    [TestMethod]
    public void TwelveRows_ScrollToBottom_ShowsTheThirdRowFirst()
    {
        var scroll = new ShopScroll();
        Assert.AreEqual(2, scroll.LeftMax(12));
        scroll.WheelLeft(0, -1, 12);
        Assert.AreEqual(2, scroll.Left(0)); // zero-based row 2 is the third row
    }

    [TestMethod]
    public void Wheel_ThreeRows_ClampsAtTheBottom()
    {
        var scroll = new ShopScroll();
        scroll.WheelLeft(0, -1, 12);
        scroll.WheelLeft(0, -1, 12);
        Assert.AreEqual(2, scroll.Left(0));
    }

    [TestMethod]
    public void Wheel_TwentyRows_OneNotchMovesThreeRows()
    {
        var scroll = new ShopScroll();
        scroll.WheelLeft(0, -1, 20);
        Assert.AreEqual(3, scroll.Left(0));
    }

    [TestMethod]
    public void LeftTabs_KeepSeparateRows()
    {
        var scroll = new ShopScroll();
        scroll.SetLeft(0, 2);
        scroll.SetLeft(1, 0);
        Assert.AreEqual(2, scroll.Left(0));
        Assert.AreEqual(0, scroll.Left(1));
    }

    [TestMethod]
    public void BagTabs_KeepSeparateRows()
    {
        var scroll = new ShopScroll();
        scroll.SetInv(0, 2);
        scroll.SetInv(5, 1);
        Assert.AreEqual(2, scroll.Inv(0));
        Assert.AreEqual(1, scroll.Inv(5));
        Assert.AreEqual(0, scroll.Inv(1));
    }

    [TestMethod]
    public void RowScroll_TwelveRowsAtTwo_PutsRowTwoInSlotZero()
    {
        var scroll = new ShopScroll();
        scroll.SetLeft(0, 2);

        var rowTwo = new GameObject();
        rowTwo.Add(new Rect(new Rectangle(0, 141, 336, 45)));
        var two = new ShopRowScroll(2, 0, 141, 50, 10, () => scroll.Left(0));
        rowTwo.Add(two);
        rowTwo.NoHit = true;
        two.Apply();
        Assert.AreEqual(141, rowTwo.Get<Rect>()!.Offset.Y);
        Assert.IsFalse(rowTwo.NoHit);

        var rowZero = new GameObject();
        rowZero.Add(new Rect(new Rectangle(0, 141, 336, 45)));
        var zero = new ShopRowScroll(0, 0, 141, 50, 10, () => scroll.Left(0));
        rowZero.Add(zero);
        zero.Apply();
        Assert.AreEqual(-10000, rowZero.Get<Rect>()!.Offset.Y);
        Assert.IsTrue(rowZero.NoHit);
    }

    [TestMethod]
    public void SellingThreeRows_ClampsTheSavedRow()
    {
        var scroll = new ShopScroll();
        scroll.SetLeft(0, 2);
        scroll.ClampLeft(0, 9); // twelve rows minus the three sold
        Assert.AreEqual(0, scroll.Left(0));

        scroll.SetInv(1, 2);
        scroll.ClampInv(1, 7); // a ten-row bag minus the three sold
        Assert.AreEqual(0, scroll.Inv(1));
    }
}
