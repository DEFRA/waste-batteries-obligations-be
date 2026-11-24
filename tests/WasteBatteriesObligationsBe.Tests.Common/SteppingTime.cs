using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Common;

// Each read is one millisecond later, so two saves of one scheme get two timestamps.
public sealed class SteppingTime(DateTime start) : ITimeProvider
{
    private long _ticks = start.Ticks;

    public DateTime UtcNow
    {
        get
        {
            var ticks = Interlocked.Add(ref _ticks, TimeSpan.TicksPerMillisecond) - TimeSpan.TicksPerMillisecond;
            return new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public void Reset() => Interlocked.Exchange(ref _ticks, start.Ticks);
}
