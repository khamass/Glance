namespace Glance;

internal readonly record struct GposeTransition(bool Entered, bool Exited, bool Preserve, bool ReclaimGaze);

internal sealed class GposeSession
{
    private bool active;
    private nint gazeOwner;

    public GposeTransition Update(bool inGpose, bool preserve, nint player, bool ownsGaze)
    {
        if (inGpose)
        {
            if (active) return default;
            active = true;
            gazeOwner = preserve && ownsGaze ? player : 0;
            return new(true, false, preserve, false);
        }
        if (!active) return default;
        active = false;
        var reclaim = player != 0 && player == gazeOwner;
        gazeOwner = 0;
        return new(false, true, false, reclaim);
    }
}
