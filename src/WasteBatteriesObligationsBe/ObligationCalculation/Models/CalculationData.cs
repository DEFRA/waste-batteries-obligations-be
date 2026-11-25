using System.Text.Json.Serialization;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Models;

// The submissions and trigger for one obligation version.
// Events will supply this after reading the submissions for the scheme and year.
// The trigger is what started this run.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CalculationData
{
    public int CompliancePeriodYear { get; init; }

    public List<PomSubmissionForCalculation> Submissions { get; init; } = [];

    public ObligationTrigger Trigger { get; init; } = new();

    public CalculationData Trimmed() => this with
    {
        Submissions = Submissions?.ConvertAll(submission => submission is null ? null! : submission.Trimmed())!,
        Trigger = Trigger?.Trimmed()!
    };
}
