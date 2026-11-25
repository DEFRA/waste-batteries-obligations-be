namespace WasteBatteriesObligationsBe.Utils.Time;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}

public sealed class SystemTimeProvider : ITimeProvider
{
    // BSON Date keeps milliseconds. The value written on create is the value a later read returns.
    public DateTime UtcNow
    {
        get
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        }
    }
}
