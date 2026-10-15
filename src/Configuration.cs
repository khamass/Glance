using Dalamud.Configuration;

namespace Glance;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public bool Enabled = true;
    public float Distance = 3.0f;
    public float Response = 45.0f;

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
    }
}

