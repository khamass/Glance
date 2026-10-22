namespace Glance;

// Capture the configured value once, never a transient camera interpolation value.
internal sealed class CameraAngleSetting
{
    public float? Original { get; private set; }
    public void Apply(float current, float requested, Action<float> write)
    {
        if (!float.IsFinite(current)) return;
        Original ??= current;
        if (MathF.Abs(current - requested) > 0.00001f) write(requested);
    }

    public void Forget() => Original = null;

    public void Restore(Action<float> write)
    {
        if (Original is not { } value) return;
        write(value);
        Original = null;
    }
}

