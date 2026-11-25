using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Common;

public sealed class FixedTime(DateTime utcNow) : ITimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}
