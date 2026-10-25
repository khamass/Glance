using System.Numerics;

namespace Glance;

internal static class GazeTracking
{
    // Verified against native /facecamera captures on game 2026.10.16:
    // LookAt.CameraVector holds SceneCamera.Position, not Euler angles.
    // Reject incompatible captures instead of guessing another representation.
    public static bool Matches(Vector3 captured, Vector3 cameraPosition) =>
        IsFinite(captured) && IsFinite(cameraPosition)
        && Vector3.DistanceSquared(captured, cameraPosition) <= 0.0001f;

    public static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
}
