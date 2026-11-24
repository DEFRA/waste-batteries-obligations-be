namespace WasteBatteriesObligationsBe.Config;

/// <summary>
/// The CDP platform environment, as opposed to the ASP.NET host environment.
/// </summary>
internal static class CdpEnvironment
{
    /// <summary>
    /// Process variable CDP injects into every service (<c>dev</c>, <c>test</c>, <c>prod</c>, …).
    /// </summary>
    internal const string VariableName = "ENVIRONMENT";

    /// <summary>
    /// CDP's name for the production environment. Not <c>Production</c>: that is the ASP.NET host
    /// name, and CDP sets <c>ASPNETCORE_ENVIRONMENT</c> to <c>Production</c> in every environment.
    /// </summary>
    internal const string ProductionName = "prod";

    /// <summary>
    /// Read <see cref="VariableName"/> from the process.
    /// </summary>
    /// <remarks>
    /// Deliberately not read through <see cref="IConfiguration"/>. <c>ENVIRONMENT</c> is also the
    /// host environment key, so an <c>ASPNETCORE_ENVIRONMENT</c> value shadows it and configuration
    /// returns <c>Production</c> rather than the platform name.
    /// </remarks>
    internal static string? Read() => Environment.GetEnvironmentVariable(VariableName);

    internal static bool IsProduction(string? cdpEnvironment) =>
        string.Equals(cdpEnvironment?.Trim(), ProductionName, StringComparison.OrdinalIgnoreCase);
}
