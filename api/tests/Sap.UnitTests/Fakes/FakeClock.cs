namespace Sap.UnitTests.Fakes;

/// <summary>A clock the test can move forward, so expiry is tested without waiting.</summary>
public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
