namespace Glance;

internal static class CloseUpZoom
{
    public static float Sanitize(float delta) => float.IsFinite(delta) ? Math.Clamp(delta, 0.01f, 1f) : 0.1f;

    public static float Apply(float original, bool enabled, float distance, float threshold, float delta)
        => enabled && float.IsFinite(distance) && distance > 0
            && float.IsFinite(threshold) && distance <= threshold
            ? Sanitize(delta) : original;
}
