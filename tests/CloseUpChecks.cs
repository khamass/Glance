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
