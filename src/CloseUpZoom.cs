namespace Glance;

internal static class CloseUpZoom
{
    public static float Sanitize(float delta) => float.IsFinite(delta) ? Math.Clamp(delta, 0.01f, 1f) : 0.1f;

    public static float Apply(float original, bool enabled, float distance, float threshold, float delta)
    {
        if (!enabled || !float.IsFinite(original) || original <= 0) return original;
        var blend = CloseUpTransition.Weight(distance, threshold);
        if (blend <= 0) return original;
        if (blend >= 1) return Sanitize(delta);
        return original + (Sanitize(delta) - original) * blend;
    }
}
