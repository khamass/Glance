using System.Numerics;

namespace Glance;

public static class FacingMath
{
    public static bool ShouldRefresh(float yaw, float pitch, float previousYaw, float previousPitch, float elapsed)
        => elapsed >= 0.35f && float.IsFinite(yaw) && float.IsFinite(pitch)
            && (MathF.Abs(Wrap(yaw - previousYaw)) >= MathF.PI / 60f
                || MathF.Abs(pitch - previousPitch) >= MathF.PI / 60f);
    public static float Wrap(float angle) => MathF.Atan2(MathF.Sin(angle), MathF.Cos(angle));
    public static float Toward(float current, float target, float response, float seconds)
        => Wrap(current + Wrap(target - current) * (1f - MathF.Exp(-response * Math.Clamp(seconds, 0f, 0.1f))));
    public static float CameraYaw(Vector3 player, Vector3 camera)
        => MathF.Atan2(camera.X - player.X, camera.Z - player.Z);
    public static bool InRange(float distance, float threshold, bool active)
        => float.IsFinite(distance) && distance > 0.05f && distance <= threshold + (active ? 0.35f : 0f);
}

