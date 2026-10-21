using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// The hotkey bar (HUD, bottom right): one cell per game action. The cell
// shows the official key cap of the key the action has now and the name of
// the action. Clicking a cell opens the feature. The bar sits above the
// channel bar; every number lives in layouts/panel-hotkey.json.
public static class HotkeyBar
{
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-hotkey.json"));

    // The actions the game has: the action, the feature it opens and the
    // name under the key cap.
    private static readonly (HotkeyAction Action, Feature Feature, string Name)[] Cells =
    {
        (HotkeyAction.Inventory, Feature.Inventory, "Items"),
        (HotkeyAction.Stat, Feature.Stat, "Stat"),
        (HotkeyAction.Skill, Feature.Skill, "Skill"),
        (HotkeyAction.Quest, Feature.Quest, "Quest"),
        (HotkeyAction.WorldMap, Feature.WorldMap, "Map"),
        (HotkeyAction.Settings, Feature.Settings, "Setup"),
    };

    // The bar tree: the game adds it to the HUD scene (HudBar.cs).
    public static GameObject Build(MushTownGame game)
    {
        game.LoadTexture("ui/hotkey_cells.png");
        KeyConfigWindow.LoadCaps(game); // the key caps of the bound keys
        var root = new GameObject();
        for (int i = 0; i < Cells.Length; i++)
        {
            var c = Cells[i];
            var o = new GameObject();
            o.Add(new Rect(CellRect(i)));
            o.Add(new HotkeyCell(() => game.HotkeyKey(c.Action), c.Name));
            o.Add(new Button { Kind = ButtonKind.Click, HoverFill = true, OnClick = () => game.OpenFeature(c.Feature) });
            root.AddChild(o);
        }
        return root;
    }

    // One cell box.
    private static Rectangle CellRect(int i) => new(
        _lay.Get("x", 1156) + i * _lay.Get("pitch", 35),
        _lay.Get("y", 581),
        _lay.Get("cellW", 35), _lay.Get("cellH", 33));
}

// One hotkey cell: the official cell frame, the key cap of the key the
// action has now and the name of the action. The cap is read every draw,
// so a new binding shows up right away.
public sealed class HotkeyCell : Component, IDrawable
{
    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-hotkey.json"));

    private readonly Func<string> _key;
    private readonly string _name;
    public HotkeyCell(Func<string> key, string name) { _key = key; _name = name; }

    public void Draw(UiGfx gfx)
    {
        var r = Owner.Get<Rect>()!.Box;
        // the cell frame: the first cell of the official quick slot strip
        gfx.Sb.Draw(gfx.Tex("ui/hotkey_cells.png"), r, new Rectangle(0, 0, r.Width, r.Height), Color.White);
        // the key cap of the key the player bound to the action
        string cap = KeyConfigWindow.CapArt(_key());
        if (cap != "")
        {
            var tex = gfx.Tex(cap);
            gfx.Sb.Draw(tex, new Vector2(r.X + _lay.Get("capDx", 2), r.Y + _lay.Get("capDy", 2)), Color.White);
        }
        // the name of the action under the cap
        var t = gfx.Ui.GetSmall(_name);
        gfx.Sb.Draw(t, new Vector2(r.X + (r.Width - t.Width) / 2, r.Y + _lay.Get("nameDy", 18)), Color.White);
    }
}
