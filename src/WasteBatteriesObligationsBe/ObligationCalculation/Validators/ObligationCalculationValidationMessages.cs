namespace WasteBatteriesObligationsBe.ObligationCalculation.Validators;

// Text for a rejected calculation or a version conflict.
public static class ObligationCalculationValidationMessages
{
    public const string SchemeOrgIdRequired = "schemeOrgId is required.";

    public const string SchemeOrgIdObjectId = "schemeOrgId must be an ObjectId.";

    public const string SchemeNameRequired = "schemeName is required.";

    public const string CompliancePeriodYearRange = "compliancePeriodYear must be a calendar year.";

    public const string ObligationTonnesFormat = "obligationTonnes must have exactly 3 decimal places.";

    public const string PlacedOnMarketRequired = "placedOnMarketByYear must contain one entry for each year in the window.";

    public const string PlacedOnMarketYearRequired = "placedOnMarketByYear cannot contain a null year.";

    public const string YearRange = "year must be a calendar year.";

    public const string TonnesFormat = "tonnes must have exactly 3 decimal places.";

    public const string AnnualisedTonnesFormat = "annualisedTonnes must have exactly 3 decimal places.";

    public const string QuartersRange = "quartersOfData must be from 0 to 4.";

    public const string AnnualisedEstimateMismatch =
        "isAnnualisedEstimate must be true only when annualisedTonnes differs from tonnes.";

    public const string MissingYearBlank =
        "A year with no quarters is 0.000 tonnes and has no source submissions.";

    public const string EarlierYearScaled =
        "An earlier year uses its tonnes. Only the compliance year is annualised.";

    public const string SourceSubmissionIdsRequired = "sourceSubmissionIds are required.";

    public const string SourceSubmissionIdObjectId = "sourceSubmissionIds must be ObjectIds.";

    public const string ParametersRequired = "parameters are required.";

    public const string TargetRequired = "parameters.target is required.";

    public const string TargetType = "parameters.target.type must be percentage.";

    public const string TargetValueFormat = "parameters.target.value must have exactly 2 decimal places.";

    public const string WindowYearsRange = "parameters.windowYears must be at least 1.";

    public const string DenominatorRule = "parameters.denominatorRule must be yearsWithData.";

    public const string TriggerRequired = "trigger is required.";

    public const string TriggerType = "trigger.type must be event or manual.";

    public const string EventIdRequired = "trigger.eventId is required.";

    public const string CorrelationIdRequired = "trigger.correlationId is required.";

    public const string ActorRequired = "trigger.actor is required.";

    public const string ReasonRequired = "trigger.reason is required.";

    public const string EventIdNotAllowed = "trigger.eventId is only set for an event calculation.";

    public const string CorrelationIdNotAllowed = "trigger.correlationId is only set for an event calculation.";

    public const string ActorNotAllowed = "trigger.actor is only set for a manual calculation.";

    public const string ReasonNotAllowed = "trigger.reason is only set for a manual calculation.";

    public const string CurrentCalculationExists =
        "A current obligation calculation already exists for this scheme and compliance year.";

    public const string CurrentCalculationMissing =
        "A current obligation calculation does not exist for this scheme and compliance year.";

    // The version found before the save was no longer current when the new version was written.
    public const string CurrentCalculationChanged =
        "The current obligation calculation changed before this version was saved.";
}
