using Microsoft.Xna.Framework;
using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// The party HP panel (UIParty.img/partyHP): one row per party member with the
// member mark, the name and a red HP bar. It is not the bar over a head; that
// one is drawn in the world (GameRender). The number of rows follows the party.
public sealed class PartyHpWindow : UiWindow
{
    private readonly MushTownGame _game;
    private static readonly PanelLayout _lay = PartyWindow._lay;

    public PartyHpWindow(MushTownGame game) : base(HotkeyAction.PartyHp) { _game = game; }

    private int Rows => Math.Min(PartyWorld.MaxMembers, _game.PartyMembers.Length);
    private int Height => _lay.Get("hpTop", 7) * 2 + Rows * _lay.Get("hpRowH", 18);

    public override Rectangle Rect() => new(0, 0, _lay.Get("hpW", 200), Height);
    public override Rectangle TitleRect() => Rect(); // no title bar: the whole panel drags
    public override Point LayoutPos() => new(_lay.Get("hpX", 20), _lay.Get("hpY", 280));
    public override Point ClampPos(Point p) =>
        new(Math.Clamp(p.X, 0, MushTownGame.GameWidth - Rect().Width),
            Math.Clamp(p.Y, 0, MushTownGame.GameHeight - Rect().Bottom));

    public override void Build()
    {
        foreach (string f in new[]
        {
            "party_hp_nw.png", "party_hp_ne.png", "party_hp_sw.png", "party_hp_se.png",
            "party_hp_n.png", "party_hp_s.png", "party_hp_w.png", "party_hp_e.png", "party_hp_c.png",
            "party_hp_bar.png", "party_hp_graduation.png", "party_hp_gauge.png",
            "party_hp_leader0.png", "party_hp_leader1.png",
        })
            _game.LoadTexture("ui/" + f);

        Host.Clear();
        var root = new GameObject();
        root.Add(new Rect(Rect()));
        root.Add(new Box("party_hp"));
        root.Add(new PartyHpRows(_game, Rows));
        root.AddChild(MapWindowParts.TitleDrag(this, TitleRect()));
        Host.AddChild(root);
    }

    // Esc closes the panel: the switch in the party window goes back to off.
    public override void OnClosed() => _game.SetPartyHpVisible(false);

    // The rows read the member list live; only the row count changes a build.
    private sealed class PartyHpRows : Component, IDrawable
    {
        private readonly MushTownGame _game;
        private readonly int _count;
        public PartyHpRows(MushTownGame game, int count) { _game = game; _count = count; }

        public void Draw(UiGfx gfx)
        {
            var r = Owner.Get<Rect>()!.Screen;
            var lay = PartyHpWindow._lay;
            var bar = gfx.Tex("ui/party_hp_bar.png");
            var graduation = gfx.Tex("ui/party_hp_graduation.png");
            var gauge = gfx.Tex("ui/party_hp_gauge.png");
            for (int i = 0; i < _count; i++)
            {
                var m = _game.PartyMembers[i];
                int rowY = r.Y + lay.Get("hpTop", 7) + i * lay.Get("hpRowH", 18);
                var icon = gfx.Tex(i == 0 ? "ui/party_hp_leader0.png" : "ui/party_hp_leader1.png");
                gfx.Sb.Draw(icon, new Vector2(r.X + lay.Get("hpIconX", 8), rowY + (lay.Get("hpRowH", 18) - icon.Height) / 2), Color.White);

                var nameBox = new Rectangle(r.X + lay.Get("hpNameX", 24), rowY, lay.Get("hpNameW", 86), lay.Get("hpRowH", 18));
                Text.Draw(gfx.Sb, gfx.Ui, Text.Fit(gfx, m.Name, nameBox.Width), nameBox, new Color(51, 51, 51));

                PartyGauge.Draw(gfx.Sb, graduation, bar, gauge,
                    r.X + lay.Get("hpBarX", 118), rowY + lay.Get("hpBarDy", 4), m.HpPct);
            }
        }
    }
}
