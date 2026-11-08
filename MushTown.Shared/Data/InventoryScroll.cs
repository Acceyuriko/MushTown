namespace MushTown.Shared;

// The row math of the inventory grid. The window keeps one scroll row per tab;
// these helpers turn the filled cells into a legal row and back.
public static class InventoryScroll
{
    // How many rows the tab uses, or 0 for an empty tab.
    public static int UsedRows(int cols, Func<int, int> slotId)
    {
        for (int i = Inventory.Capacity - 1; i >= 0; i--)
            if (slotId(i) != 0) return i / cols + 1;
        return 0;
    }

    // The last row the small window may show first.
    public static int MaxRow(int usedRows, int rows) => Math.Max(0, usedRows - rows);

    // One wheel notch is three rows; the result stays inside 0..max.
    public static int WheelRow(int row, int notches, int max) =>
        Math.Clamp(row - notches * 3, 0, max);

    // The real bag cell under one visible screen cell.
    public static int CellIndex(int row, int cols, int screenCell) => row * cols + screenCell;
}
