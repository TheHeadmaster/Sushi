namespace Sushi.Intermediate;

/// <summary>
/// Identifies a mathematical integer comparison in structured Sushi IR.
/// </summary>
public enum IRIntegerComparisonOperator
{
    /// <summary>
    /// Compares operands for equality.
    /// </summary>
    Equal,

    /// <summary>
    /// Compares operands for inequality.
    /// </summary>
    NotEqual,

    /// <summary>
    /// Tests signed less-than ordering.
    /// </summary>
    LessThan,

    /// <summary>
    /// Tests signed less-than-or-equal ordering.
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// Tests signed greater-than ordering.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Tests signed greater-than-or-equal ordering.
    /// </summary>
    GreaterThanOrEqual
}