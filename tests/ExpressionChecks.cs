using Glance;

public static class ExpressionChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
        Check(Expressions.Pool(Personality.Off, ExpressionChoices.All) == ExpressionChoices.None, "Off has no expression commands");
        Check(Expressions.Pool(Personality.Custom, ExpressionChoices.None) == ExpressionChoices.None, "Empty custom pool stays empty");
        Check(Expressions.Pool(Personality.Wink, ExpressionChoices.All) == ExpressionChoices.Wink, "Classic preset keeps wink default");
        Check(Expressions.Pool(Personality.Friendly, ExpressionChoices.All) == (ExpressionChoices.Smile | ExpressionChoices.FakeSmile), "Friendly preset contains smiles");
        var picker = new ExpressionPicker();
        var random = new Random(8);
        ExpressionKind? previous = null;
        for (var i = 0; i < 100; i++)
        {
            var pick = picker.Pick(ExpressionChoices.All, random)!;
            if (pick.Kind == previous) throw new Exception("Repeated random expression");
            previous = pick.Kind;
        }
        Check(true, "Random pool avoids consecutive repeats");
        Check(picker.Pick(ExpressionChoices.None, random) == null, "Empty pool cannot issue a command");
        Check(picker.Pick(ExpressionChoices.Taunt, random)!.Kind == ExpressionKind.Taunt, "Single selection remains usable");
        foreach (var definition in Expressions.All)
        {
            var sent = new List<string>();
            bool Send(string command) { sent.Add(command); return true; }
            var playback = new ExpressionPlayback();
            Check(playback.Start(definition, definition.Hold, 1, Send), "Start " + definition.Name);
            Check(!playback.Start(definition, definition.Hold, 1, Send), "No overlapping expression");
            playback.Update(definition.Hold - 0.05f, 1, Send);
            Check(sent.Count == 1, "Hold lasts until configured deadline");
            playback.Update(0.06f, 1, Send);
            if (definition.Kind == ExpressionKind.Wink)
            {
                Check(sent[^1] == "/smile motion", "Wink transitions to smile");
                playback.Update(0.36f, 1, Send);
                playback.Update(2.01f, 1, Send);
            }
            Check(sent[^1] == "/straightface motion", "Expression returns to neutral");
            playback.Update(0.66f, 1, Send);
            Check(!playback.IsBusy, "Neutral fallback finishes without looping");

            sent.Clear(); playback.Start(definition, definition.Hold, 1, Send);
            playback.Cancel(1, Send);
            playback.Cancel(1, Send); // pausing every frame must not restart cleanup
            playback.Update(0.66f, 1, Send);
            Check(sent.SequenceEqual(new[] { definition.Command, "/straightface motion", "/straightface motion" }), "Zoom-out cancels queued expression steps");
            Check(!playback.IsBusy, "Repeated pause allows cleanup to finish");

            sent.Clear(); playback.Start(definition, definition.Hold, 1, Send);
            playback.Update(10, 2, Send);
            Check(sent.Count == 1 && !playback.IsBusy, "Actor replacement discards old callbacks");
        }
        var failed = new ExpressionPlayback();
        Check(!failed.Start(Expressions.All[0], 0.3f, 1, _ => false) && !failed.IsBusy, "Failed dispatch does not start a sequence");
        var neutralCommands = new List<string>();
        bool NeutralSend(string command) { neutralCommands.Add(command); return true; }
        failed.Cancel(1, NeutralSend);
        Check(neutralCommands.Count == 0, "Gaze-only mode requires no emote cleanup");
        failed.Start(Expressions.All[0], 0.3f, 1, NeutralSend);
        failed.Update(10, 1, NeutralSend);
        Check(neutralCommands.Count == 2 && neutralCommands[^1] == "/smile motion", "Frame stall does not dispatch multiple expression steps at once");
        failed.Update(10, 0, NeutralSend);
        Check(!failed.IsBusy && neutralCommands.Count == 2, "Logout discards pending expression callbacks");
        var cadence = new WinkCadence();
        Check(!cadence.EnterZoom(2), "Custom frequency waits for second zoom");
        cadence.LeaveZoom();
        Check(cadence.EnterZoom(2), "Custom frequency triggers at second zoom");
        cadence.Reset();
        Check(cadence.EnterZoom(1), "Frequency one triggers immediately");
        var rotation = new RotationCadence();
        rotation.Update(0, 0, 0, 1); rotation.Update(0.3f, 0, 0.1f, 1);
        Check(rotation.Update(0.3f, 0, 0.31f, 1), "Rotation cadence uses configurable frequency");
    }
}
