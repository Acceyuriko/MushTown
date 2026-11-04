using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MushTown.Shared;

namespace MushTown.Client;

// Character names, one draw for the select screen and the live game: a
// see-through black plate, or the worn ring's plate (the ring def names its NameTag node).
public partial class MushTownGame
{
    private readonly Dictionary<int, NameTagData> _tags = new(); // plate node -> slices + ink
    private readonly Dictionary<int, NameEvent> _names = new(); // peer id -> name + plate
    private const int NameDown = 6; // live-game tag gap below the feet (the select screen tunes its own)
    private static readonly Color NamePlate = new(0, 0, 0, 120); // the see-through plate every plain tag wears

    // Plates ride the defs: every ring's NameTag node loads its slices here,
    // so exporter and client stay in sync with no shared list.
    private void LoadNameTags()
    {
        foreach (int node in _items.Values.Where(d => d.NameTag != 0).Select(d => d.NameTag).Distinct())
        {
            _tags[node] = LoadJson<NameTagData>(Path.Combine(_assetsDir, "ui", "tag", $"ring{node}.json"));
            LoadTexture($"ui/tag/ring{node}_w.png");
            LoadTexture($"ui/tag/ring{node}_c.png");
            LoadTexture($"ui/tag/ring{node}_e.png");
        }
    }

    // The worn ring's plate node (0 = the plain see-through black plate). First ring wins.
    private int PlateFor(List<int>? worn)
    {
        if (worn == null) return 0;
        foreach (int id in worn)
            if (_items.TryGetValue(id, out var d) && d.NameTag != 0)
                return d.NameTag;
        return 0;
    }

    // One name under a character: centered on cx, hanging below topY (the box
    // is 16 tall, plates hang by their origins). Text centers the bar.
    private void DrawName(int cx, int topY, string name, int plate)
    {
        var tex = _ui.GetSmall(name);
        if (plate == 0 || !_tags.TryGetValue(plate, out var t)) // unknown plates (a foreign client) fall back to plain
        {
            DrawTag(new Rectangle(cx - tex.Width / 2 - 3, topY, tex.Width + 6, 16), tex);
            return;
        }
        var w = _textures[$"ui/tag/ring{plate}_w.png"];
        var c = _textures[$"ui/tag/ring{plate}_c.png"];
        var e = _textures[$"ui/tag/ring{plate}_e.png"];
        int span = tex.Width + 12;
        int x0 = cx - span / 2;
        _spriteBatch.Draw(w, new Vector2(x0 - t.WOx, topY - t.WOy), Color.White);
        int run = span + t.COx;
        for (int dx = 0; dx < run; dx += c.Width)
        {
            int take = Math.Min(c.Width, run - dx);
            _spriteBatch.Draw(c, new Vector2(x0 - t.COx + dx, topY - t.COy),
                new Rectangle(0, 0, take, c.Height), Color.White);
        }
        _spriteBatch.Draw(e, new Vector2(x0 + span - t.EOx, topY - t.EOy), Color.White);
        _spriteBatch.Draw(tex, new Vector2(x0 + 6, topY - t.COy + (c.Height - tex.Height) / 2),
            new Color((t.Clr >> 16) & 255, (t.Clr >> 8) & 255, t.Clr & 255));
    }

    // The plain tag: the see-through plate and the name centered in it. The
    // NPCs of a map wear the same one, so a player without a ring plate does
    // not look different from them.
    private void DrawTag(Rectangle box, Texture2D tex)
    {
        _spriteBatch.Draw(_pixel, box, NamePlate);
        Text.Draw(_spriteBatch, _ui, tex, box, Color.White, TextLine.Middle, TextAlign.Center);
    }
}
