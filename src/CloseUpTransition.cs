namespace Glance;

internal static class CloseUpTransition
{
    public const float Width = 0.75f;
    public static float Weight(float distance, float threshold)
    {
        if (!float.IsFinite(distance) || distance <= 0 || !float.IsFinite(threshold)) return 0;
        var t = Math.Clamp((threshold + Width - distance) / Width, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static float Smooth(float current, float target, float dt, float seconds)
    {
        if (!float.IsFinite(dt) || dt <= 0) return current;
        seconds = float.IsFinite(seconds) ? Math.Clamp(seconds, 0.1f, 1.5f) : 0.35f;
        var value = current + (target - current) * (1f - MathF.Exp(-6f * Math.Min(dt, 0.25f) / seconds));
        return MathF.Abs(value - target) < 0.001f ? target : value;
    }
}
