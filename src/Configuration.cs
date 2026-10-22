using Dalamud.Configuration;

namespace Glance;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public bool Enabled = true;
    public float Distance = 3.0f;
    public float Response = 45.0f;
    public bool ExtendedCloseUp;
    public bool FineCloseUpZoom = true;
    public float CloseUpZoomDelta = 0.1f;
    public float CloseUpTransitionSeconds = 0.35f;
    public bool OverrideCloseUpAngle = true;
    public float CloseUpCameraAngle = 50f;
    public float CloseUpAngleDistance = 1.5f;
    public float MinimumCameraDistance = 0.5f;
    public AngleRecovery? PendingAngleRecovery;
    public Personality Personality = Personality.Wink;
    public ExpressionChoices CustomExpressions = ExpressionChoices.Wink;
    public HashSet<uint> CustomEmoteIds = new();
    public Dictionary<uint, float> EmoteHolds = new();
    public bool AllowBodyMotions = true;
    public int ExpressionFrequency = 4;
    public float ExpressionDelay = 2f;
    public Dictionary<ExpressionKind, float> ExpressionHolds = new();

    public void Sanitize()
    {
        if (Version < 2)
        {
            // Upgrade saved settings too, so existing installs get the quicker gaze.
            Response *= 5f;
            Version = 2;
        }
        if (Version < 3)
        {
            Response *= 1.5f;
            Version = 3;
        }
        Distance = float.IsFinite(Distance) ? Math.Clamp(Distance, 1f, 10f) : 3f;
        Response = float.IsFinite(Response) ? Math.Clamp(Response, 5f, 90f) : 45f;
        MinimumCameraDistance = CloseUpLimit.Sanitize(MinimumCameraDistance);
        CloseUpZoomDelta = CloseUpZoom.Sanitize(CloseUpZoomDelta);
        CloseUpTransitionSeconds = float.IsFinite(CloseUpTransitionSeconds) ? Math.Clamp(CloseUpTransitionSeconds, 0.1f, 1.5f) : 0.35f;
        CloseUpCameraAngle = float.IsFinite(CloseUpCameraAngle) ? Math.Clamp(CloseUpCameraAngle, 0f, 100f) : 50f;
        CloseUpAngleDistance = float.IsFinite(CloseUpAngleDistance) ? Math.Clamp(CloseUpAngleDistance, 0.2f, 3f) : 1.5f;
        if (!Enum.IsDefined(Personality)) Personality = Personality.Wink;
        CustomExpressions &= ExpressionChoices.All;
        CustomEmoteIds ??= new();
        EmoteHolds ??= new();
        foreach (var id in EmoteHolds.Keys.ToArray())
            EmoteHolds[id] = float.IsFinite(EmoteHolds[id]) ? Math.Clamp(EmoteHolds[id], 0.1f, 30f) : 8f;
        ExpressionFrequency = Math.Clamp(ExpressionFrequency, 1, 10);
        ExpressionDelay = float.IsFinite(ExpressionDelay) ? Math.Clamp(ExpressionDelay, 0f, 5f) : 2f;
        ExpressionHolds ??= new();
        foreach (var definition in Expressions.All)
        {
            var value = ExpressionHolds.GetValueOrDefault(definition.Kind, definition.Hold);
            ExpressionHolds[definition.Kind] = float.IsFinite(value) ? Math.Clamp(value, 0.1f, 5f) : definition.Hold;
        }
        Version = 4;
    }
}


