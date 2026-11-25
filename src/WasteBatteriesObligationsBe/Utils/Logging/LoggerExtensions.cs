namespace WasteBatteriesObligationsBe.Utils.Logging;

internal static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled exception")]
    public static partial void UnhandledException(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Ensuring index is created if it does not exist for collection {CollectionName} in DB {DatabaseName}")]
    public static partial void EnsuringIndexes(
        this ILogger logger,
        string collectionName,
        string databaseName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Request validation failed: {ValidationErrors}")]
    public static partial void RequestValidationFailed(
        this ILogger logger,
        string validationErrors);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Stored obligation calculation {CalculationId} version {Version} for scheme {SchemeOrgId} compliance year {CompliancePeriodYear}")]
    public static partial void StoredObligationCalculation(
        this ILogger logger,
        string calculationId,
        int version,
        string schemeOrgId,
        int compliancePeriodYear);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Stored obligation calculation {CalculationId} version {Version} superseding {SupersededCalculationId} for scheme {SchemeOrgId} compliance year {CompliancePeriodYear}")]
    public static partial void StoredObligationCalculationVersion(
        this ILogger logger,
        string calculationId,
        int version,
        string supersededCalculationId,
        string schemeOrgId,
        int compliancePeriodYear);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Failed to save obligation calculation")]
    public static partial void FailedToSaveObligationCalculation(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "Obligation calculation is invalid: {ValidationErrors}")]
    public static partial void ObligationCalculationInvalid(
        this ILogger logger,
        string validationErrors);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Error,
        Message = "Obligation calculation input is invalid: {ValidationErrors}")]
    public static partial void ObligationCalculationInputInvalid(
        this ILogger logger,
        string validationErrors);

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Searching for {Name}, found {Result}")]
    public static partial void SearchingForExample(
        this ILogger logger,
        string name,
        object? result);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Searching for {ExampleId}, found {Result}")]
    public static partial void SearchingForExampleData(
        this ILogger logger,
        string exampleId,
        object? result);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Creating proxy http client")]
    public static partial void CreatingProxyHttpClient(
        this ILogger logger);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "HTTP_PROXY is NOT set, proxy client will be disabled")]
    public static partial void ProxyNotConfigured(
        this ILogger logger);
}
