using System.Numerics;
using Glance;

internal static class RuntimeSafetyChecks
{
    private static void Check(bool result, string description)
    {
        if (!result) throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }

    public static void Run()
    {
        var position = new Vector3(90.93067f, 1.7279847f, -128.22731f);
        Check(GazeTracking.Matches(position, position), "Native world-position capture accepted");
        Check(!GazeTracking.Matches(new Vector3(0.2f, 1.4f, 0), position), "Rotation-format capture rejected rather than guessed");
        Check(!GazeTracking.Matches(Vector3.Normalize(position), position), "Direction-format capture rejected");
        Check(!GazeTracking.Matches(position + Vector3.UnitX * 0.02f, position), "Stale camera capture rejected");
        Check(!GazeTracking.Matches(new(float.NaN, 0, 0), position), "Non-finite gaze rejected");
        Check(!GazeTracking.Matches(position, new(float.PositiveInfinity, 0, 0)), "Non-finite camera position rejected");
        var session = new GposeSession();
        Check(session.Update(false, true, 1, true) == default, "Normal update does not release gaze");
        var entry = session.Update(true, true, 1, true);
        Check(entry.Entered && entry.Preserve && !entry.Exited, "GPose entry preserves owned gaze");
        Check(session.Update(true, false, 2, false) == default, "GPose stays paused without repeating entry cleanup");
        var exit = session.Update(false, true, 1, false);
        Check(exit.Exited && exit.ReclaimGaze, "Exit releases only the originally owned normal-world gaze");
        Check(session.Update(false, true, 1, false) == default, "GPose exit cleanup occurs once");
        session.Update(true, true, 1, true);
        Check(!session.Update(false, true, 2, false).ReclaimGaze, "Changed actor is never reclaimed");
        session.Update(true, true, 1, false);
        Check(!session.Update(false, true, 1, false).ReclaimGaze, "Manually enabled gaze remains unowned");
        Check(!session.Update(true, false, 1, true).Preserve, "Disabled handoff retains ordinary pause behavior");
        Check(!session.Update(false, false, 1, false).ReclaimGaze, "Ordinary pause does not cause a second release on exit");
        foreach (var definition in Expressions.All)
        {
            var sent = new List<string>();
            bool Send(string command) { sent.Add(command); return true; }
            var playback = new ExpressionPlayback();
            playback.Start(definition, definition.Hold, 1, Send);
            playback.Discard(); // The GPose handoff must discard, never Cancel.
            playback.Update(30, 1, Send);
            Check(sent.SequenceEqual(new[] { definition.Command }) && !playback.IsBusy,
                "GPose does not dispatch queued cleanup for " + definition.Name);
        }
        var cleanup = new ExpressionPlayback();
        var neutral = new List<string>();
        bool Neutral(string command) { neutral.Add(command); return true; }
        cleanup.Cancel(1, Neutral, forceNeutral: true);
        cleanup.Discard();
        cleanup.Update(10, 1, Neutral);
        Check(neutral.Count == 1, "GPose also discards a pending neutral retry");
    }
}
