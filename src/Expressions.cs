namespace Glance;

public enum Personality { Wink, Playful, Friendly, Dramatic, Custom, Off }
public enum ExpressionKind { Wink, Smile, FakeSmile, PuckerUp, Amazed, Taunt }

[Flags]
public enum ExpressionChoices
{
    None = 0, Wink = 1, Smile = 2, FakeSmile = 4, PuckerUp = 8, Amazed = 16, Taunt = 32,
    All = Wink | Smile | FakeSmile | PuckerUp | Amazed | Taunt,
}

public sealed record ExpressionDefinition(ExpressionKind Kind, string Name, string Command, float Hold)
{
    public ExpressionChoices Choice => (ExpressionChoices)(1 << (int)Kind);
}

public static class Expressions
{
    public static readonly ExpressionDefinition[] All =
    [
        new(ExpressionKind.Wink, "Wink", "/wink motion", 0.3f),
        new(ExpressionKind.Smile, "Smile", "/smile motion", 2f),
        new(ExpressionKind.FakeSmile, "Fake smile", "/fakesmile motion", 1.8f),
        new(ExpressionKind.PuckerUp, "Pucker up", "/puckerup motion", 1.4f),
        new(ExpressionKind.Amazed, "Amazed", "/amazed motion", 2f),
        new(ExpressionKind.Taunt, "Taunt", "/taunt motion", 1.6f),
    ];

    public static ExpressionChoices Pool(Personality personality, ExpressionChoices custom) => personality switch
    {
        Personality.Wink => ExpressionChoices.Wink,
        Personality.Playful => ExpressionChoices.Wink | ExpressionChoices.PuckerUp,
        Personality.Friendly => ExpressionChoices.Smile | ExpressionChoices.FakeSmile,
        Personality.Dramatic => ExpressionChoices.Amazed | ExpressionChoices.Taunt,
        Personality.Custom => custom & ExpressionChoices.All,
        _ => ExpressionChoices.None,
    };
}

public sealed class ExpressionPicker
{
    private ExpressionKind? previous;
    public ExpressionDefinition? Pick(ExpressionChoices pool, Random random)
    {
        var options = Expressions.All.Where(e => (pool & e.Choice) != 0).ToArray();
        if (options.Length == 0) return null;
        if (options.Length > 1) options = options.Where(e => e.Kind != previous).ToArray();
        var picked = options[random.Next(options.Length)];
        previous = picked.Kind;
        return picked;
    }
}

// All steps run on the framework thread; no delayed callback can target a stale actor.
public sealed class ExpressionPlayback
{
    private readonly Queue<(string Command, float Delay)> steps = new();
    private nint owner;
    private float remaining;
    private bool expressing;
    public bool IsBusy => steps.Count > 0;
    public string? CurrentName { get; private set; }

    public bool Start(ExpressionDefinition expression, float hold, nint actor, Func<string, bool> send)
    {
        if (actor == 0 || IsBusy || !send(expression.Command)) return false;
        owner = actor;
        expressing = true;
        CurrentName = expression.Name;
        hold = float.IsFinite(hold) ? Math.Clamp(hold, 0.1f, 5f) : expression.Hold;
        if (expression.Kind == ExpressionKind.Wink)
        {
            steps.Enqueue(("/smile motion", hold));
            steps.Enqueue(("/smile motion", 0.35f)); // bounded emote-lock fallback
            steps.Enqueue(("/straightface motion", 2f));
        }
        else steps.Enqueue(("/straightface motion", hold));
        steps.Enqueue(("/straightface motion", 0.65f));
        remaining = steps.Peek().Delay;
        return true;
    }

    public void Update(float seconds, nint actor, Func<string, bool> send)
    {
        if (actor == 0 || actor != owner) { Discard(); return; }
        if (!IsBusy) return;
        remaining -= float.IsFinite(seconds) ? Math.Max(seconds, 0) : 0;
        if (remaining > 0) return;
        // One step per frame, relative to actual dispatch, including after a stall.
        var next = steps.Dequeue();
        send(next.Command);
        if (next.Command == "/straightface motion") { expressing = false; CurrentName = null; }
        if (IsBusy) remaining = steps.Peek().Delay;
        else Discard();
    }

    public void Cancel(nint actor, Func<string, bool> send, bool forceNeutral = false)
    {
        if (actor == 0 || (owner != 0 && actor != owner)) { Discard(); return; }
        if (!expressing && !forceNeutral) return; // don't restart cleanup on every paused frame
        Discard();
        send("/straightface motion");
        owner = actor;
        steps.Enqueue(("/straightface motion", 0.65f));
        remaining = 0.65f;
    }

    public void Discard()
    {
        steps.Clear(); owner = 0; remaining = 0; expressing = false; CurrentName = null;
    }
}
