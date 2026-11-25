namespace WasteBatteriesObligationsBe.ObligationCalculation.Validators;

// Text for rejected calculation data. The stored calculation has its own messages.
public static class CalculationDataValidationMessages
{
    public const string CompliancePeriodYearRange = "compliancePeriodYear must be a calendar year.";

    public const string WindowOutsideCalendar = "The compliance window must stay within calendar years.";

    public const string SubmissionsRequired = "At least one POM submission is required.";

    public const string SubmissionRequired = "submissions cannot contain a null submission.";

    public const string SchemeMismatch = "All submissions must be for the same scheme.";

    public const string SubmissionRepeated = "Each submission can be included once.";

    public const string SubmissionYearOutsideWindow =
        "Every submission year must be the compliance year or an earlier year in the window.";

    public const string SubmissionIdRequired = "id is required.";

    public const string SubmissionIdObjectId = "id must be an ObjectId.";

    public const string SchemeOrgIdRequired = "schemeOrgId is required.";

    public const string SchemeOrgIdObjectId = "schemeOrgId must be an ObjectId.";

    public const string QuarterRequired = "compliancePeriodQuarter is required.";

    public const string QuarterUnknown =
        "compliancePeriodQuarter must be Quarter 1, Quarter 2, Quarter 3 or Quarter 4.";

    public const string TonnesRequired = "tonnes are required.";

    public const string TonnesFormat = "tonnes must have exactly 3 decimal places.";
}
