using MushTown.Server;
using MushTown.Shared;

namespace MushTown.Client;

// Drops: ground loot views, spawn/take application.
public partial class MushTownGame
{
    private void ClearDrops()
    {
        foreach (var d in _drops.Values) _world.RemoveChild(d.Owner);
        _drops.Clear();
    }

    // One ground drop: the sprite sits on its feet (coin or potion icon).
    private void DrawDrop(DropArt d)
    {
        string image = d.Kind == DropKind.Mesos ? "ui/hud_coin.png" : _items[d.Id].Image;
        var tex = _textures[image];
        int x = (int)Math.Floor(d.X) - tex.Width / 2;
        int y = (int)Math.Floor(d.Y) - tex.Height;
        DrawTexture(image, x, y, false);
    }

    // One drop landed: mesos draw the HUD coin, potions their own icon.
    // Fresh kill drops land in view: every local judge drains right after
    // striking (net play gets them pushed instead).
    private void DrainDrops(MobWorld world)
    {
        foreach (var d in world.TakeDrops())
            ApplyDropSpawn(d.Uid, d.X, d.Y, d.Kind, d.Id, d.Count);
    }

    private void ApplyDropSpawn(int uid, float x, float y, int kind, int id, int count)
    {
        var o = new GameObject();
        _drops[uid] = o.Add(new DropArt { X = x, Y = y, Kind = kind, Id = id, Count = count });
        _world.AddChild(o);
    }

    // One drop gone: everyone clears the view, the taker claims the loot.
    // Mesos pile up; known drops land in a bag (equips in EQUIP, potions in USE).
    private void ApplyDropTake(int uid, bool mine, int kind, int id, int count)
    {
        if (_drops.Remove(uid, out var gone)) _world.RemoveChild(gone.Owner);
        var chara = _save?.Slots[_slot];
        if (!mine || chara == null) return;
        PlaySfx("pickup");
        if (kind == DropKind.Mesos)
        {
            chara.Mesos += id;
            GameLog($"picked up {id} mesos");
        }
        else // the server only drops known ids
        {
            var bag = _items[id].Slot != null
                ? chara.EquipBag ??= new Dictionary<int, int>()
                : chara.Use ??= new Dictionary<int, int>();
            bag.TryGetValue(id, out int have);
            bag[id] = have + count;
            GameLog($"picked up {_items[id].Name} x{count}");
        }
    }
}
