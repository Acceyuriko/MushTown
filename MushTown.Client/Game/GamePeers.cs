using MushTown.Shared;

namespace MushTown.Client;

// Peers: remote heroes, their plate store, and our own plate sync.
public partial class MushTownGame
{
    private void ClearPeers()
    {
        foreach (var r in _remotes.Values) _world.RemoveChild(r.Owner);
        _remotes.Clear();
    }

    // One peer gone (left or walked to another map): the Owner leaves with
    // the dict entry. The name stays (ids never repeat).
    private void RemovePeer(int id)
    {
        if (_remotes.Remove(id, out var gone)) _world.RemoveChild(gone.Owner);
    }

    // One peer state: spawn-or-update the pose (a new peer pulls its plate
    // from the session names when the Name beat the first state).
    private void ApplyRemote(RemoteEvent e)
    {
        if (!_remotes.TryGetValue(e.Id, out var p))
        {
            var o = new GameObject();
            p = o.Add(new PeerArt { Id = e.Id });
            var plate = o.Add(new PlateArt());
            _world.AddChild(o);
            _remotes[e.Id] = p;
            if (_names.TryGetValue(e.Id, out var n)) { plate.HasName = true; plate.Name = n.Name; plate.Plate = n.Plate; }
        }
        p.X = e.X;
        p.Y = e.Y;
        p.Action = e.Action;
        p.Frame = e.Frame;
        p.FacingRight = e.FacingRight;
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
