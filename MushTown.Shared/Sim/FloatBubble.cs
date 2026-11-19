namespace MushTown.Shared;

// The slow fall's bubble on one actor (the hero or a peer): the clock of its loop and the one-time end. While Active, Ms
// counts up from 0 and the loop (WZ 2001012 specialAffected) plays behind the actor. When Active turns off, Ended is set
// once and the game plays the end effect (WZ 2001012 special) where the actor is. The owner sets Active: the hero from
// its AirMovement, a peer from its pose (the fly action of the State message).
public sealed class FloatBubble : Component
{
    public float Ms { get; private set; }
    public bool Ended { get; private set; }

    private bool _active;

    public bool Active
    {
        get => _active;
        set
        {
            if (_active && !value) Ended = true;
            if (!_active && value) Ms = 0;
            _active = value;
        }
    }

    // True once per end: the caller plays the end effect.
    public bool TakeEnded()
    {
        bool was = Ended;
        Ended = false;
        return was;
    }

    public override void Update(float dt)
    {
        if (_active) Ms += dt * 1000f;
    }
}
