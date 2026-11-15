using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        _pickupSent.Clear();
    }

    // One ground drop: the icon is centred on its point, spins while falling,
    // bobs at rest, and flies to the taker during a pickup.
    private void DrawDrop(DropArt d)
    {
        string image = d.Kind == DropKind.Mesos ? "ui/hud_coin.png" : _items[d.Id].Image;
        var tex = _textures[image];
        float x = d.X, y = d.Y;
        if (d.State == DropState.Falling)
        {
            float t = Math.Min(1f, d.SpawnMs / DropArt.FallMs);
            x = MathHelper.Lerp(d.FromX, d.X, t);
            y = MathHelper.Lerp(d.FromY, d.Y, t) - 40f * MathF.Sin(MathF.PI * t);
        }
        else if (d.State == DropState.Taken)
        {
            float t = Math.Min(1f, d.SpawnMs / DropArt.TakeMs);
            var (tx, ty) = TakerPos(d);
            x = MathHelper.Lerp(d.X, tx, t);
            y = MathHelper.Lerp(d.Y, ty - 30f, t) - 20f * MathF.Sin(MathF.PI * t);
        }
        float bob = d.State == DropState.Resting ? MathF.Sin(d.SpawnMs / 250f + d.BobPhase) * 2f : 0f;
        _spriteBatch.Draw(tex,
            new Vector2(x - _camera.X, y - _camera.Y - tex.Height / 2f + bob), null, Color.White,
            d.Rotation, new Vector2(tex.Width / 2f, tex.Height / 2f), 1f, SpriteEffects.None, 0f);
    }

    // The live feet of the player a take flies to. Our own takes fly to us;
    // a remote taker missing from this map ends at the drop point instead.
    private (float X, float Y) TakerPos(DropArt d)
    {
        if (d.Taker > 0 && d.Taker != _net?.PlayerId)
            return _remotes.TryGetValue(d.Taker, out var r) ? (r.X, r.Y) : (d.X, d.Y);
        return (_mover.X, _mover.Y);
    }

    // One drop landed: mesos draw the HUD coin, items their own icon. Fresh
    // kill drops land in view: every local judge drains right after striking
    // (net play gets them pushed instead).
    private void DrainDrops(MobWorld world, bool animate = true)
    {
        foreach (var d in world.TakeDrops())
            ApplyDropSpawn(d.Uid, d.X, d.Y, d.FromX, d.FromY, d.Tab, d.Kind, d.Id, d.Count, d.Amount, animate);
    }

    private void ApplyDropSpawn(int uid, float x, float y, float fromX, float fromY, string tab,
        int kind, int id, int count, long amount, bool animate = true)
    {
        var o = new GameObject();
        _drops[uid] = o.Add(new DropArt
        {
            FromX = fromX,
            FromY = fromY,
            X = x,
            Y = y,
            SpawnMs = 0,
            State = animate ? DropState.Falling : DropState.Resting,
            Rotation = 0,
            BobPhase = uid * 0.73f,
            Tab = tab,
            Kind = kind,
            Id = id,
            Count = count,
            Amount = amount,
        });
        _world.AddChild(o);
    }

    // One drop gone: everyone sees the take animation, the taker claims the
    // loot. taker 0 means expired: remove at once, no pickup animation.
    private void ApplyDropTake(int uid, int taker, bool mine, int kind, int id, int count, long amount)
    {
        if (_drops.TryGetValue(uid, out var gone))
        {
            if (taker == 0)
            {
                _drops.Remove(uid);
                _world.RemoveChild(gone.Owner);
            }
            else
            {
                gone.State = DropState.Taken;
                gone.SpawnMs = 0;
                gone.Rotation = 0;
                gone.Taker = taker;
            }
        }
        var chara = _save?.Slots[_slot];
        if (!mine || chara == null) return;
        PlaySfx("pickup");
        if (kind == DropKind.Mesos)
        {
            chara.Mesos += amount;
            LogEvent($"picked up {amount} mesos");
        }
        else // the server only drops known ids
        {
            string tab = DropBagRule.FromSource(gone?.Tab ?? "", _items[id]);
            if (!Inventory.Add(chara, tab, id, count))
            {
                // A second pickup can arrive before the first bag update:
                // put the item back on the ground instead of losing it.
                if (_net != null && _net.Connected) _net.SendDropItem(id, count, tab);
                else if (_localWorld != null)
                {
                    _localWorld.DropItem(_mapId, _mover.X, _mover.Y, tab, DropKind.Item, id, count);
                    DrainDrops(_localWorld);
                }
                return;
            }
            LogEvent($"picked up {_items[id].Name} x{count}");
        }
    }

    // A pickup only goes out when the bag can take it: the ground drop stays
    // put on a full tab.
    private bool CanTakeDrop(DropArt d, CharaSlot chara)
    {
        if (d.Kind == DropKind.Mesos) return true;
        return Inventory.CanAdd(chara, d.Tab, d.Id, d.Count);
    }
}
