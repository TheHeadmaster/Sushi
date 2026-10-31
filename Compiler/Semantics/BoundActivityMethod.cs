
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Represents a lifecycle method whose declaration identity and signature have been validated.
/// Its executable body has not yet been bound.
/// </summary>
/// <param name="Name">
/// The decoded semantic name, including removal of any escaped-identifier prefix.
/// </param>
/// <param name="Accessibility">
/// The declared accessibility of the lifecycle method.
/// </param>
/// <param name="Declaration">
/// The source declaration retained for later body binding and code generation.
/// </param>
public sealed record BoundActivityMethod(string Name, Accessibility Accessibility, ActivityMethodDeclarationSyntax Declaration)
{
    /// <summary>
    /// Gets the method's source location for diagnostics.
    /// </summary>
    public SourceSpan Span => this.Declaration.Span;

    /// <summary>
    /// Gets whether the method is designated as an activity entry point.
    /// </summary>
    public bool IsEntry => this.Declaration.LifecycleModifier.Type is SyntaxType.EntryKeyword;

    /// <summary>
    /// Gets whether the method is designated as an activity stop hook.
    /// </summary>
    public bool IsStop => this.Declaration.LifecycleModifier.Type is SyntaxType.StopKeyword;
}
