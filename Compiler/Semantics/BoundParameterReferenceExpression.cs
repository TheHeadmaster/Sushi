namespace Sushi.Semantics;

/// <summary>
/// References a resolved function parameter without retaining or repeating its source spelling.
/// </summary>
public sealed record BoundParameterReferenceExpression : BoundExpression
{
    /// <summary>
    /// Gets the authoritative bound parameter.
    /// </summary>
    public BoundParameter Parameter { get; }

    /// <summary>
    /// Creates a typed reference to a function parameter.
    /// </summary>
    /// <param name="parameter">
    /// The referenced parameter.
    /// </param>
    public BoundParameterReferenceExpression(BoundParameter parameter) : base(parameter?.Type ?? throw new ArgumentNullException(nameof(parameter)))
    {
        this.Parameter = parameter;
    }
}