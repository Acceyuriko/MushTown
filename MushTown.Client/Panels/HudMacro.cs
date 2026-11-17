using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The macro list window (UIWindow2.img/Skill/macro): five rows, each with three skill slots and
// the official icon of its macro. The window docks beside the skill window when it opens. A click
// on an active skill in hand puts it in a slot; a click on a slot with a skill takes it up; a click
// on a macro icon takes the macro up (to a key or a quick slot). The name box and the check box that
// are part of the official art do nothing yet. Every number lives in layouts/panel-macro.json.
public sealed class MacroWindow : UiWindow
{
    private readonly MushTownGame _game;

    public MacroWindow(MushTownGame game) : base(HotkeyAction.MacroList) { _game = game; }

    private static readonly PanelLayout _lay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-macro.json"));

    public override Rectangle Rect() => new(0, 0, _lay.Get("w", 184), _lay.Get("h", 361));
    public override Rectangle TitleRect() => new(0, 0, Rect().Width, _lay.Get("titleH", 20));

    // Beside the skill window, wherever that is now.
    public override Point LayoutPos()
    {
        var spot = _game.SkillSpot + new Point(_lay.Get("dockX", 320), _lay.Get("dockY", 0));
        return new Point(Math.Clamp(spot.X, 0, 1366 - Rect().Width), Math.Clamp(spot.Y, 0, 768 - Rect().Height)); // a skill window at the right edge must not push this one off the screen
    }
    public override void OnShown() => Pos = LayoutPos();

    private Rectangle SlotRect(int macro, int slot) =>
        new(_lay.Get("slotX", 15) + slot * _lay.Get("slotStep", 34), RowY(macro), _lay.Get("slotW", 32), _lay.Get("slotH", 32));

    private Rectangle IconRect(int macro) => new(_lay.Get("iconX", 134), RowY(macro), _lay.Get("iconW", 32), _lay.Get("iconH", 32));

    private int RowY(int macro) => _lay.Get("rowY", 40) + macro * _lay.Get("rowPitch", 44);

    // A drop with a skill in hand: the slot under the point takes it. False when the point is not
    // on a slot (or another window covers it), so the caller puts the piece back.
    internal bool TryDropAt(int gx, int gy, BindTarget target)
    {
        if (!Show || target.Kind != BindKind.Skill || _game.Hero == null) return false;
        if (MushTownGame.RootOf(Scene.TopAt(gx, gy)) != Host) return false;
        for (int m = 0; m < MacroBook.Count; m++)
            for (int s = 0; s < MacroBook.Slots; s++)
            {
                if (!OnScreen(SlotRect(m, s)).Contains(gx, gy)) continue;
                _game.DropMacroSkill(m, s, target.Id);
                return true;
            }
        return false;
    }

    public override void Build()
    {
        foreach (string art in new[] { "macro_bg", "macro_panel", "macro_list", "hud_statx" })
            _game.LoadTexture($"ui/{art}.png");
        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect())); // the window box: the children ride it
        root.Add(new Picture("ui/macro_bg.png"));
        root.Add(new Picture("ui/macro_panel.png", _lay.Get("panelX", 5), _lay.Get("panelY", 22)));
        root.Add(new Picture("ui/macro_list.png", _lay.Get("listX", 11), _lay.Get("listY", 33)));
        for (int m = 0; m < MacroBook.Count; m++)
        {
            int macro = m; // its own copy: the closures outlive the loop
            for (int s = 0; s < MacroBook.Slots; s++)
            {
                int slot = s;
                var cell = new GameObject();
                cell.Add(new Rect(SlotRect(macro, slot)));
                cell.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => _game.PickMacroSlot(macro, slot) }); // the drop is taken before the click chain; the pick plays the click
                cell.Add(new Grab(() => _game.MacroSkill(macro, slot) != 0));
                cell.Add(new MacroArt(() => _game.MacroSlotIcon(macro, slot)));
                root.AddChild(cell);
            }
            var icon = new GameObject();
            icon.Add(new Rect(IconRect(macro)));
            icon.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => _game.PickMacro(macro) });
            icon.Add(new Grab(() => !_game.MacroEmpty(macro)));
            icon.Add(new MacroArt(() => $"ui/macro_icon{macro}.png"));
            root.AddChild(icon);
        }
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        var x = new GameObject();
        x.Add(new Rect(new Rectangle(Rect().Width - _lay.Get("closeX", 15) - _lay.Get("closeW", 11), _lay.Get("closeY", 2), _lay.Get("closeW", 11), _lay.Get("closeH", 11))));
        x.Add(new Button { Kind = ButtonKind.Click, OnClick = () => Show = false });
        x.Add(new Picture("ui/hud_statx.png")); // the macro window has no X of its own in the WZ
        root.AddChild(x);
        Host.AddChild(root);
    }
}

// A picture centered in its box (a skill icon in a slot, the official macro icon).
public sealed class MacroArt : Component, IDrawable
{
    private readonly Func<string> _art;
    public MacroArt(Func<string> art) => _art = art;
    public void Draw(UiGfx gfx)
    {
        string name = _art();
        if (name == "") return;
        var r = Owner.Get<Rect>()!.Screen;
        var tex = gfx.Tex(name);
        gfx.Sb.Draw(tex, new Vector2(r.X + (r.Width - tex.Width) / 2, r.Y + (r.Height - tex.Height) / 2), Color.White);
    }
}
