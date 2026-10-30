using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The speech bubble over the player: the official MapleStory balloon
// (UI/ChatBalloon.img/0) with the message the player sent. The bubble
// hangs over the head, so it moves with the character.
public partial class MushTownGame
{
    // Every number of the bubble lives in layouts/panel-balloon.json.
    internal static readonly PanelLayout _balloonLay = PanelLayout.Load(Path.Combine(MushTownGame.LayoutsDir, "panel-balloon.json"));

    // The message in the bubble and the clock it goes away at.
    private string _balloonText = "";
    private double _balloonUntil;

    // The player sent a message: the bubble shows it for a few seconds.
    internal void ShowBalloon(string text)
    {
        _balloonText = text;
        _balloonUntil = _timeMs + _balloonLay.Get("lifeMs", 4000);
    }

    // The balloon art (the exporter writes assets/ui/balloon_*.png).
    internal void LoadBalloonArt()
    {
        foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" })
            LoadTexture($"ui/balloon_{piece}.png");
        LoadTexture("ui/balloon_arrow.png");
    }

    // The bubble over the head, in the tree (its lines wrap once per message).
    private Balloon MakeBalloon() => new(BubbleText, FeetOnScreen, _balloonLay);

    // The feet of the character on screen: the bubble hangs over the head.
    private Point FeetOnScreen() => new((int)Math.Floor(_mover.X) - _camera.X, (int)Math.Floor(_mover.Y) - _camera.Y);

    // The message the bubble shows: the one the player sent.
    private string BubbleText()
    {
        if (_balloonText == "") return "";
        if (_timeMs <= _balloonUntil) return _balloonText;
        _balloonText = ""; // the bubble is gone
        return "";
    }
}
