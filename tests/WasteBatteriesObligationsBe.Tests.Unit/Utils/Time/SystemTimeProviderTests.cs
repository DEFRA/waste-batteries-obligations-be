using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Unit.Utils.Time;

public class SystemTimeProviderTests
{
    [Fact]
    public void UtcNow_WhenCalled_ReturnsAUtcTime()
    {
        var now = new SystemTimeProvider().UtcNow;

        Assert.Equal(DateTimeKind.Utc, now.Kind);
        Assert.Equal(0, now.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.InRange(now, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }
}
