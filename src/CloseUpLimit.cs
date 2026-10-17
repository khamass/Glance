namespace Glance;

// Own only the minimum we wrote; another camera plugin may change it later.
internal sealed class CloseUpLimit
{
    private float? original;
    private float applied;
    public bool Active => original.HasValue;
    public float? Original => original;
    public static float Sanitize(float value) => float.IsFinite(value) ? Math.Clamp(value, 0.1f, 1.5f) : 0.5f;

    public float Apply(float current, float requested)
    {
        if (!Active || current != applied) original = current;
        applied = Math.Min(original!.Value, Sanitize(requested));
        return applied;
    }

    public float Restore(float current)
    {
        var result = original.HasValue && current == applied ? original.Value : current;
        Forget();
        return result;
    }

    public void Forget() => original = null;
}
