using MushTown.Shared;

namespace MushTown.Client;

// The camera: spring chase + rounded screen offset (moved to a _world
// child in S8). No Update override: Follow stays at its exact Update
// spot (after the frame's anims, before the drains); the game reads X/Y.
public sealed class CameraComp : Component
{
    public int X;
    public int Y;
    public float FX;
    public float FY;
    public float VX;
    public float VY;

    // Teleport: snap, don't swoosh.
    public void Snap(int x, int y)
    {
        FX = x;
        FY = y;
        VX = 0;
        VY = 0;
        X = x;
        Y = y;
    }

    // One chase step toward the target (critically damped spring).
    public void Follow(float dt, int tx, int ty)
    {
        (FX, VX) = SceneMath.SpringStep(FX, VX, tx, dt);
        (FY, VY) = SceneMath.SpringStep(FY, VY, ty, dt);
        X = (int)Math.Round(FX);
        Y = (int)Math.Round(FY);
    }
}
