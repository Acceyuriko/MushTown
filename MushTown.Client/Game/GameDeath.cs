using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// Death: HP 0 starts it (Die), the popup's button ends it (Revive). The dead
// character is the same doll in the ghoststand pose (the ghost body, the head,
// face, hair and gear on top) standing in front of a falling tombstone; the
// name tag and the party bar stay as they are. The keys, swings and windows
// are blocked in Update while DeathState.Dead is set.
public partial class MushTownGame
{
    private static readonly PanelLayout _deathLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-death.json"));
    private const int TombFallFrames = 12; // Tomb.img/fall: 0-11 fall, 12-18 land, 19 rests
    private const int TombRestFrame = 19;

    private UiFramesData _deathTomb = new();

    private void LoadDeathArt()
    {
        _deathTomb = LoadJson<UiFramesData>(Path.Combine(_assetsDir, "effect", "tomb.json"));
        if (_deathTomb.Frames.Count != TombRestFrame + 1)
            throw new InvalidDataException("tomb.json has the wrong frame count: re-run the export");
        foreach (var f in _deathTomb.Frames) LoadTexture(f.Image);
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
        int px = _deathLay.Get("fontPx", 16), step = _deathLay.Get("textStep", 22);
        var box = new Rectangle((GameWidth - w) / 2, (GameHeight - h) / 2, w, h);
        var lines = _ui!.Wrap("You have died. Return to the nearest town?", w - pad, px);
        int textY = (_deathLay.Get("blueH", 104) - lines.Count * step) / 2 + _deathLay.Get("textDy", 0); // centered in the blue area
        _deathHost.AddChild(SysPopUi.BuildOk(box, lines, Revive, textY, step, pad, _deathLay.Get("okY", 115), px, _deathLay));
    }

    // The tombstone first, the ghost doll in front of it.
    private void DrawDeath(CharaSlot? hero)
    {
        int x = (int)_death.X;
        var t = _deathTomb.Frames[TombFrame()];
        DrawTexture(t.Image, x - t.Ox, (int)_death.TombY - t.Oy, false);
        float bob = MathF.Sin(TimeMs / 400f) * _deathLay.Get("ghostBob", 4);
        int gy = (int)(_death.GroundY - _deathLay.Get("ghostLiftY", 0) + bob);
        DrawAvatar(x, gy, "ghoststand", IdleFrame("ghoststand", hero?.Skin ?? 0), _mover.FacingRight, hero);
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
