using System.Numerics;
using Glance;

ExpressionChecks.Run();

static void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
static bool Near(float a, float b) => MathF.Abs(a - b) < 0.0001f;

var cadence = new WinkCadence();
var rotations = new RotationCadence();
var yaw = 0f;
rotations.Update(yaw, 0, 0);
for (var turn = 1; turn <= 8; turn++)
{
    for (var frame = 0; frame < 20; frame++)
    {
        yaw += 0.02f;
        Check(!rotations.Update(yaw, 0, 1f / 60), "Continuous drag does not count multiple turns");
    }
    Check(rotations.Update(yaw, 0, 0.31f) == (turn % 4 == 0), $"Rotation wink cadence on turn {turn}");
    Check(!rotations.Update(yaw, 0, 3), "Stopped camera does not recount turn");
}
rotations.ResetMotion();
rotations.Update(0, 0, 0);
rotations.Update(0.01f, 0, 0.01f);
Check(!rotations.Update(0.01f, 0, 1), "Tiny adjustment is ignored");
for (var visit = 1; visit <= 12; visit++)
{
    Check(cadence.EnterZoom() == (visit % 4 == 0), $"Wink cadence on zoom-in {visit}");
    Check(!cadence.EnterZoom() && !cadence.EnterZoom(), "Remaining zoomed in does not retrigger wink");
    cadence.LeaveZoom();
}

Check(Near(FacingMath.CameraYaw(Vector3.Zero, Vector3.UnitZ), 0), "Camera south produces zero yaw");
Check(Near(FacingMath.CameraYaw(Vector3.Zero, Vector3.UnitX), MathF.PI / 2), "Camera east produces positive quarter turn");
Check(Near(MathF.Abs(FacingMath.CameraYaw(Vector3.Zero, -Vector3.UnitZ)), MathF.PI), "Camera north produces half turn");
var current = MathF.PI - 0.05f;
var target = -MathF.PI + 0.05f;
var next = FacingMath.Toward(current, target, 6, 1f / 60);
Check(FacingMath.Wrap(next - current) > 0 && FacingMath.Wrap(next - current) < 0.1f, "Wraparound takes shortest turn");
var at30 = 0f;
var at120 = 0f;
for (var i = 0; i < 30; i++) at30 = FacingMath.Toward(at30, 2, 6, 1f / 30);
for (var i = 0; i < 120; i++) at120 = FacingMath.Toward(at120, 2, 6, 1f / 120);
Check(Near(at30, at120), "Smoothing independent of frame rate");
Check(FacingMath.InRange(3, 3, false) && !FacingMath.InRange(3.2f, 3, false)
    && FacingMath.InRange(3.2f, 3, true) && !FacingMath.InRange(3.4f, 3, true), "Zoom hysteresis prevents threshold chatter");
Check(!FacingMath.InRange(float.NaN, 3, true) && !FacingMath.InRange(0, 3, true), "Invalid zoom does not activate");
Check(Near(FacingMath.Toward(1, 2, 6, 0), 1), "Zero elapsed time preserves rotation");
Check(!FacingMath.ShouldRefresh(0.01f, 0, 0, 0, 1), "Small camera jitter does not refresh gaze");
Check(!FacingMath.ShouldRefresh(1, 0, 0, 0, 0.2f), "Refresh cooldown limits command rate");
Check(FacingMath.ShouldRefresh(0.1f, 0, 0, 0, 0.35f), "Camera orbit refreshes gaze");
Check(FacingMath.ShouldRefresh(0, 0.1f, 0, 0, 0.35f), "Camera pitch refreshes gaze");
Check(!FacingMath.ShouldRefresh(-MathF.PI + 0.01f, 0, MathF.PI - 0.01f, 0, 1), "Yaw wraparound does not create a false refresh");
Check(!FacingMath.ShouldRefresh(float.NaN, 0, 0, 0, 1), "Invalid camera angles do not refresh gaze");

