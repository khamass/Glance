using Glance;

internal static class CloseUpChecks
{
    public static void Run()
    {
        static void Check(bool value, string name)
        {
            if (!value) throw new Exception(name);
            Console.WriteLine("PASS " + name);
        }
        var limit = new CloseUpLimit();
        Check(CloseUpZoom.Apply(0.75f, true, 1f, 3f, 0.1f) == 0.1f,
            "Close-up uses the selected zoom delta");
        Check(CloseUpZoom.Apply(0.75f, true, 3.01f, 3f, 0.1f) == 0.75f
            && CloseUpZoom.Apply(0.75f, false, 1f, 3f, 0.1f) == 0.75f,
            "Outside close-up or disabled returns the original zoom speed");
        Check(CloseUpZoom.Apply(0.75f, true, float.NaN, 3f, 0.1f) == 0.75f
            && CloseUpZoom.Apply(0.75f, true, 0f, 3f, 0.1f) == 0.75f,
            "Invalid camera distance preserves the original zoom behavior");
        Check(CloseUpZoom.Sanitize(float.NaN) == 0.1f && CloseUpZoom.Sanitize(-2f) == 0.01f
            && CloseUpZoom.Sanitize(5f) == 1f, "Zoom delta is bounded and finite");
        var setting = new CameraAngleSetting();
        var configuredAngle = CloseUpAngle.ToNative(20);
        var savedAngle = configuredAngle;
        var writes = 0;
        void Write(float value) { configuredAngle = value; writes++; }
        setting.Apply(configuredAngle, CloseUpAngle.Angle50, Write);
        Check(configuredAngle == CloseUpAngle.Angle50, "Close-up writes the actual game configuration angle");
        setting.Apply(configuredAngle, CloseUpAngle.Angle50, Write);
        Check(writes == 1, "Unchanged configuration does not produce repeated writes");
        configuredAngle = CloseUpAngle.ToNative(35);
        setting.Apply(configuredAngle, CloseUpAngle.ToNative(70), Write);
        setting.Restore(Write);
        Check(configuredAngle == savedAngle, "External writes and slider changes never replace the captured original angle");
        setting.Restore(Write);
        Check(writes == 3, "Repeated cleanup does not write again");
        Check(MathF.Abs(CloseUpAngle.ToNative(0) + 0.08f) < 0.00001f
            && MathF.Abs(CloseUpAngle.ToNative(100) - 0.21f) < 0.00001f, "Camera angle slider maps both endpoints");
        Check(CloseUpAngle.ToNative(float.NaN) == CloseUpAngle.Angle50
            && CloseUpAngle.ToNative(-50) == CloseUpAngle.ToNative(0)
            && CloseUpAngle.ToNative(150) == CloseUpAngle.ToNative(100), "Camera angle clamps invalid inputs");
        var angle = new CloseUpAngle();
        Check(!angle.Update(2f, 1.5f), "Normal zoom keeps the user's camera angle");
        Check(angle.Update(1.49f, 1.5f), "Crossing below the original minimum activates angle 50");
        Check(angle.Update(1.52f, 1.5f), "Boundary jitter does not toggle the close-up angle");
        Check(!angle.Update(1.56f, 1.5f), "Zooming out releases the angle override");
        Check(!angle.Update(1.5f, 1.5f), "Angle override waits until inside extended range");
        angle.Update(0.5f, 1.5f);
        angle.Reset();
        Check(!angle.Active && !angle.Update(float.NaN, 1.5f), "Pauses and invalid zoom release the angle override");
        Check(MathF.Abs(CloseUpAngle.Angle50 - 0.065f) < 0.00001f, "Camera angle 50 maps to the native tilt value");
        Check(limit.Apply(1.5f, 0.5f) == 0.5f, "Extended zoom lowers the normal minimum");
        Check(limit.Apply(0.5f, 0.2f) == 0.2f, "Slider updates preserve the original limit");
        Check(limit.Restore(0.2f) == 1.5f && !limit.Active, "Disabling restores the original camera limit");
        Check(limit.Apply(0.05f, 0.5f) == 0.05f, "Existing closer limits are not increased");
        Check(limit.Restore(0.05f) == 0.05f, "Existing camera customization survives restore");
        limit.Apply(1.5f, 0.5f);
        Check(limit.Restore(0.3f) == 0.3f, "Restore does not overwrite another plugin's later change");
        limit.Apply(1.5f, 0.5f);
        limit.Apply(0.7f, 0.2f);
        Check(limit.Restore(0.2f) == 0.7f, "Reapplying tracks a newer external camera limit");
        limit.Apply(1.5f, 0.5f);
        limit.Forget();
        Check(limit.Restore(0.8f) == 0.8f, "Camera replacement discards the old snapshot");
        Check(CloseUpLimit.Sanitize(float.NaN) == 0.5f
            && CloseUpLimit.Sanitize(float.PositiveInfinity) == 0.5f
            && CloseUpLimit.Sanitize(-1f) == 0.1f
            && CloseUpLimit.Sanitize(20f) == 1.5f, "Invalid slider values are bounded");
    }
}
