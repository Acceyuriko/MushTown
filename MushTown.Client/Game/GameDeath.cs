using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Death: HP 0 starts it (Die), the popup's OK ends it (Revive). The dead
// character is a ghost behind a falling tombstone; the keys, swings and
// windows are blocked in Update while DeathState.Dead is set.
public partial class MushTownGame
{
    private static readonly PanelLayout _deathLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-death.json"));
    private const int TombFallFrames = 12; // Tomb.img/fall: 0-11 fall, 12-18 land, 19 rests
    private const int TombRestFrame = 19;

    private UiFramesData _deathTomb = new();
    private UiFramesData _deathGhost = new();

    private void LoadDeathArt()
    {
        _deathTomb = LoadJson<UiFramesData>(Path.Combine(_assetsDir, "effect", "tomb.json"));
        _deathGhost = LoadJson<UiFramesData>(Path.Combine(_assetsDir, "effect", "ghost.json"));
        if (_deathTomb.Frames.Count != TombRestFrame + 1 || _deathGhost.Frames.Count == 0)
            throw new InvalidDataException("tomb.json / ghost.json have the wrong frame count: re-run the export");
        foreach (var f in _deathTomb.Frames.Concat(_deathGhost.Frames)) LoadTexture(f.Image);
        foreach (string f in new[] { "wm_pop_top.png", "wm_pop_mid.png", "wm_pop_bottom.png", "wm_pop_yes_n.png", "wm_pop_yes_h.png", "wm_pop_yes_p.png" })
            LoadTexture("ui/" + f);
    }

    // HP hit 0: stop where we stand, drop the tombstone, ask for the revive.
    private void Die()
    {
        _hurt.Clear();
        ResetMotion();
        var ground = SceneMath.GroundBelow(_map.Footholds, (int)_mover.X, (int)_mover.Y);
        _death.Die(_mover.X, ground != null ? SceneMath.FootholdY(ground, (int)_mover.X) : _mover.Y);
        _talkNpc = 0; _talkQuest = 0; _talkReward = 0; _talkPage = 0; // the keys are off: end any talk here
        _showShop = false; _shopQtyId = 0; _shopNotice = ""; _shopQtyField.Unfocus(); // and the NPC-bound shop
        LogEvent("you died");
    }

    // The OK button: full pools, the town the map belongs to.
    private void Revive()
    {
        int town = DeathState.ReviveMap(_map.Info); // first: a bad map must leave the character dead, not half healed
        if (_save?.Slots[_slot] is CharaSlot chara)
        {
            chara.Hp = MaxHpNow();
            chara.Mp = MaxMpNow();
        }
        _death.Revive();
        if (town != _mapId) TravelTo(town);
        else
        {
            Spawn();
            SnapCamera();
        }
        LogEvent("you are back in town");
    }

    // The popup root is built once with the HUD: the host switches on with
    // DeathState.Dead, the blocker under the box takes every click.
    private void BuildDeathScene()
    {
        _deathHost.Clear();
        var full = new Rectangle(0, 0, GameWidth, GameHeight);
        var blocker = new GameObject();
        blocker.Add(new Rect(full));
        blocker.Add(new Button { Kind = ButtonKind.Silent, OnClick = () => { } });
        _deathHost.AddChild(blocker);
        int w = _deathLay.Get("popW", 318), h = _deathLay.Get("popH", 160), pad = _deathLay.Get("textPad", 32);
        var box = new Rectangle((GameWidth - w) / 2, (GameHeight - h) / 2, w, h);
        _deathHost.AddChild(SysPopUi.BuildOk(box,
            _ui!.Wrap("You have died. Return to the nearest town?", w - pad),
            Revive, _deathLay.Get("textY", 38), _deathLay.Get("textStep", 17), pad, _deathLay.Get("okY", 115), _deathLay));
    }

    // The ghost first, the tombstone over it: the ghost floats behind it.
    private void DrawDeath()
    {
        int x = (int)_death.X;
        var g = _deathGhost.Frames[GhostFrame()];
        float bob = MathF.Sin(TimeMs / 400f) * _deathLay.Get("ghostBob", 4);
        int gy = (int)(_death.GroundY - _deathLay.Get("ghostLiftY", 26) + bob);
        int gw = _textures[g.Image].Width;
        DrawTexture(g.Image, _mover.FacingRight ? x - (gw - g.Ox) : x - g.Ox, gy - g.Oy, _mover.FacingRight);
        var t = _deathTomb.Frames[TombFrame()];
        DrawTexture(t.Image, x - t.Ox, (int)_death.TombY - t.Oy, false);
    }

    private int GhostFrame()
    {
        int total = _deathGhost.Frames.Sum(f => f.Delay);
        int ms = (int)TimeMs % total;
        for (int i = 0; i < _deathGhost.Frames.Count; i++)
        {
            ms -= _deathGhost.Frames[i].Delay;
            if (ms < 0) return i;
        }
        return 0;
    }

    // Falling: the first twelve frames loop. Landed: the dust frames play
    // once, then the tombstone rests.
    private int TombFrame()
    {
        if (!_death.Landed) return (int)(_death.FallMs / _deathTomb.Frames[0].Delay) % TombFallFrames;
        float ms = _death.LandedMs;
        for (int i = TombFallFrames; i < TombRestFrame; i++)
        {
            ms -= _deathTomb.Frames[i].Delay;
            if (ms < 0) return i;
        }
        return TombRestFrame;
    }
}
