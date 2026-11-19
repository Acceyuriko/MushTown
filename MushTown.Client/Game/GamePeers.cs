using MushTown.Shared;

namespace MushTown.Client;

// Peers: remote heroes, their plate store, and our own plate sync.
public partial class MushTownGame
{
    private void ClearPeers()
    {
        foreach (var r in _remotes.Values) _world.RemoveChild(r.Owner);
        _remotes.Clear();
        _appearances.Clear();
    }

    // One peer gone (left or walked to another map): the Owner leaves with
    // the dict entry. The name stays (ids never repeat).
    private void RemovePeer(int id)
    {
        if (_remotes.Remove(id, out var gone)) _world.RemoveChild(gone.Owner);
    }

    // One peer state: spawn-or-update the pose (a new peer pulls its plate
    // from the session names when the Name beat the first state).
    // The slow fall has no message: a peer's bubble is derived from its pose (fly = floating), so two cases differ from
    // what the hero's own client shows (kept on purpose, no extra message): a weapon swing during the float replaces the
    // fly pose, so the peer's bubble goes out and starts again from its first frame when the swing is over; and a boost
    // during the float (Up and Jump together) plays the end effect (special) on the hero's own client, while a peer sees
    // fly turn into alert and plays no end effect.
    private void ApplyRemote(RemoteEvent e)
    {
        if (!_remotes.TryGetValue(e.Id, out var p))
        {
            var o = new GameObject();
            p = o.Add(new PeerArt(e.Id, _appearances));
            var plate = o.Add(new PlateArt());
            o.Add(new FloatBubble());
            _world.AddChild(o);
            _remotes[e.Id] = p;
            if (_names.TryGetValue(e.Id, out var n)) { plate.HasName = true; plate.Name = n.Name; plate.Plate = n.Plate; }
        }
        p.X = e.X;
        p.Y = e.Y;
        p.Action = e.Action;
        p.Bubble.Active = e.Action == Net.ActFly; // the pose is all a peer sends of the slow fall
        p.Frame = e.Frame;
        p.FacingRight = e.FacingRight;
    }

    // Looks can beat the first State: store by player id, and repaint a peer
    // that already exists. A later State then spawns with the stored look.
    private void DrainAppearances()
    {
        while (_net!.TryTakeAppearance(out var look))
        {
            _appearances[look.Id] = look;
            if (_remotes.TryGetValue(look.Id, out var p)) p.SetAppearance(look);
        }
    }

    // The hero's plate follows the live chara (name + worn ring). Update
    // re-syncs every frame, so equips never go stale; shots sync once after
    // staging. No chara, no plate (CLI sessions).
    private void SyncHeroPlate()
    {
        var hero = _save?.Slots[_slot];
        if (hero == null) return;
        _heroPlate.HasName = true;
        _heroPlate.Name = hero.Name;
        _heroPlate.Plate = PlateFor(hero.Equips);
    }
}
