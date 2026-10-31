
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Represents a semantically valid activity declaration with resolved lifecycle method identities.
/// Execution, ownership state, and method-body binding belong to later compiler stages.
/// </summary>
/// <param name="Name">
/// The decoded source-level activity name.
/// </param>
/// <param name="Accessibility">
/// The declared accessibility of the activity.
/// </param>
/// <param name="Lifetime">
/// The activity's explicit task or persistent lifetime policy.
/// </param>
/// <param name="Entry">
/// The designated entry method, which every concrete activity must declare.
/// </param>
/// <param name="Stop">
/// The optional runtime-invoked stop method.
/// </param>
/// <param name="Declaration">
/// The authoritative source declaration for this activity.
/// </param>
public sealed record BoundActivity(
    string Name,
    Accessibility Accessibility,
    ActivityLifetime Lifetime,
    BoundActivityMethod Entry,
    BoundActivityMethod? Stop,
    ActivityDeclarationSyntax Declaration)
{
    /// <summary>
    /// Gets whether this activity is declared as the executable program root.
    /// </summary>
    public bool IsRoot => this.Declaration.RootKeyword is { IsSourceBacked: true };

    /// <summary>
    /// Gets the activity's source range for diagnostics and declaration identity.
    /// </summary>
    public SourceSpan Span => this.Declaration.Span;
}
