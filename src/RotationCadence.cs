namespace Glance;

public sealed class RotationCadence
{
    private bool initialized;
    private float previousYaw;
    private float previousPitch;
    private float travel;
    private int turns;
    public float StillSeconds { get; private set; }

    public void ResetMotion()
    {
        initialized = false;
        travel = 0;
        StillSeconds = 0;
    }

    public void Reset() { turns = 0; ResetMotion(); }

    public bool Update(float yaw, float pitch, float dt, int frequency = 4)
    {
        if (!float.IsFinite(yaw) || !float.IsFinite(pitch)) { ResetMotion(); return false; }
        if (!initialized)
        {
            previousYaw = yaw; previousPitch = pitch; initialized = true; return false;
        }
        var delta = MathF.Abs(FacingMath.Wrap(yaw - previousYaw)) + MathF.Abs(pitch - previousPitch);
        previousYaw = yaw; previousPitch = pitch;
        if (delta > 0.0001f)
        {
            travel += delta;
            StillSeconds = 0;
            return false;
        }
        StillSeconds += Math.Max(dt, 0);
        if (StillSeconds < 0.3f) return false;
        // Ignore tiny camera adjustments; settle a drag into one distinct gesture.
        var completedTurn = travel >= MathF.PI / 18f;
        travel = 0;
        if (!completedTurn) return false;
        turns = (turns + 1) % Math.Clamp(frequency, 1, 10);
        return turns == 0;
    }
}

