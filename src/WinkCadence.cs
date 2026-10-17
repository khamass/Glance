namespace Glance;

public sealed class WinkCadence
{
    private int visits;
    private bool countedVisit;

    public void LeaveZoom() => countedVisit = false;
    public void Reset() { visits = 0; countedVisit = false; }

    public bool EnterZoom(int frequency = 4)
    {
        if (countedVisit) return false;
        countedVisit = true;
        visits = (visits + 1) % Math.Clamp(frequency, 1, 10);
        return visits == 0;
    }
}

