using Microsoft.Xna.Framework;
using MushTown.Shared;

namespace MushTown.Client;

// The speech bubble over the player: the official MapleStory balloon
// (UI/ChatBalloon.img/<style>) with the message the player sent. The bubble
// hangs over the head, so it moves with the character. The style is the worn
// chat ring's (info/chatBalloon).
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

    // The style of every balloon the game can draw, by style id.
    private readonly Dictionary<int, BalloonStyleData> _balloonStyles = new();
    private readonly Dictionary<int, QuestMarkData> _questMarks = new(); // where each quest balloon hangs

    // The balloon art (the exporter writes assets/ui/balloon_<style>_*.png and
    // balloon_<style>.json): the plain style and the ones a chat ring names.
    internal void LoadBalloonStyles()
    {
        foreach (int style in _items.Values.Select(d => d.ChatBalloon).Where(s => s != 0).Append(0).Distinct())
        {
            _balloonStyles[style] = LoadJson<BalloonStyleData>(Path.Combine(_assetsDir, "ui", $"balloon_{style}.json"));
            foreach (string piece in new[] { "nw", "n", "ne", "w", "c", "e", "sw", "s", "se" })
                LoadTexture($"ui/balloon_{style}_{piece}.png");
            LoadTexture($"ui/balloon_{style}_arrow.png");
        }
        for (int i = 0; i < 3; i++)
            _questMarks[i] = LoadJson<QuestMarkData>(Path.Combine(_assetsDir, "ui", $"npc_quest_{i}.json"));
    }

    // Where quest balloon n hangs from: the tail tip inside its own picture.
    internal Point QuestMarkTail(int icon) => new(_questMarks[icon].TailX, _questMarks[icon].TailY);

    // The style of the worn chat ring: the first ring wins, 0 is the plain balloon.
    // (*balloonring* shots pose the pink ring, the way *chatmin* poses a fold.)
    private BalloonStyleData BalloonStyle()
    {
        if (_shotBalloonRing) return _balloonStyles[_shotPath!.Contains("blue") ? 7 : 1]; // *balloonring* poses a chat ring: a shot never equips one
        return _balloonStyles[ItemData.WornBalloonStyle(_save?.Slots[_slot]?.Equips, _items)];
    }

    // The bubble over the head, in the tree (its lines wrap once per message).
    private Balloon MakeBalloon() => new(BubbleText, FeetOnScreen, _balloonLay, BalloonStyle);

    // The feet of the character on screen: the bubble hangs over the head.
    // The balloon hangs on the body center line, not on the feet: the body is
    // drawn off-center, so the tail would lean when the character turns.
    private Point FeetOnScreen() => new(
        BodyCenterX((int)Math.Floor(_mover.X), _mover.Action, _frame, _mover.FacingRight, _save?.Slots[_slot]?.Equips) - _camera.X,
        (int)Math.Floor(_mover.Y) - _camera.Y);

    // The message the bubble shows: the one the player sent.
    private string BubbleText()
    {
        if (_balloonText == "") return "";
        if (_timeMs <= _balloonUntil) return _balloonText;
        _balloonText = ""; // the bubble is gone
        return "";
    }
}
