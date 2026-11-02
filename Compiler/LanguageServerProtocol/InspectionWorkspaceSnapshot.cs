using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Captures immutable source references before leaving workspace synchronization.
/// </summary>
internal sealed record InspectionWorkspaceSnapshot(SourceSnapshot Selected, IReadOnlyList<SourceSnapshot> OpenSources, IReadOnlyList<Uri> Projects);

/// <summary>
/// Signals an expected editor-input failure, distinct from compiler implementation failures.
/// </summary>
internal sealed class InspectionInputException : Exception
{
    public InspectionInputException() : base("Invalid inspection input.") { }
    public InspectionInputException(string message) : base(message) { }
    public InspectionInputException(string message, Exception innerException) : base(message, innerException) { }
}