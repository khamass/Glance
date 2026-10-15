namespace Glance;

public sealed class WinkCadence
{
    private int visits;
    private bool countedVisit;

    public void LeaveZoom() => countedVisit = false;

    public bool EnterZoom()
    {
        if (countedVisit) return false;
        countedVisit = true;
        visits = (visits + 1) % 4;
        return visits == 0;
    }
}

