namespace MushTown.Shared;

// One step of a body action script: show one frame of an existing action for DelayMs (WZ Character 00002000.img/<name>: each step is
// an action name, a frame and a delay; the sign of the delay only marks the attack frame, the time is its absolute value).
public sealed class ActionStep
{
    public string Action { get; set; } = "";
    public int Frame { get; set; }
    public int DelayMs { get; set; }
}

// The body pose of a skill whose WZ action is a script (Poison Breath): the steps play in order on a clock of their own and Current is the step
// on show (the last one once the time is up). The Client ticks it while the cast lock runs, scaled like a swing. Held directly like SwingActions
// (never added to an object).
public sealed class ActionScript : Component
{
    private IReadOnlyList<ActionStep> _steps = Array.Empty<ActionStep>();
    private float _ms;

    public bool Active => _steps.Count > 0;

    public ActionStep Current
    {
        get
        {
            float left = _ms;
            foreach (var step in _steps)
            {
                if (left < step.DelayMs) return step;
                left -= step.DelayMs;
            }
            return _steps[^1];
        }
    }

    public void Start(IReadOnlyList<ActionStep> steps)
    {
        _steps = steps;
        _ms = 0;
    }

    // A plain swing or a basic attack follows: the script is over.
    public void Clear() => _steps = Array.Empty<ActionStep>();

    public void Tick(float ms) => _ms += ms;
}
