namespace Sushi.Semantics;

/// <summary>
/// References a previously initialized local without duplicating its declared identity.
/// </summary>
public sealed record BoundLocalReferenceExpression : BoundExpression
{
    /// <summary>
    /// Gets the authoritative local declaration.
    /// </summary>
    public BoundLocal Local { get; }

    /// <summary>
    /// Creates a typed reference to an existing local.
    /// </summary>
    /// <param name="local">
    /// The previously declared local.
    /// </param>
    public BoundLocalReferenceExpression(BoundLocal local) : base(local?.Type ?? throw new ArgumentNullException(nameof(local)))
    {
        this.Local = local;
    }
}