namespace Sushi.Semantics;

/// <summary>
/// Defines the termination statuses reserved by the implemented program-exit contract.
/// </summary>
internal static class ProgramTerminationCodes
{
    /// <summary>
    /// The successful graceful-termination status, which may also be requested explicitly.
    /// </summary>
    public const int GracefulSuccess = 0;

    /// <summary>
    /// The fatal status for an invalid application termination-code request.
    /// </summary>
    public const int InvalidTerminationCode = 1;
}
