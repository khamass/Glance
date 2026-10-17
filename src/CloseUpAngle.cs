namespace Glance;

internal sealed class CloseUpAngle
{
    // Character Configuration's 0–100 slider maps to -0.08–0.21.
    public static float ToNative(float value) => -0.08f + Math.Clamp(float.IsFinite(value) ? value : 50f, 0f, 100f) / 100f * 0.29f;
    public static float Angle50 => ToNative(50f);
    public bool Active { get; private set; }

    public bool Update(float distance, float normalMinimum)
    {
        if (!float.IsFinite(distance) || !float.IsFinite(normalMinimum)) return Active = false;
        // A small exit margin avoids flickering at the normal zoom boundary.
        return Active = distance < normalMinimum + (Active ? 0.05f : 0f);
    }

    public void Reset() => Active = false;
}
