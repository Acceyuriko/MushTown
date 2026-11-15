using MushTown.Shared;

namespace MushTown.Client;

// The shop's two lists keep one row per tab: Sell / Buy Back on the left,
// the six bag tabs on the right. The row math is the same as the inventory's.
public sealed class ShopScroll
{
    public const int VisibleRows = 10;

    private readonly int[] _left = new int[2];
    private readonly int[] _inv = new int[6];

    public int Left(int tab) => _left[tab];
    public int Inv(int tab) => _inv[tab];
    public void SetLeft(int tab, int row) => _left[tab] = row;
    public void SetInv(int tab, int row) => _inv[tab] = row;
    public void Reset()
    {
        Array.Clear(_left);
        Array.Clear(_inv);
    }

    public int LeftMax(int rows) => InventoryScroll.MaxRow(rows, VisibleRows);
    public int InvMax(int rows) => InventoryScroll.MaxRow(rows, VisibleRows);

    public void ClampLeft(int tab, int rows) => _left[tab] = Math.Clamp(_left[tab], 0, LeftMax(rows));
    public void ClampInv(int tab, int rows) => _inv[tab] = Math.Clamp(_inv[tab], 0, InvMax(rows));

    public void WheelLeft(int tab, int notches, int rows) =>
        _left[tab] = InventoryScroll.WheelRow(_left[tab], notches, LeftMax(rows));

    public void WheelInv(int tab, int notches, int rows) =>
        _inv[tab] = InventoryScroll.WheelRow(_inv[tab], notches, InvMax(rows));
}
