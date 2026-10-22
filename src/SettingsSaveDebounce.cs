namespace Glance;

internal sealed class SettingsSaveDebounce
{
    public bool Pending { get; private set; }
    private long saveAt;
    public void Changed(long now) { Pending = true; saveAt = now + 500; }
    public bool Due(long now) => Pending && now >= saveAt;
    public void Saved() => Pending = false;
}
