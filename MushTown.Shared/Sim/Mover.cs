namespace MushTown.Shared;

// One frame of keys plus buffed speeds, built by the Client (it owns the
// keyboard and the buffs). The mover never touches either directly, so this
// file stays headless: the server could step it with scripted inputs.
public struct PlayerInput
{
    public int Dir; // -1/0/1 from Left/Right
    public bool Up;
    public bool Down;
    public bool JumpHeld; // Alt held
    public bool JumpPressed; // Alt fresh edge
    public bool UpPressed; // Up fresh edge
    public bool Locked; // swing lock: feet planted, facing kept
    public float Walk; // buffed run speed, px per second
    public float Jump; // buffed take-off velocity, px per second
}

// Walk, jump, ladder, portal: the player's feet, stepped once per frame.
// Moved from the Client in S2, behavior kept. The seam: the Client sets
// Map (on load/switch) and Input (every frame), steps the world, then
// drains the outputs (JumpSfx/Travel/FellOut). Skills poke the state
// directly (Vy for leaps, X/Ground for the dash), same order as before.
public sealed class PlayerMover : Component
{
    // Walk/jump feel, tuned by hand (approximate MapleStory values).
    public const float WalkSpeed = 140f; // px per second (the Client scales these two by Warrior Mastery)
    public const float JumpV0 = -520f; // take-off velocity, px per second
    private const float Gravity = 1800f; // pull-down, px per second squared
    private const float MaxFall = 700f; // terminal velocity: long drops stop speeding up here
    private const float AirAccel = 100f; // weak air drift: a full jump shifts vx by ~60
    private const float ClimbSpeed = 120f; // ladder/rope slide, px per second
    private const int DropFrames = 3; // down+jump ignores landings for ~3 frames: just enough to clear the dropped line
    private const int PortalCooldown = 30; // frames after travel during which Up cannot travel again (~0.5s)
    private const int GrabCooldown = 20; // frames after leaving a ladder during which Up/Down cannot re-grab (~0.33s)

    // Feet position, horizontal/vertical speed, and the foothold stood on
    // (null means airborne, so there is no separate on-ground flag).
    public float X, Y, Vx, Vy;
    public FootholdItem? Ground;
    public LadderItem? Climb; // set while on a ladder or rope
    public bool ClimbMoving; // climb pose only animates while sliding
    public bool FacingRight; // raw sprites face left; the Client mirrors when this is set
    public bool WasInPortal; // Up held across frames only travels on walk-in, never standing still
    public string Action = "stand2"; // art key the Client draws (and the net maps to a code)

    public MapData Map = null!; // current map, set by the Client on load and switch
    public PlayerInput Input; // this frame's snapshot, set by the Client before the step

    // Outputs, fresh every step: the Client drains them after the world runs.
    public bool JumpSfx; // a jump or ladder leap fired: play the jump sound
    public PortalItem? Travel; // Up at a portal: switch to this one after the step
    public bool FellOut; // left the world below VrBottom: respawn after the step

    private int _dropFrames; // down+jump: skip landings until this hits 0
    private int _portalCooldown; // travel cooldown, ticks down alongside the beat
    private int _grabCooldown; // ladder re-grab block after jumping off or mounting the top

    // Stop all motion: still speeds, no ladder, no drop-through. (In-mover
    // callers run under !Locked; the Client zeroes its own lock separately.)
    public void Reset()
    {
        Vx = 0;
        Vy = 0;
        Climb = null;
        ClimbMoving = false;
        _dropFrames = 0;
    }

    // Travel epilogue: still plus the re-travel block.
    public void OnTravel()
    {
        Reset();
        _portalCooldown = PortalCooldown;
    }

