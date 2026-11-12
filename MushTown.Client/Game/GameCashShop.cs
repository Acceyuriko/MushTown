using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The cash shop is a scene, not a panel over the world: the player leaves
// the map, the world keeps running, and only the shop is drawn. The server
// gets one CashShop frame and the next State re-enters the saved spot.
public partial class MushTownGame
{
    private bool _cashShopScene;

    // Called by CashShopWindow.OnShown, after Show is set.
    internal void EnterCashShop()
    {
        _cashShopScene = true;
        _hand.Return();
        ClearHeld();
        _uiScene.ClearHover();
        if (_shotPath != null) return; // shots have no world/session to leave

        if (_net != null && _net.Connected)
        {
            _net.SendCashShop();
            ClearPeers(); // the peer states are map events: idle while away
            ClearMobs();
            ClearDrops();
        }
        else if (_localWorld == null)
        {
            StartLocalWorld(); // a solo session still has a world to tick
        }
        PlayScreenBgm();
    }

    // Called by CashShopWindow.OnClosed: come back at the saved spot.
    internal void LeaveCashShop()
    {
        if (!_cashShopScene) return;
        _cashShopScene = false;
        if (_shotPath == null && _net != null && _net.Connected)
        {
            ClearPeers();
            ClearMobs();
            ClearDrops();
            DropNumbers();
            DropFx();
            _net.SendState(_mapId, _mover.X, _mover.Y, Net.ActStand, 0, _mover.FacingRight);
        }
        PlayScreenBgm();
    }

    // The shop is open: drain the social queues (chat, party, announcements),
    // discard map events, and tick the solo world without a player.
    private void UpdateCashShop(float dt, int dtMs, GameTime gameTime)
    {
        if (_net != null)
        {
            DrainSocialNet();
            DiscardMapNet();
            if (!_net.Connected)
            {
                ClearPeers();
                ClearParty();
                if (_localWorld == null) StartLocalWorld();
            }
        }
        else if (_localWorld == null)
        {
            StartLocalWorld();
        }
        TickLocalWorld(dtMs, withPlayer: false);
        _uiScene.Update(dt);
        _cashShopWindow.UpdatePreview(dtMs);
        base.Update(gameTime);
    }

    // The social half of the net drain, shared by the world and the shop.
    private void DrainSocialNet()
    {
        while (_net!.TryTakeName(out var n))
        {
            _names[n.Id] = n;
            if (_remotes.TryGetValue(n.Id, out var p))
            {
                var plate = p.Owner.Get<PlateArt>()!;
                plate.HasName = true;
                plate.Name = n.Name;
                plate.Plate = n.Plate;
            }
        }
        while (_net.TryTakeChat(out var chat)) _chatWindow.AddLine(chat.Name, chat.Text);
        while (_net.TryTakeParty(out var party)) SetPartyMembers(party.Members);
        while (_net.TryTakePartyPlayers(out var players)) SetPartyPlayers(players.Players);
        while (_net.TryTakePartyAsk(out var ask)) SetPartyAsk(ask);
        while (_net.TryTakeExp(out long exp))
        {
            LogEvent($"party share +{exp} exp");
            GainExp(exp);
        }
        // Quest events drain before mob hits: a level-up inside ApplyHit
        // re-syncs, and the sync must carry the counters this frame's kills
        // pushed (not wipe them on the server's copy).
        while (_net.TryTakeQuest(out var q))
        {
            if (q.Kill) ApplyQuestKill(q);
            else ApplyQuestOk(q);
        }
    }

    private void DiscardMapNet()
    {
        while (_net!.TryTake(out _)) { }
        while (_net.TryTakeMob(out _)) { }
        while (_net.TryTakeMove(out _)) { }
        while (_net.TryTakeHurt(out _)) { }
        while (_net.TryTakeDrop(out _)) { }
        while (_net.TryTakeFx(out _)) { }
    }

    // The local world's one step. The shop calls it with no player: mobs keep
    // moving and respawning, but nothing can hit the player who is away.
    private void TickLocalWorld(int dtMs, bool withPlayer)
    {
        if (_localWorld == null) return;
        var players = new List<(int id, int map, float x, float y)>();
        if (withPlayer) players.Add((0, _mapId, _mover.X, _mover.Y));
        var ev = _localWorld.Tick(dtMs, players);
        foreach (var mv in ev.Moves) ApplyMove(mv.Uid, mv.X, mv.Y, mv.FacingRight);
        foreach (var h in ev.Hurts)
            if (ApplyHurt(h.Damage))
                break;
        ApplySpawns(ev.Spawns);
        foreach (var t in ev.Takes) if (_drops.Remove(t.uid, out var gone)) _world.RemoveChild(gone.Owner);
    }
}
