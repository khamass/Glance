namespace Glance;

internal sealed class CloseUpAngle
{
    // Character Configuration's 0–100 slider maps to -0.08–0.21.
    public const float Angle50 = -0.08f + 0.5f * (0.21f - -0.08f);
    public bool Active { get; private set; }

    public bool Update(float distance, float normalMinimum)
    {
        if (!float.IsFinite(distance) || !float.IsFinite(normalMinimum)) return Active = false;
        // A small exit margin avoids flickering at the normal zoom boundary.
        return Active = distance < normalMinimum + (Active ? 0.05f : 0f);
    }

    public void Reset() => Active = false;
}