    public override void Update(float dt)
    {
        if (_portalCooldown > 0) _portalCooldown--;
        if (_grabCooldown > 0) _grabCooldown--;
        JumpSfx = false; Travel = null; FellOut = false; // fresh outputs per step
        int dir = Input.Locked ? 0 : Input.Dir; // mid-swing: feet planted, facing kept
        if (!Input.Locked && Input.Dir != 0 && Climb == null) FacingRight = Input.Dir > 0;

        // Up at a portal travels: tap anywhere in range, or hold Up and walk
        // in (portal wins over ladder). Travel cools down for ~0.5s, and
        // standing in range with Up held needs a fresh tap, so arrivals never
        // ping-pong back.
        var portal = SceneMath.FindPortal(Map.Portals, X, Y);
        bool wantPortal = Input.UpPressed || (Input.Up && !WasInPortal);
        bool traveled = false;
        if (Climb == null && portal != null && wantPortal && _portalCooldown == 0)
        {
            Travel = portal; // the Client switches maps after the step, then re-seeds WasInPortal
            traveled = true;
        }
        if (Climb == null && !traveled && _grabCooldown == 0 && (Input.Up || Input.Down) && !Input.Locked)
            GrabLadder(Input.Up, Input.Down);
        if (!traveled) WasInPortal = portal != null;

        // Alt+side leaps off faster than a run-up with half a jump of up
        // (rope and ladder feel the same); Alt alone holds on
        if (Climb != null && Input.JumpPressed && dir != 0)
        {
            Climb = null;
            ClimbMoving = false;
            Vy = Input.Jump / 2;
            JumpSfx = true;
            Vx = dir * Input.Walk * 1.5f;
            FacingRight = dir > 0;
            Ground = null;
            _grabCooldown = GrabCooldown;
        }
        if (Climb != null) ClimbMove(Input.Up, Input.Down, dt);

        // held Alt jumps (landings chain into the next jump); down+jump instead
        // drops through the platform, one floor per tap, and only over a floor
        bool wantJump = Input.JumpHeld && !Input.Down && Ground != null && Climb == null && !Input.Locked;
        bool wantDrop = Input.JumpPressed && Input.Down && Ground != null && Climb == null && !Input.Locked
            && SceneMath.GroundBelow(Map.Footholds, (int)Math.Floor(X), (int)Math.Ceiling(Y) + 1) != null;

        // horizontal: full speed on ground, keep momentum plus weak drift in the air
        bool moved = false; // false when still or already at the wall: feet stay, so stand, not walk
        float sx = X, nx = X; // stride start and end (the ground follow walks between them)
        if (Climb == null)
        {
            if (Ground != null)
                Vx = Input.Down ? 0 : dir * Input.Walk; // held Down pins the feet: prone
            else
                Vx = Math.Clamp(Vx + dir * AirAccel * dt, -Input.Walk, Input.Walk);
            float rawX = X + Vx * dt;
            nx = Math.Clamp(rawX, Map.Info.VrLeft, Map.Info.VrRight);
            if (nx != rawX) Vx = 0; // hit a wall: momentum dies here
            moved = nx != X;
            X = nx;
        }
        if (Ground != null && Climb == null)
        {
            // follow the line pixel by pixel: one 2.3px stride would skip the
            // joint pixel on slopes and read a 2px gap in a continuous line
            int x0 = (int)Math.Floor(sx), x1 = (int)Math.Floor(nx);
            int stepX = Math.Sign(x1 - x0);
            int gx = x0;
            while (Ground != null)
            {
                if (gx < Math.Min(Ground.X1, Ground.X2) || gx > Math.Max(Ground.X1, Ground.X2))
                {
                    var step = SceneMath.StepTo(Map.Footholds, gx, (int)Math.Floor(Y));
                    if (step == null)
                    {
                        Ground = null; // walked off the edge: fall from here
                        break;
                    }
                    Ground = step;
                }
                Y = SceneMath.FootholdY(Ground, gx);
                if (gx == x1) break;
                gx += stepX;
            }
        }

        if (wantJump)
        {
            Vy = Input.Jump;
            Ground = null;
            JumpSfx = true;
        }
        if (wantDrop)
        {
            Ground = null; // fall through; the landing check skips for a few frames
            _dropFrames = DropFrames;
        }

        // vertical: one-way platforms (pass through going up, land coming down)
        if (Ground == null && Climb == null)
        {
            Vy += Gravity * dt;
            Vy = Math.Min(Vy, MaxFall);
            float ny = Y + Vy * dt;
            if (Vy > 0 && _dropFrames == 0)
            {
                int ix = (int)Math.Floor(X), iy = (int)Math.Ceiling(Y); // ceiling: never snap up through a line
                var g = SceneMath.GroundBelow(Map.Footholds, ix, iy);
                if (g != null && ny >= SceneMath.FootholdY(g, ix))
                {
                    Y = SceneMath.FootholdY(g, ix);
                    Vy = 0;
                    Ground = g;
                }
                else
                {
                    Y = ny;
                }
            }
            else
            {
                Y = ny;
            }
            if (_dropFrames > 0) _dropFrames--;
            if (Ground == null && Y >= Map.Info.VrBottom) // fell out of the world: the Client respawns after the step
                FellOut = true;
        }

        Action = Input.Locked ? "swingP1"
            : Climb != null ? (Climb.Ladder ? "ladder" : "rope")
            : Ground == null ? "jump" : Input.Down ? "prone" : dir != 0 && moved ? "walk2" : "stand2";
    }

    // Held Up/Down near a ladder or rope grabs on, centered on its x.
    // Down-only at or below the foot does nothing (else it grabs and lets
    // go in the same frame: a visible hop).
    private void GrabLadder(bool up, bool down)
    {
        var l = SceneMath.FindLadder(Map.Ladders, X, Y);
        if (l == null) return;
        if (!up && down && Y >= l.Y2) return;
        Reset();
        Climb = l;
        X = l.X;
        Ground = null;
    }

    // Slide along the grabbed ladder or rope. Up at the top steps onto the
    // platform above when one is there; Down at the bottom lets go.
    private void ClimbMove(bool up, bool down, float dt)
    {
        var l = Climb!;
        int dirY = (down ? 1 : 0) - (up ? 1 : 0);
        float ny = Math.Clamp(Y + dirY * ClimbSpeed * dt, l.Y1, l.Y2);
        ClimbMoving = ny != Y;
        if (up && ny <= l.Y1)
        {
            var g = SceneMath.MountTop(Map.Footholds, l.X, l.Y1);
            if (g != null)
            {
                Ground = g;
                Y = SceneMath.FootholdY(g, l.X);
                Climb = null;
                ClimbMoving = false;
                _grabCooldown = GrabCooldown; // just mounted: walk off before Up can re-grab
                return;
            }
        }
        if (down && !up && ny >= l.Y2)
        {
            Climb = null;
            ClimbMoving = false; // let go, gravity takes over
        }
        Y = ny;
    }
}
