namespace Glance;

[Serializable]
public sealed class AngleRecovery
{
    public ulong ContentId;
    public float Original;
    public uint SystemPadMode;
    public uint CharacterPadMode;
    public bool IsValid => ContentId != 0 && float.IsFinite(Original)
        && Original >= -0.0801f && Original <= 0.2101f;
    public bool Matches(ulong contentId, uint systemPadMode, uint characterPadMode) =>
        Matches(contentId) && SystemPadMode == systemPadMode && CharacterPadMode == characterPadMode;
    public bool Matches(ulong contentId) => IsValid && contentId != 0 && ContentId == contentId;
}
